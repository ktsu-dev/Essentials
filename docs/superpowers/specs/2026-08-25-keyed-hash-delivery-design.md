# Keyed hashing: delivering issue #5 without the AEAD half

## Status

This is a delivery spec, not a replacement design. The interface design of record is
[`2026-08-19-keyed-hashing-and-incremental-hashing-design.md`](2026-08-19-keyed-hashing-and-incremental-hashing-design.md),
whose Part 2 covers issue #5. Every decision in that document stands. This spec records three things
that document does not settle:

1. The delivery is split, so authenticated encryption no longer ships alongside keyed hashing.
2. How the three HMAC providers share an implementation, which the earlier spec left open.
3. What changed on `main` since 2026-08-19 that makes point 2 matter.

Part 1 of the earlier spec shipped as PR #9 and closed issue #6. Part 2 has not shipped. Part 3,
documentation, is split across both deliveries and the remainder travels with this one.

## Scope

### In scope

- `Essentials/IKeyedHashProvider.cs`, exactly the surface specified in the earlier spec.
- `Essentials/FixedTimeComparison.cs`, forwarding to `CryptographicOperations.FixedTimeEquals`.
- Three packages: `ktsu.Essentials.KeyedHashProviders.HmacSha256`, `.HmacSha384`, and `.HmacSha512`.
- `Shared/HmacKeyedHashCore.cs`, linked into all three.
- Documentation of the confidentiality-only guarantee on `IEncryptionProvider` and
  `AesEncryptionProvider`.
- Tests, `Essentials.All` wiring, and the README and CLAUDE.md updates for this surface.

### Deliberately deferred

`IAuthenticatedEncryptionProvider` and the AES-GCM package move to their own issue and their own
pull request. The earlier spec placed them in the same delivery as keyed hashing, and that pairing
is worth breaking. They share no code, and AEAD carries a design question that keyed hashing does
not: whether an interface that returns a tag can express every AEAD mode a caller might want, or
only the one the first implementation happens to use. Shipping keyed hashing first also unblocks the
consumer named in the issue, which needs a MAC and not an AEAD.

The reference to `IAuthenticatedEncryptionProvider` in the new `IEncryptionProvider` remarks is
dropped from this delivery, because pointing at a type that does not exist yet is worse than not
pointing at all. It returns when the AEAD work lands.

HMAC-MD5 and HMAC-SHA1 remain unshipped, for the reason the earlier spec gives: they are not broken
as MACs, but placing them in a new security-facing category invites misuse, and a consumer who needs
one implements the two primitives themselves.

## What changed since 2026-08-19

`main` currently fails its SonarCloud quality gate on `new_duplicated_lines_density`, at 6.8% against
a 3% threshold, from 504 duplicated lines. The largest contributor is the FNV hash provider cluster
at 214 lines, four files of 168 lines each that differ in two lines of logic and their doc comments.
The compression providers contribute roughly 192 more.

That is the same shape this work would produce. Three HMAC providers written independently differ
only in an algorithm name and a hash length, so they would duplicate against each other at
much the same rate and push a gate that is already red further from green.

The earlier spec says new projects "follow the existing scaffolding convention". Followed literally,
for three providers this near-identical, that convention is the defect.

## Implementation approach: one shared core

`Shared/HmacKeyedHashCore.cs`, linked into all three provider projects, following the precedent of
`Shared/NonCryptoIncrementalHash.cs`, which is `internal sealed` in namespace `ktsu.Essentials` and
already linked into six provider projects:

```xml
<Compile Include="..\Shared\HmacKeyedHashCore.cs" Link="HmacKeyedHashCore.cs" />
```

`internal` is required rather than stylistic. Each package compiles its own copy, so a public type
would collide across packages for any consumer referencing more than one.

The core carries the algorithm-independent work, parameterized by `HashAlgorithmName` and hash
length:

```csharp
internal static class HmacKeyedHashCore
{
    internal static bool TryHash(HashAlgorithmName algorithm, int hashLengthBytes,
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);

    internal static bool TryHash(HashAlgorithmName algorithm, int hashLengthBytes,
        ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten);

    internal static IIncrementalHash CreateIncremental(HashAlgorithmName algorithm,
        int hashLengthBytes, ReadOnlySpan<byte> key);
}
```

Each provider reduces to its distinct content:

```csharp
public class HmacSha256KeyedHashProvider : IKeyedHashProvider
{
    public int HashLengthBytes => 32;

    /// <inheritdoc/>
    public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
        => HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA256, HashLengthBytes, key, data, destination, out bytesWritten);

    // the Stream primitive and CreateIncremental follow the same one-line shape
}
```

