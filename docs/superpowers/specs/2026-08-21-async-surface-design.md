# Async surface: genuine I/O, and an honest shape for the rest

Design for [#8](https://github.com/ktsu-dev/Essentials/issues/8) — "Most async methods are
`Task.Run` wrappers, not genuine async I/O".

Status: approved in brainstorming, not yet implemented.

## Problem

55 async members across 8 interfaces route through one helper:

```csharp
internal static Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken)
    => cancellationToken.IsCancellationRequested
        ? Task.FromCanceled<T>(cancellationToken)
        : Task.Run(action, cancellationToken);
```

Two distinct harms, pulling in opposite directions:

- **Stream paths.** `TryCompressAsync(Stream, Stream, …)` releases the calling thread and then blocks
  a thread-pool thread for the whole duration of the I/O. The thread was relocated, not freed. Under
  load this is how thread-pool starvation begins.
- **In-memory paths.** `EncodeAsync` on a 200-byte buffer costs a thread-pool dispatch, a `Task`
  allocation and a context switch to do microseconds of work. It also takes a decision away from the
  caller, who is the only party that knows whether offloading is worth it.

### Already fixed, contrary to the issue

The issue says the README claims the opposite of the truth. That was corrected in
[#9](https://github.com/ktsu-dev/Essentials/pull/9). The README now reads:

> Stream hashing is genuinely asynchronous — it reads with `ReadAsync` and holds no thread. Most
> other async variants are convenience wrappers that run synchronous work on the thread pool.

So the issue's option 3 ("document accurately") is done, and no consumer is being actively misled.
That removes the urgency, not the defect.

### Verified inventory

Counted against the tree at 2.1.1, classifying by whether the signature touches a `Stream`,
`TextWriter` or `TextReader`:

| Interface | Sites | Stream-ish | In-memory |
| --- | --- | --- | --- |
| `ICompressionProvider` | 10 | 6 | 4 |
| `IEncodingProvider` | 10 | 6 | 4 |
| `IEncryptionProvider` | 10 | 6 | 4 |
| `IObfuscationProvider` | 10 | 6 | 4 |
| `ICacheProvider` | 6 | 0 | 6 |
| `ISerializationProvider` | 5 | 2 | 3 |
| `IValidationProvider` | 3 | 0 | 3 |
| `IHashProvider` | 1 | 0 | 1 |
| **Total** | **55** | **26** | **29** |

`IPersistenceProvider` and `ICommandExecutor` declare their async members abstractly and their
implementations are genuinely asynchronous. They are not in scope.

## Decisions

1. **Ship in two releases.** The stream work is additive; the in-memory work is breaking. Shipping
   them together would hold the non-breaking improvement hostage to the breaking one.
2. **Stream paths keep `Task`, and become genuinely asynchronous.**
3. **In-memory paths become `ValueTask`, computed synchronously on the caller's thread.**

The third decision deserves its rationale recorded, because `ValueTask` is easy to misread as an
idiom for backgrounding. It is not — it says nothing about threading. It is the idiom for "async
shaped, usually completes synchronously", and its benefit is avoiding a `Task` allocation on that
path. Choosing it means the library **stops offloading on the caller's behalf**; a caller who wants
work backgrounded writes `Task.Run` themselves, where the knowledge about UI threads and payload
size actually lives.

The pairing has a property worth keeping deliberately: **the return type becomes the honest signal
the issue asks for.** `ValueTask` means "runs on your thread, now"; `Task` means "real I/O, actually
yields". A consumer can tell which is which from the signature instead of reading the implementation.
That is why the stream paths are not also migrated to `ValueTask` — genuine I/O rarely completes
synchronously, so it would take on the await-once and never-store rules with no allocation to save,
and would erase the distinction.

## Release 1 — 2.2.0 `[minor]`, non-breaking

The `*Async` members are **default interface implementations**. A provider that declares a matching
member supplies the interface implementation itself and the default is not used. So genuine async can
be added provider by provider with **no interface change at all**, and third-party providers keep the
existing fallback.

### In scope

Providers whose underlying primitive is natively streaming *and* natively async:

| Area | Providers | Primitive |
| --- | --- | --- |
| Compression | Gzip, Deflate, ZLib, Brotli | `GZipStream` / `DeflateStream` / `ZLibStream` / `BrotliStream` plus `CopyToAsync` |
| Encryption | Aes | `CryptoStream` plus `CopyToAsync` |

These are also the cases the issue names as the real harm: large payloads over disk or network.

That covers **12 of the 26 stream-ish sites** — the compression and encryption halves. The remaining
14 (encoding 6, obfuscation 6, serialization 2) are excluded for the reasons below, and the issue
should record that it is partially rather than wholly addressed by this release.

### Deliberately not in scope

**Encoding and obfuscation stream paths.** The original plan was one generic asynchronous default
covering all of them, needing no provider changes. That is not sound. Their transforms have
incompatible streaming shapes:

- `Hex` streams byte-by-byte and buffers nothing, so a generic buffering default would be a memory
  regression for large inputs.
- `Reverse` must hold the entire input before it can emit anything, and already buffers.
- `Base64` is only chunk-safe on three-byte boundaries.

No single default is correct for all three: a chunked one breaks `Reverse` and `Base64`, a buffering
one regresses `Hex`. Making this safe would need each provider to declare its own chunking
constraint, which is new interface surface for cheap in-memory transforms whose stream overloads are
a convenience rather than a throughput path. Left as-is and documented.

**Serialization.** The two stream-ish members are `TrySerializeAsync(object, TextWriter, …)` and
`DeserializeAsync<T>(TextReader, …)`. In both, the serialization work itself is CPU-bound and only
the surrounding read or write is I/O. An async version would serialize synchronously and then
`await writer.WriteAsync(…)`, or `await reader.ReadToEndAsync()` and then deserialize synchronously
— honest at the I/O boundary, but leaving the bulk of the method exactly as it is. Marginal, and it
invites the reader to think more was fixed than was.

Worth revisiting separately: `System.Text.Json` and `Newtonsoft.Json` both offer genuinely
asynchronous `Stream`-based APIs, but `ISerializationProvider` exposes `TextWriter`/`TextReader`
rather than `Stream`, so reaching them would mean new interface surface. That is a design question
about the serialization abstraction, not part of this issue.

**`ICommandExecutor.Execute`.** Blocks on `ExecuteAsync(...).Result` behind a VSTHRD002 suppression.
Sync-over-async is a different defect class — deadlock risk under a synchronization context, and
exceptions surfacing as `AggregateException` — not thread waste. Gets its own issue.

### Testing

The claim to prove is "holds no thread", which a timing assertion cannot establish without flaking.
Instead, a test double:

```csharp
/// A stream supporting only the asynchronous members. The synchronous ones throw, so an
/// implementation that is asynchronous in name only fails loudly instead of passing silently.
internal sealed class AsyncOnlyStream : Stream
{
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) { /* real */ }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) { /* real */ }
}
```

A fake-async implementation reaches for the synchronous path and throws; a genuine one passes. This
is deterministic and machine-independent. Each converted provider gets:

1. An `AsyncOnlyStream` test proving it never touches the synchronous members.
2. A round-trip test asserting the async path produces byte-identical output to the sync path.
3. A cancellation test asserting an already-cancelled token is honoured before work begins.

`Stream.Read(byte[], int, int)` is the abstract member the base class routes its other synchronous
overloads through, so throwing there catches every synchronous entry point.

## Release 2 — 3.0.0 `[major]`, breaking

Migrate the 29 in-memory async members from `Task<T>` to `ValueTask<T>`, computed synchronously:

```csharp
public ValueTask<byte[]> EncodeAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    => cancellationToken.IsCancellationRequested
        ? ValueTask.FromCanceled<byte[]>(cancellationToken)
        : new ValueTask<byte[]>(Encode(data.Span));
```

`ProviderHelpers.RunAsync` loses its in-memory callers and is deleted if nothing else uses it.

`ICacheProvider` fits `ValueTask` beyond the allocation argument: a future distributed cache provider
would be genuinely asynchronous, and `ValueTask` accommodates both a cached hit that completes
synchronously and a network miss that does not. A completed `Task` cannot express the second without
allocating.

### Consumer impact

Six repositories in the local workspace reference `ktsu.Essentials`: `GitLfsCache`,
`GitIntegration`, `GitBranchStateCache`, `ImageGui`, `MusicAnalyzer`, `BlastMerge`.

`await provider.EncodeAsync(x)` is unchanged — `ValueTask` is awaitable and those call sites compile
as they are. What breaks is code that *stores* the result, awaits it twice, passes it to
`Task.WhenAll`, or blocks on `.Result`. Each dependent needs a recompile and a scan for those
patterns; `.AsTask()` is the escape hatch where a real `Task` is genuinely needed.

The behaviour change is the part with no compiler error to announce it: work that used to run on a
thread-pool thread now runs on the caller's. For buffers already in memory this is microseconds, but
a caller who called `EncodeAsync` specifically to get off a UI thread will now block on it. This must
be called out in the changelog and the migration notes, not only in the type signature.

## Out of scope

- `ICommandExecutor.Execute` sync-over-async — separate issue.
- A genuine backgrounding feature (queued work, progress reporting) — a different abstraction, not
  something to express through per-method async wrappers.
- `IPersistenceProvider` and `ICommandExecutor` async members — already genuine.
