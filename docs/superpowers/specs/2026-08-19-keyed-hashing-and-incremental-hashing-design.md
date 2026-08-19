# Keyed Hashing, Incremental Hashing, and Authenticated Encryption

Design for GitHub issues [#6](https://github.com/ktsu-dev/Essentials/issues/6) and
[#5](https://github.com/ktsu-dev/Essentials/issues/5).

## Problem

Both issues come from [ktsu-dev/GitLfsCache](https://github.com/ktsu-dev/GitLfsCache), which had to
bypass `ktsu.Essentials` and call the base class library directly.

**Issue #6.** `IHashProvider` can hash a stream, but only synchronously, and cannot hash data
incrementally as it arrives. `HashAsync` takes `ReadOnlyMemory<byte>`, so the data is already
buffered. A caller hashing something too large to buffer has no asynchronous option. GitLfsCache
streams multi-gigabyte objects that must each be verified against a SHA-256 digest, so it wraps its
write sink in a stream that digests as it goes, using `IncrementalHash` from the BCL.

**Issue #5.** Every hash provider is unkeyed, so no message authentication code can be computed
through the abstraction. `IEncryptionProvider` has no authentication tag anywhere in its surface, so
an authenticated mode cannot be expressed through it at all, and `AesEncryptionProvider` builds on
`Aes.Create()` defaults — CBC with PKCS7 padding, which is malleable. A caller who reaches for "the
encryption provider" reasonably expects tampering to be detected, gets no indication from the API
that it is not, and has no keyed-hash provider available to add authentication with.

## Platform constraints

Verified by compiling probe projects against each target framework rather than assumed.

Available on **netstandard2.1** and every other target:

- `IncrementalHash`, including `AppendData(ReadOnlySpan<byte>)`, `TryGetHashAndReset(Span<byte>, out int)`,
  `GetHashAndReset()`, and `CreateHMAC(HashAlgorithmName, byte[])`
- `CryptographicOperations.FixedTimeEquals` and `CryptographicOperations.ZeroMemory`
- `HMACSHA256` including `TryComputeHash(ReadOnlySpan<byte>, Span<byte>, out int)`
- `AesGcm`

Not available on netstandard2.1:

- `IncrementalHash.HashLengthInBytes` (.NET 6+). Immaterial — every provider knows its own hash
  length as a constant.
- `AesGcm(byte[] key, int tagSizeInBytes)` (.NET 8+). The single-argument constructor is obsolete
  from .NET 8 (`SYSLIB0053`) and warnings are errors here, so the AES-GCM provider needs one
  `#if NET8_0_OR_GREATER` around construction. This is the last-resort conditional compilation that
  `CLAUDE.md` permits, and it is the only one in this work.

## Decisions

| Decision | Choice | Consequence |
| --- | --- | --- |
| How `IIncrementalHash` attaches to `IHashProvider` | `CreateIncremental()` added with a **default body** | Non-breaking; both issues ship as minors |
| Scope of authenticated encryption | New sibling `IAuthenticatedEncryptionProvider` + AES-GCM | `IEncryptionProvider` untouched; no encrypt-then-MAC composite |
| Keyed hash package set | HMAC-SHA256 / 384 / 512 | Three packages; no HMAC-MD5 or HMAC-SHA1 |

Rejected: making `CreateIncremental()` abstract (would break external implementers and force 3.0.0);
a separate `IIncrementalHashProvider` capability interface (forces callers to type-test); and an
encrypt-then-MAC composite provider (would commit the library to a permanent wire framing format and
key-derivation scheme for the benefit of one caller).

## Part 1 — Issue #6

The two halves of #6 are not independent. Once `CreateIncremental()` exists, the async stream
overload is a short genuinely-async read loop written once in the interface, which every provider
inherits. Building it in the other order would produce a `Task.Run` wrapper that does not solve the
problem — it moves the blocked thread to the pool rather than releasing it, and a held thread during
a full disk read is precisely what the issue objects to.

### New file: `Essentials/IIncrementalHash.cs`

```csharp
public interface IIncrementalHash : IDisposable
{
    public int HashLengthBytes { get; }
    public void Append(ReadOnlySpan<byte> data);
    public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten);
    public byte[] GetHashAndReset();   // default body over TryGetHashAndReset
}
```

`HashLengthBytes` is not in the issue's proposal. It is included so a caller holding only the
incremental object can size a destination buffer.

There are deliberately no async members. A caller in an async loop already has the bytes in hand and
calls `Append(buffer.Span)`, which is CPU work. This is the shape GitLfsCache's `HashingStream`
already uses.

### Added to `IHashProvider`, both with default bodies

```csharp
public IIncrementalHash CreateIncremental() => new BufferingIncrementalHash(this);

public async Task<bool> TryHashAsync(Stream data, Memory<byte> destination, CancellationToken cancellationToken = default)
{
    if (data is null || destination.Length < HashLengthBytes)
    {
        return false;
    }

    using IIncrementalHash hash = CreateIncremental();
    byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
    try
    {
        int read;
        while ((read = await data.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.Append(buffer.AsSpan(0, read));
        }

        return hash.TryGetHashAndReset(destination.Span, out int bytesWritten)
            && bytesWritten == HashLengthBytes;
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}

public Task<byte[]> HashAsync(Stream data, CancellationToken cancellationToken = default);  // default body over the above
```

`TryHashAsync` returns `Task<bool>` rather than reporting `bytesWritten`, because an `out` parameter
cannot cross an await boundary. The existing `TryEncryptAsync(Stream, …)` sets the precedent. Hash
output is fixed length, so the documented contract is that `true` guarantees exactly
`HashLengthBytes` bytes were written.

The `destination.Span` access sits after every await in a single expression, so no span local
crosses an await.

### `Essentials/BufferingIncrementalHash.cs` (internal)

Accumulates appended bytes into a `MemoryStream` and calls the provider's existing `TryHash(Stream, …)`
on `TryGetHashAndReset`. Correct for any provider, but it buffers.

This is the accepted cost of the non-breaking option. Because it also backs `TryHashAsync`, a
third-party provider that does not override `CreateIncremental()` silently gets the buffering
behaviour that #6 complains about. `CreateIncremental()`'s XML documentation must state this plainly
and direct implementers to override it.

### All fifteen providers override `CreateIncremental()`

No provider is left on the buffering fallback.

- **MD5, SHA1, SHA256, SHA384, SHA512** — wrap `IncrementalHash.CreateHash(HashAlgorithmName.X)`
  in a shared internal adapter.
- **CRC32, CRC64, XxHash32, XxHash64, XxHash3, XxHash128** — the `System.IO.Hashing` types derive
  from `NonCryptographicHashAlgorithm` and already expose `Append` and `TryGetHashAndReset`.
- **FNV1 and FNV1a, 32- and 64-bit** — keep the running `uint`/`ulong` accumulator. Genuinely
  simpler than the existing one-shot form.

The non-cryptographic providers currently use the static one-shot API (`Crc32.TryHash`) on the span
path and the instance API on the stream path. The contract test below is what proves those agree.

### Testing

Applied to every one of the fifteen providers:

- **Incremental equals one-shot**, fed in uneven chunks with boundaries that are not aligned to the
  algorithm's block size. This is the test that catches endianness or reset mismatches between the
  static and instance paths.
- **Reset and reuse** — a second digest from the same object after `GetHashAndReset` matches a fresh one.
- **Empty input** through the incremental path equals the one-shot empty digest.
- **Async stream equals sync stream** equals one-shot.
- **Cancellation** — a cancelled token on `TryHashAsync` surfaces as cancellation.
- **`TryHashAsync` with an undersized destination** returns false.

## Part 2 — Issue #5

`IncrementalHash.CreateHMAC` is available on netstandard2.1, so `IKeyedHashProvider` reuses the
`IIncrementalHash` type from Part 1 rather than defining a parallel one. Part 1 therefore lands first.

### New file: `Essentials/IKeyedHashProvider.cs`

```csharp
public interface IKeyedHashProvider
{
    public int HashLengthBytes { get; }

    // The only two members an implementer must write:
    public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);
    public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten);

    // Default bodies, mirroring IHashProvider:
    public byte[] Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data);
    public byte[] Hash(ReadOnlySpan<byte> key, Stream data);
    public byte[] Hash(ReadOnlySpan<byte> key, string data);
    public Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    public Task<bool> TryHashAsync(ReadOnlyMemory<byte> key, Stream data, Memory<byte> destination, CancellationToken cancellationToken = default);
    public Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, Stream data, CancellationToken cancellationToken = default);
    public IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key);
    public bool Verify(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected);
}
```

**The key is passed per call, not bound at construction.** This matches `IEncryptionProvider` and
keeps providers stateless singletons. `SHA256HashProvider`'s own remarks record a past defect where
a provider held algorithm state in a field and concurrent callers corrupted each other's in-progress
hash; a key-holding provider would reintroduce that class of bug.

`CreateIncremental(key)` gets a buffering default body for the same reason as Part 1, so third-party
implementers need only write the two `TryHash` methods. The buffering implementation copies the key
and zeroes it with `CryptographicOperations.ZeroMemory` on disposal.

**`Verify` is the answer to the issue's fixed-time comparison request.** The issue proposes a helper
because comparing tags with `==` is the obvious next mistake, but a helper still has to be
remembered. `Verify` computes and compares in fixed time so the caller never performs a comparison
at all.

`FixedTimeComparison.Equals(ReadOnlySpan<byte>, ReadOnlySpan<byte>)` is also exposed publicly in
`ktsu.Essentials` for callers holding a tag obtained elsewhere. It forwards to
`CryptographicOperations.FixedTimeEquals`; it exists so consumers do not need to know that.

### Keyed hash packages

| Package | Class |
| --- | --- |
| `ktsu.Essentials.KeyedHashProviders.HmacSha256` | `HmacSha256KeyedHashProvider` |
| `ktsu.Essentials.KeyedHashProviders.HmacSha384` | `HmacSha384KeyedHashProvider` |
| `ktsu.Essentials.KeyedHashProviders.HmacSha512` | `HmacSha512KeyedHashProvider` |

All three implement over `IncrementalHash.CreateHMAC`. HMAC-MD5 and HMAC-SHA1 are not shipped: they
are not broken as MACs, but placing them in a new security-facing category invites misuse, and a
consumer who needs one can implement `IKeyedHashProvider` in a few lines.

Registration follows the existing pattern — `AddHmacSha256KeyedHashProvider()` registering a
singleton both as the concrete type and via `TryAddEnumerable` as `IKeyedHashProvider`.

### New file: `Essentials/IAuthenticatedEncryptionProvider.cs`

```csharp
public interface IAuthenticatedEncryptionProvider
{
    public int NonceLengthBytes { get; }
    public int TagLengthBytes { get; }
    public byte[] GenerateKey();
    public byte[] GenerateNonce();

    public bool TryEncrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
                           ReadOnlySpan<byte> associatedData, Span<byte> ciphertext, Span<byte> tag);

    public bool TryDecrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
                           ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> tag, Span<byte> plaintext);

    // Default bodies:
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
                          ReadOnlySpan<byte> associatedData = default);   // returns ciphertext || tag
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertextAndTag, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
                          ReadOnlySpan<byte> associatedData = default);
}
```

For GCM the ciphertext length equals the plaintext length, so there is no `GetMaxEncryptedLength`
growth factor and no padding.

Three deliberate departures from `IEncryptionProvider`:

- **No `Stream` overloads.** GCM cannot safely stream decryption: plaintext must not be released
  before the tag verifies, and .NET's `AesGcm` exposes no streaming API. A `TryDecrypt(Stream, Stream)`
  would either buffer everything while claiming to stream, or emit unverified plaintext, which is a
  security defect. Span-only is the honest surface.
- **A `nonce`, not an `iv`.** The name differs because the requirement differs: nonce reuse under a
  single key is catastrophic for GCM rather than merely sloppy. The XML documentation states this.
- **No async members.** The data must fit in memory regardless, AEAD over an in-memory buffer is
  fast, and a `Task.Run` wrapper would add a thread hop for no benefit.

`Encrypt` returns `ciphertext || tag`. This is a framing commitment, but appending a fixed-length tag
is universal AEAD convention rather than a bespoke format. The nonce stays out of the returned blob
and remains caller-managed so that its uniqueness requirement stays visible.

### Authenticated encryption package

`ktsu.Essentials.AuthenticatedEncryptionProviders.AesGcm` → `AesGcmAuthenticatedEncryptionProvider`.

- 12-byte nonce, 16-byte tag, 32-byte generated key
- One `#if NET8_0_OR_GREATER` around construction, per the platform constraints above
- `TryDecrypt` catches `CryptographicException` — which covers `AuthenticationTagMismatchException` —
  returns false, and clears the destination span so no unverified plaintext is observable

### Documentation of the existing encryption surface

`IEncryptionProvider` gains `<remarks>` stating that it provides confidentiality only, that
ciphertext is not tamper-evident, that a caller needing integrity must authenticate the ciphertext
separately, and pointing at `IAuthenticatedEncryptionProvider`. `AesEncryptionProvider` gains a note
that it is CBC with PKCS7, that its ciphertext is malleable, and that a decrypt-then-parse caller
becomes a padding oracle. This mirrors the warning the obfuscation providers already carry.

No code in `IEncryptionProvider` or `AesEncryptionProvider` changes.

### Testing

- **Known-answer vectors, not only round-trips.** RFC 4231 for HMAC-SHA256/384/512; NIST GCM
  vectors for AES-GCM.
- **The tamper matrix**, each of which must fail closed: flipped ciphertext byte, flipped tag byte,
  wrong associated data, wrong nonce, wrong key.
- **`Verify` returns false** for a wrong tag and true for a correct one.
- **Incremental HMAC equals one-shot HMAC**, same chunking approach as Part 1.
- **`TryDecrypt` leaves no plaintext** in the destination after an authentication failure.
- **Undersized destination buffers** return false rather than throwing.
- **DI registration** — every new provider resolves both concretely and through its interface.

## Part 3 — Documentation

`README.md`:

- **Line 35 is the overclaim named in issue #6**: "Every operation has async variants with proper
  `CancellationToken` support." It remains false even after Part 1, because span-destination async
  overloads cannot exist and the new AEAD interface deliberately has none. Replace it with an
  accurate claim that also records which async variants are `Task.Run` wrappers over synchronous work
  and which — the new stream hashing path — are genuinely asynchronous. `CLAUDE.md` documents this
  distinction internally; consumers should see it too.
- Line 23: reword the encryption bullet to confidentiality-only; add keyed hashing and authenticated
  encryption bullets.
- The hash usage example gains incremental and async-stream snippets.
- The custom provider example gains a `CreateIncremental()` override, since silently inheriting the
  buffering default is the trap.
- API Reference: new rows on the `IHashProvider` table; new `IIncrementalHash`, `IKeyedHashProvider`,
  and `IAuthenticatedEncryptionProvider` sections; a confidentiality-only note on the
  `IEncryptionProvider` section.

`CLAUDE.md`: key-files list, the two new provider categories, the new test files.

`DESCRIPTION.md` and `TAGS.md`: refreshed via the `update-docs` skill rather than hand-edited, so
they match house style.

## Delivery

Two pull requests, each tagged `[minor]`, each carrying its own documentation changes. No third
documentation PR.

1. **Issue #6** — `IIncrementalHash`, `CreateIncremental()`, async stream overloads, fifteen provider
   overrides, tests, README and CLAUDE.md updates for that surface.
2. **Issue #5** — `IKeyedHashProvider`, `FixedTimeComparison`, three HMAC packages,
   `IAuthenticatedEncryptionProvider`, the AES-GCM package, the `IEncryptionProvider` documentation
   fix, tests, and the remaining documentation updates.

Both are purely additive. Neither requires a major version.

New projects follow the existing scaffolding convention — one project per implementation, referenced
from `Essentials.All`, and registered in `AddEssentials()`. The `new-provider` skill scaffolds them.

Issue #5's author offered to send a pull request for the keyed hash provider. That offer should be
answered on the issue before implementation starts, since this design covers it.