Roughly 25 lines of distinct content per provider instead of 170, and no cross-provider duplication
for Sonar to find.

`CreateIncremental` is overridden rather than inherited. The buffering default specified in the
earlier spec exists for third-party implementers, and silently inheriting it is the trap that spec
already names for `IHashProvider`. `IncrementalHash.CreateHMAC` makes the override a single line.

## Platform constraints

Carried forward from the earlier spec, which verified these by compiling probe projects against each
target framework rather than assuming them. Re-confirmed against the netstandard2.1 reference
assembly for this work:

Available on netstandard2.1 and every later target:

- `IncrementalHash.CreateHMAC(HashAlgorithmName, byte[])`
- `CryptographicOperations.FixedTimeEquals` and `CryptographicOperations.ZeroMemory`
- `HMACSHA256` and friends, including `TryComputeHash(ReadOnlySpan<byte>, Span<byte>, out int)`

Not available on netstandard2.1:

- `IncrementalHash.HashLengthInBytes`. Immaterial, because every provider knows its own length as a
  constant.

No conditional compilation is expected in this delivery. The earlier spec's single permitted `#if`
was for AES-GCM construction, which has moved out of scope.

One implementation-time check remains, and it is an optimization rather than a correctness
question: whether a span-accepting `CreateHMAC` overload exists on the newer targets. The `byte[]`
overload is available everywhere and is the fallback, so the work proceeds either way. Confirm it by
building all six target frameworks rather than by reading documentation.

### Key material

`IncrementalHash.CreateHMAC` takes `byte[]` on the floor target, so the key is copied there. Every
copy is zeroed with `CryptographicOperations.ZeroMemory` once the HMAC is constructed, and every
`IncrementalHash` is disposed. This is the reason the core owns key handling rather than each
provider repeating it, and getting it wrong in one of three places is exactly the failure the shared
core prevents.

The pooled read buffer in the async stream path is returned with `clearArray: true`, consistent with
the fix in issue #12. `ArrayPool<byte>.Shared` is process-wide, so the tail of an authenticated
message would otherwise stay readable to whatever rents next.

## Testing

All tests live in the existing `Essentials.Tests` project. A second test project would silently lose
coverage, because KtsuBuild runs one solution-level `dotnet test --coverage` and every test project
writes the same output file.

- **RFC 4231 known-answer vectors** for HMAC-SHA256, HMAC-SHA384, and HMAC-SHA512. These prove
  interoperability with every other implementation, which round-trip tests cannot. They include the
  oversized-key case, where a key longer than the block size is hashed first, and that path is easy
  to get wrong and invisible to self-consistency testing.
- **Agreement across all four paths.** One-shot, stream, incremental, and async must produce
  identical output for identical input, with the incremental case driven at several chunk boundaries.
- **`Verify` fails closed.** True for a correct tag, false for a tag with any single bit flipped,
  false for a correct tag under the wrong key, and false for a truncated tag.
- **Undersized destination buffers return false** rather than throwing, matching the contract every
  other provider category follows.
- **The buffer length contract**, mirroring `ProviderContractTests`: report the exact bytes written
  and leave the rest of the caller's buffer untouched.
- **DI registration**, resolving each provider both concretely and through `IKeyedHashProvider`.

Note for whoever writes these: check which overload the tests actually bind to. Pre-existing async
tests in this repo bound to the `ReadOnlyMemory`/`string` overloads, which is how six rewritten
public bodies nearly shipped untested during the async stream work.

## Documentation

`IEncryptionProvider` gains `<remarks>` stating that it provides confidentiality only, that
ciphertext is not tamper-evident, and that a caller needing integrity must authenticate the
ciphertext separately. `AesEncryptionProvider` gains a note that it is CBC with PKCS7, that its
ciphertext is malleable, and that a decrypt-then-parse caller becomes a padding oracle. This mirrors
the warning `IObfuscationProvider` already carries in its summary. No code in either type changes.

`README.md` gains a keyed hashing bullet and an API Reference section, and its encryption bullet is
reworded to confidentiality-only. `CLAUDE.md` gains the new provider category, the key files, and
the new test file. `DESCRIPTION.md` and `TAGS.md` go through the `update-docs` skill rather than
being hand-edited.

The README overclaim named in issue #6, on line 35, was addressed in the Part 1 delivery and is not
revisited here.

## Delivery

One pull request tagged `[minor]`, carrying the interface, the helper, three packages, the shared
core, the `Essentials.All` wiring, the documentation, and the tests. Purely additive, so no major
version.

A follow-up issue is filed for `IAuthenticatedEncryptionProvider` and AES-GCM, carrying the design
already written in Part 2 of the earlier spec so none of that thinking is lost.
