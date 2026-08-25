# Keyed Hash Provider Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `IKeyedHashProvider` and three HMAC provider packages so callers can compute and verify message authentication codes without leaving Essentials.

**Architecture:** A new interface in the `Essentials` interfaces package with two required primitives and eight default bodies, mirroring `IHashProvider`. Three provider packages each delegate to one `internal` implementation linked from `Shared/`, so the providers do not duplicate against each other. Verification is a default method that computes and compares in fixed time, so callers never write a tag comparison themselves.

**Tech Stack:** C#, .NET (net10.0 through net6.0 and netstandard2.1), ktsu.Sdk, MSTest, `System.Security.Cryptography.IncrementalHash`.

**Spec:** `docs/superpowers/specs/2026-08-25-keyed-hash-delivery-design.md`, which defers to `docs/superpowers/specs/2026-08-19-keyed-hashing-and-incremental-hashing-design.md` for the interface design of record.

## Global Constraints

- **Target frameworks:** `net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1` on every new project.
- **No conditional compilation.** Every API used is available on netstandard2.1. If you reach for `#if`, stop and reconsider.
- **Warnings are errors.** A build with any warning fails.
- **Tabs for indentation.** CRLF line endings. File-scoped namespaces. Using directives inside the namespace.
- **No `this.` qualifiers.** Name constructor parameters so they differ from fields.
- **Always brace control flow.** Always specify accessibility modifiers.
- **No global suppressions.** Use targeted `[SuppressMessage]` with a real justification.
- **File header:** every file starts with `// Copyright (c) 2023-2026 ktsu-dev contributors` followed by a blank line.
- **Preserve each file's existing byte order mark.** The repo is genuinely mixed and that is not a defect.
- **Commit tags:** `[minor]` on the commit that completes the feature, `[patch]` on the rest. Tag goes at the end of a lowercase conventional-commit subject. No `Co-Authored-By` lines.
- **Never stage `.gitignore`.** It shows as modified in `git status` but is not modified. Stage files explicitly by path, never `git add -A`.
- **All tests go in the existing `Essentials.Tests` project.** A second test project silently loses coverage, because KtsuBuild runs one solution-level `dotnet test --coverage` and every test project writes the same output file.
- **Check which overload a test actually binds to** before assuming it covers the method you changed. A `byte[]` argument converts to `ReadOnlySpan<byte>`, `ReadOnlyMemory<byte>`, and `Span<byte>` alike, so an intended test of the span path can silently bind elsewhere. This is how six rewritten public bodies nearly shipped untested during the async stream work. Where it matters, assert against a value only the intended overload can produce, or step through once to confirm.

## File Structure

**Interfaces package (`Essentials/`)**
- `IKeyedHashProvider.cs` — new. The contract: 2 required members, 8 defaults.
- `BufferingKeyedIncrementalHash.cs` — new, `internal sealed`. Backs the `CreateIncremental` default so third-party implementers need only write the two primitives.
- `FixedTimeComparison.cs` — new, public static. For callers holding a tag obtained elsewhere.
- `IEncryptionProvider.cs` — modify. Documentation only.

**Shared (`Shared/`)**
- `HmacKeyedHashCore.cs` — new, `internal static`. Linked into all three provider projects. Must be `internal`: each package compiles its own copy, so a public type would collide for a consumer referencing two of them.

**Provider packages** — three new projects, each with a `.csproj`, a provider class, and `ServiceCollectionExtensions.cs`:
- `Essentials.KeyedHashProviders.HmacSha256/`
- `Essentials.KeyedHashProviders.HmacSha384/`
- `Essentials.KeyedHashProviders.HmacSha512/`

**Aggregation**
- `Essentials.All/Essentials.All.csproj` and `Essentials.All/ServiceCollectionExtensions.cs` — modify.
- `Essentials.slnx` — modify.

**Tests**
- `Essentials.Tests/KeyedHashProviderTests.cs` — new.

---

### Task 1: FixedTimeComparison

The smallest independent unit, and nothing else depends on it existing first except `Verify` in Task 2.

**Files:**
- Create: `Essentials/FixedTimeComparison.cs`
- Test: `Essentials.Tests/KeyedHashProviderTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public static bool FixedTimeComparison.Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)` in namespace `ktsu.Essentials`.

- [ ] **Step 1: Write the failing test**

Create `Essentials.Tests/KeyedHashProviderTests.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using ktsu.Essentials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class KeyedHashProviderTests
{
	#region FixedTimeComparison

	[TestMethod]
	public void FixedTimeComparison_Matches_Identical_Spans()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3, 4];

		Assert.IsTrue(FixedTimeComparison.Equals(left, right));
	}

	[TestMethod]
	public void FixedTimeComparison_Rejects_Single_Bit_Difference()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3, 5];

		Assert.IsFalse(FixedTimeComparison.Equals(left, right));
	}

	[TestMethod]
	public void FixedTimeComparison_Rejects_Different_Lengths()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3];

		Assert.IsFalse(FixedTimeComparison.Equals(left, right));
	}

	[TestMethod]
	public void FixedTimeComparison_Matches_Empty_Spans()
	{
		Assert.IsTrue(FixedTimeComparison.Equals([], []));
	}

	#endregion
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~FixedTimeComparison"`

Expected: compile failure, `FixedTimeComparison` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Essentials/FixedTimeComparison.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Security.Cryptography;

/// <summary>
/// Compares byte sequences in an amount of time that does not depend on their contents.
/// </summary>
/// <remarks>
/// Comparing an authentication tag with <c>==</c>, <c>SequenceEqual</c>, or any comparison that
/// returns early on the first differing byte leaks where the difference is. An attacker who can
/// measure that can recover a valid tag one byte at a time. Prefer
/// <see cref="IKeyedHashProvider.Verify"/>, which computes and compares in one step; use this only
/// when the tag to compare against was produced elsewhere.
/// </remarks>
public static class FixedTimeComparison
{
	/// <summary>
	/// Determines whether two byte sequences are equal, in a time that does not vary with their contents.
	/// </summary>
	/// <param name="left">The first sequence.</param>
	/// <param name="right">The second sequence.</param>
	/// <returns>True if the sequences have the same length and contents, false otherwise.</returns>
	public static bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		=> CryptographicOperations.FixedTimeEquals(left, right);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~FixedTimeComparison"`

Expected: 4 passed.

If an analyzer rejects the member name `Equals` on a static class (CA1716 or similar, which is an error here because warnings are errors), do not rename it. The spec of record names this member. Add a targeted `[SuppressMessage]` on the method with a justification saying the name is the established one for this operation.

- [ ] **Step 5: Commit**

```bash
git add Essentials/FixedTimeComparison.cs Essentials.Tests/KeyedHashProviderTests.cs
git commit -m "feat: add FixedTimeComparison for authentication tag comparison [patch]"
```

---

### Task 2: IKeyedHashProvider and its buffering default

Delivers the contract. Tested through a minimal fake implementer that supplies only the two primitives, which is exactly what a third-party implementer would write, so this also proves the defaults work for them.

**Files:**
- Create: `Essentials/IKeyedHashProvider.cs`
- Create: `Essentials/BufferingKeyedIncrementalHash.cs`
- Test: `Essentials.Tests/KeyedHashProviderTests.cs`

**Interfaces:**
- Consumes: `FixedTimeComparison.Equals` from Task 1. `IIncrementalHash` and `ProviderHelpers.RunAsync` already exist.
- Produces: `IKeyedHashProvider` in namespace `ktsu.Essentials`, with members:
  - `int HashLengthBytes { get; }`
  - `bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)`
  - `bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)`
  - `IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key)`
  - `Task<bool> TryHashAsync(ReadOnlyMemory<byte> key, Stream data, Memory<byte> destination, CancellationToken cancellationToken = default)`
  - `Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, Stream data, CancellationToken cancellationToken = default)`
  - `Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)`
  - `byte[] Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)`
  - `byte[] Hash(ReadOnlySpan<byte> key, string data)`
  - `byte[] Hash(ReadOnlySpan<byte> key, Stream data)`
  - `bool Verify(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected)`

- [ ] **Step 1: Write the failing test**

Add to `Essentials.Tests/KeyedHashProviderTests.cs`, inside the class, after the `FixedTimeComparison` region. Add `using System.IO;`, `using System.Linq;`, `using System.Text;`, and `using System.Threading.Tasks;` to the file's usings.

```csharp
	#region Default interface implementations

	/// <summary>
	/// A minimal implementer supplying only the two required primitives, which is what a third-party
	/// implementer writes. Exercising the defaults through this proves they do not secretly depend on
	/// anything a real provider overrides.
	/// </summary>
	/// <remarks>
	/// The "MAC" is deliberately trivial and is not a real construction: each output byte is the
	/// running sum of the data XORed with a key byte. It only needs to be deterministic, key-dependent,
	/// and data-dependent for these tests to mean something.
	/// </remarks>
	private sealed class FakeKeyedHashProvider : IKeyedHashProvider
	{
		public int HashLengthBytes => 8;

		public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
		{
			bytesWritten = 0;

			if (destination.Length < HashLengthBytes)
			{
				return false;
			}

			for (int i = 0; i < HashLengthBytes; i++)
			{
				byte accumulator = key.Length > 0 ? key[i % key.Length] : (byte)0;
				for (int j = 0; j < data.Length; j++)
				{
					accumulator = (byte)(accumulator + data[j] + i);
				}

				destination[i] = accumulator;
			}

			bytesWritten = HashLengthBytes;
			return true;
		}

		public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)
		{
			bytesWritten = 0;

			if (data is null)
			{
				return false;
			}

			using MemoryStream copy = new();
			data.CopyTo(copy);
			return TryHash(key, copy.ToArray(), destination, out bytesWritten);
		}
	}

	private static readonly byte[] FakeKey = Encoding.UTF8.GetBytes("fake-key");
	private static readonly byte[] FakePayload = Encoding.UTF8.GetBytes("the quick brown fox");

	[TestMethod]
	public void Defaults_Hash_Span_Matches_TryHash()
	{
		FakeKeyedHashProvider provider = new();
		byte[] expected = new byte[provider.HashLengthBytes];
		Assert.IsTrue(provider.TryHash(FakeKey, FakePayload, expected, out int written));
		Assert.AreEqual(provider.HashLengthBytes, written);

		byte[] actual = provider.Hash(FakeKey, FakePayload);

		CollectionAssert.AreEqual(expected, actual);
	}

	[TestMethod]
	public void Defaults_Hash_Stream_Matches_Hash_Span()
	{
		FakeKeyedHashProvider provider = new();
		using MemoryStream stream = new(FakePayload);

		byte[] fromStream = provider.Hash(FakeKey, stream);

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), fromStream);
	}

	[TestMethod]
	public void Defaults_Hash_String_Matches_Utf8_Bytes()
	{
		FakeKeyedHashProvider provider = new();

		byte[] fromString = provider.Hash(FakeKey, "the quick brown fox");

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), fromString);
	}

	[TestMethod]
	public void Defaults_CreateIncremental_Matches_One_Shot()
	{
		FakeKeyedHashProvider provider = new();
		using IIncrementalHash incremental = provider.CreateIncremental(FakeKey);
		incremental.Append(FakePayload.AsSpan(0, 5));
		incremental.Append(FakePayload.AsSpan(5));

		byte[] actual = incremental.GetHashAndReset();

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public async Task Defaults_TryHashAsync_Matches_One_Shot()
	{
		FakeKeyedHashProvider provider = new();
		using MemoryStream stream = new(FakePayload);
		byte[] actual = new byte[provider.HashLengthBytes];

		bool ok = await provider.TryHashAsync(FakeKey, stream, actual);

		Assert.IsTrue(ok);
		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public async Task Defaults_HashAsync_Memory_Matches_One_Shot()
	{
		FakeKeyedHashProvider provider = new();

		byte[] actual = await provider.HashAsync(FakeKey, FakePayload);

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public async Task Defaults_HashAsync_Stream_Matches_One_Shot()
	{
		FakeKeyedHashProvider provider = new();
		using MemoryStream stream = new(FakePayload);

		byte[] actual = await provider.HashAsync(FakeKey, stream);

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public void Defaults_TryHash_Rejects_Undersized_Destination()
	{
		FakeKeyedHashProvider provider = new();
		byte[] tooSmall = new byte[provider.HashLengthBytes - 1];

		Assert.IsFalse(provider.TryHash(FakeKey, FakePayload, tooSmall, out int written));
		Assert.AreEqual(0, written);
	}

	[TestMethod]
	public void Defaults_Verify_Accepts_Correct_Tag()
	{
		FakeKeyedHashProvider provider = new();
		byte[] tag = provider.Hash(FakeKey, FakePayload);

		Assert.IsTrue(provider.Verify(FakeKey, FakePayload, tag));
	}

	[TestMethod]
	public void Defaults_Verify_Rejects_Flipped_Tag_Bit()
	{
		FakeKeyedHashProvider provider = new();
		byte[] tag = provider.Hash(FakeKey, FakePayload);
		tag[0] ^= 0x01;

		Assert.IsFalse(provider.Verify(FakeKey, FakePayload, tag));
	}

	[TestMethod]
	public void Defaults_Verify_Rejects_Wrong_Key()
	{
		FakeKeyedHashProvider provider = new();
		byte[] tag = provider.Hash(FakeKey, FakePayload);
		byte[] wrongKey = Encoding.UTF8.GetBytes("other-key");

		Assert.IsFalse(provider.Verify(wrongKey, FakePayload, tag));
	}

	[TestMethod]
	public void Defaults_Verify_Rejects_Truncated_Tag()
	{
		FakeKeyedHashProvider provider = new();
		byte[] tag = provider.Hash(FakeKey, FakePayload);

		Assert.IsFalse(provider.Verify(FakeKey, FakePayload, tag.AsSpan(0, tag.Length - 1)));
	}

	#endregion
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~KeyedHashProviderTests"`

Expected: compile failure, `IKeyedHashProvider` does not exist.

- [ ] **Step 3: Write the buffering incremental hash**

Create `Essentials/BufferingKeyedIncrementalHash.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// An <see cref="IIncrementalHash"/> that accumulates every appended byte and hashes the result in
/// one pass, for keyed hash providers that do not supply a genuinely incremental implementation.
/// </summary>
/// <remarks>
/// Correct for any provider, but it holds the whole input in memory, which is the cost incremental
/// hashing exists to avoid. It backs the default body of
/// <see cref="IKeyedHashProvider.CreateIncremental"/> so that implementers need only write the two
/// required primitives; providers are expected to override it. The key is copied on construction and
/// zeroed on disposal, so the instance must be disposed.
/// </remarks>
internal sealed class BufferingKeyedIncrementalHash : IIncrementalHash
{
	private readonly IKeyedHashProvider provider;
	private readonly byte[] keyCopy;
	private readonly MemoryStream buffer = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="BufferingKeyedIncrementalHash"/> class.
	/// </summary>
	/// <param name="keyedHashProvider">The provider whose one-shot stream hashing produces the digest.</param>
	/// <param name="key">The key, copied into this instance and zeroed on disposal.</param>
	internal BufferingKeyedIncrementalHash(IKeyedHashProvider keyedHashProvider, ReadOnlySpan<byte> key)
	{
		provider = keyedHashProvider;
		keyCopy = key.ToArray();
	}

	/// <inheritdoc/>
	public int HashLengthBytes => provider.HashLengthBytes;

	/// <inheritdoc/>
	public void Append(ReadOnlySpan<byte> data) => buffer.Write(data);

	/// <inheritdoc/>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		buffer.Position = 0;
		bool hashed = provider.TryHash(keyCopy, buffer, destination, out bytesWritten);
		buffer.SetLength(0);
		return hashed;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		CryptographicOperations.ZeroMemory(keyCopy);
		buffer.Dispose();
	}
}
```

- [ ] **Step 4: Write the interface**

Create `Essentials/IKeyedHashProvider.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for keyed hash providers, which compute a message authentication code over data using
/// a secret key.
/// </summary>
/// <remarks>
/// A keyed hash answers "was this produced by someone holding the key, and is it unmodified", which
/// an unkeyed <see cref="IHashProvider"/> cannot. <see cref="IEncryptionProvider"/> provides
/// confidentiality but not integrity, so a caller who needs tamper detection over ciphertext
/// authenticates it with one of these.
/// <para>
/// The key is passed per call rather than bound at construction, which matches
/// <see cref="IEncryptionProvider"/> and keeps providers stateless singletons. A provider holding
/// key or algorithm state in a field is the defect recorded in the remarks on the SHA-256 provider,
/// where concurrent callers corrupted each other's in-progress hash.
/// </para>
/// </remarks>
public interface IKeyedHashProvider
{
	/// <summary>
	/// The length of the authentication tag in bytes.
	/// </summary>
	public int HashLengthBytes { get; }

	/// <summary>
	/// Tries to compute the authentication tag for the specified data.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the tag was written, false if the buffer was too small or the key rejected.</returns>
	public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Tries to compute the authentication tag for the data in the specified stream.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate. Read to its end from its current position.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the tag was written, false if the stream was null, the buffer too small, or the key rejected.</returns>
	public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Creates a keyed incremental hash that accepts data in successive chunks.
	/// </summary>
	/// <remarks>
	/// The default implementation accumulates every appended byte in memory and computes the tag in
	/// one pass when it is requested. That is correct but it buffers the entire input, so implementers
	/// should override this with a genuinely incremental implementation. Doing so also lets
	/// <see cref="TryHashAsync(ReadOnlyMemory{byte}, Stream, Memory{byte}, CancellationToken)"/>
	/// stream properly, because that method is built on this one.
	/// </remarks>
	/// <param name="key">The secret key.</param>
	/// <returns>A new keyed incremental hash. The caller owns it and should dispose it, which zeroes the key copy.</returns>
	public IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key) => new BufferingKeyedIncrementalHash(this, key);

	/// <summary>
	/// Asynchronously computes the authentication tag over a stream, reading it in one pass.
	/// </summary>
	/// <remarks>
	/// The key is <see cref="ReadOnlyMemory{T}"/> rather than <see cref="ReadOnlySpan{T}"/> because a
	/// span cannot cross an await boundary. The result is not reported through an <c>out</c> parameter
	/// for the same reason; a return value of true guarantees exactly <see cref="HashLengthBytes"/>
	/// bytes were written.
	/// <para>
	/// The read buffer is scrubbed on its way back to the pool. <see cref="ArrayPool{T}"/>.Shared is
	/// process-wide, so without that the tail of the authenticated message stays readable to whatever
	/// rents next.
	/// </para>
	/// </remarks>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the tag was written, false if the stream was null or the buffer too small.</returns>
	public async Task<bool> TryHashAsync(ReadOnlyMemory<byte> key, Stream data, Memory<byte> destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination.Length < HashLengthBytes)
		{
			return false;
		}

		using IIncrementalHash hash = CreateIncremental(key.Span);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
		try
		{
			int read;
			while ((read = await data.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
				hash.Append(buffer.AsSpan(0, read));
			}

			return hash.TryGetHashAndReset(destination.Span, out int bytesWritten)
				&& bytesWritten == HashLengthBytes;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
		}
	}

	/// <summary>
	/// Asynchronously computes the authentication tag over a stream, reading it in one pass.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The authentication tag.</returns>
	/// <exception cref="InvalidOperationException">The tag could not be produced.</exception>
	public async Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, Stream data, CancellationToken cancellationToken = default)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !await TryHashAsync(key, data, hash, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException($"Keyed hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

	/// <summary>
	/// Asynchronously computes the authentication tag for the specified data.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The authentication tag.</returns>
	public Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Hash(key.Span, data.Span), cancellationToken);

	/// <summary>
	/// Computes the authentication tag for the specified data.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <returns>The authentication tag.</returns>
	/// <exception cref="InvalidOperationException">The tag could not be produced.</exception>
	public byte[] Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryHash(key, data, hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Keyed hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

	/// <summary>
	/// Computes the authentication tag for the UTF-8 encoding of the specified text.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The text to authenticate.</param>
	/// <returns>The authentication tag.</returns>
	public byte[] Hash(ReadOnlySpan<byte> key, string data)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Hash(key, bytes);
	}

	/// <summary>
	/// Computes the authentication tag over the data in the specified stream.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <returns>The authentication tag.</returns>
	/// <exception cref="InvalidOperationException">The tag could not be produced.</exception>
	public byte[] Hash(ReadOnlySpan<byte> key, Stream data)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryHash(key, data, hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Keyed hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

	/// <summary>
	/// Determines whether the supplied tag is the correct authentication tag for the data.
	/// </summary>
	/// <remarks>
	/// Prefer this to computing a tag and comparing it yourself. The comparison runs in a time that
	/// does not depend on the tag's contents, so it does not leak how much of a forged tag was
	/// correct. A tag of the wrong length is rejected without comparing.
	/// </remarks>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data the tag is claimed to authenticate.</param>
	/// <param name="expected">The tag to check.</param>
	/// <returns>True if the tag is correct for this key and data, false otherwise.</returns>
	public bool Verify(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected)
	{
		if (expected.Length != HashLengthBytes)
		{
			return false;
		}

		byte[] actual = new byte[HashLengthBytes];
		try
		{
			return TryHash(key, data, actual, out int bytesWritten)
				&& bytesWritten == HashLengthBytes
				&& FixedTimeComparison.Equals(actual, expected);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(actual);
		}
	}
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~KeyedHashProviderTests"`

Expected: 16 passed (4 from Task 1, 12 here).

- [ ] **Step 6: Verify every target framework builds**

Run: `dotnet build Essentials/Essentials.csproj`

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. This is the check that no API used is missing on netstandard2.1.

- [ ] **Step 7: Commit**

```bash
git add Essentials/IKeyedHashProvider.cs Essentials/BufferingKeyedIncrementalHash.cs Essentials.Tests/KeyedHashProviderTests.cs
git commit -m "feat: add IKeyedHashProvider with buffering incremental default [patch]"
```

---

### Task 3: Shared HMAC core and the HmacSha256 package

The first real provider. Proves the shared core works and pins the RFC vectors.

**Files:**
- Create: `Shared/HmacKeyedHashCore.cs`
- Create: `Essentials.KeyedHashProviders.HmacSha256/Essentials.KeyedHashProviders.HmacSha256.csproj`
- Create: `Essentials.KeyedHashProviders.HmacSha256/HmacSha256KeyedHashProvider.cs`
- Create: `Essentials.KeyedHashProviders.HmacSha256/ServiceCollectionExtensions.cs`
- Modify: `Essentials.slnx`
- Test: `Essentials.Tests/KeyedHashProviderTests.cs`

**Interfaces:**
- Consumes: `IKeyedHashProvider` from Task 2. `IncrementalHashAdapter(IncrementalHash inner, int hashLengthBytes)` already exists and is public.
- Produces:
  - `internal static class HmacKeyedHashCore` with `TryHash(HashAlgorithmName, int, ReadOnlySpan<byte>, ReadOnlySpan<byte>, Span<byte>, out int)`, `TryHash(HashAlgorithmName, int, ReadOnlySpan<byte>, Stream, Span<byte>, out int)`, and `CreateIncremental(HashAlgorithmName, int, ReadOnlySpan<byte>)`.
  - `public class HmacSha256KeyedHashProvider : IKeyedHashProvider` in `ktsu.Essentials.KeyedHashProviders.HmacSha256`, `HashLengthBytes` of 32.
  - `public static IServiceCollection AddHmacSha256KeyedHashProvider(this IServiceCollection services)`.

- [ ] **Step 1: Write the failing test**

Add to `Essentials.Tests/KeyedHashProviderTests.cs`. Add `using ktsu.Essentials.KeyedHashProviders.HmacSha256;` to the usings.

The vectors are RFC 4231 test cases 1, 2, and 6. Case 6 uses a 131-byte key, longer than the 64-byte block size, so the implementation must hash the key first. That path is invisible to round-trip testing and easy to get wrong.

If one of these tests fails, suspect the vector before the implementation: check it against RFC 4231 section 4 rather than adjusting the code to match.

```csharp
	#region HMAC-SHA256 known answer vectors

	private static byte[] FromHex(string hex)
	{
		byte[] bytes = new byte[hex.Length / 2];
		for (int i = 0; i < bytes.Length; i++)
		{
			bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}

		return bytes;
	}

	[TestMethod]
	public void HmacSha256_Rfc4231_Case1()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Enumerable.Repeat((byte)0x0b, 20).ToArray();
		byte[] data = Encoding.UTF8.GetBytes("Hi There");

		byte[] actual = provider.Hash(key, data);

		CollectionAssert.AreEqual(
			FromHex("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7"),
			actual);
	}

	[TestMethod]
	public void HmacSha256_Rfc4231_Case2()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("Jefe");
		byte[] data = Encoding.UTF8.GetBytes("what do ya want for nothing?");

		byte[] actual = provider.Hash(key, data);

		CollectionAssert.AreEqual(
			FromHex("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843"),
			actual);
	}

	[TestMethod]
	public void HmacSha256_Rfc4231_Case6_Oversized_Key()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Enumerable.Repeat((byte)0xaa, 131).ToArray();
		byte[] data = Encoding.UTF8.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First");

		byte[] actual = provider.Hash(key, data);

		CollectionAssert.AreEqual(
			FromHex("60e431591ee0b67f0d8a26aacbf5b77f8e0bc6213728c5140546040f0ee37f54"),
			actual);
	}

	[TestMethod]
	public void HmacSha256_Agrees_With_Bcl()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("a key of some length");
		byte[] data = Encoding.UTF8.GetBytes("a payload to authenticate");

		byte[] actual = provider.Hash(key, data);

		using HMACSHA256 reference = new(key);
		CollectionAssert.AreEqual(reference.ComputeHash(data), actual);
	}

	[TestMethod]
	public void HmacSha256_All_Four_Paths_Agree()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("agreement key");
		byte[] data = Encoding.UTF8.GetBytes("a payload long enough to span several appends");
		byte[] oneShot = provider.Hash(key, data);

		using MemoryStream stream = new(data);
		byte[] fromStream = provider.Hash(key, stream);

		using IIncrementalHash incremental = provider.CreateIncremental(key);
		incremental.Append(data.AsSpan(0, 7));
		incremental.Append(data.AsSpan(7, 20));
		incremental.Append(data.AsSpan(27));
		byte[] fromIncremental = incremental.GetHashAndReset();

		CollectionAssert.AreEqual(oneShot, fromStream);
		CollectionAssert.AreEqual(oneShot, fromIncremental);
	}

	[TestMethod]
	public async Task HmacSha256_Async_Agrees_With_One_Shot()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("async key");
		byte[] data = Encoding.UTF8.GetBytes("a payload to authenticate asynchronously");
		using MemoryStream stream = new(data);

		byte[] fromAsync = await provider.HashAsync(key, stream);

		CollectionAssert.AreEqual(provider.Hash(key, data), fromAsync);
	}

	[TestMethod]
	public void HmacSha256_Reports_Exact_Length_And_Leaves_Tail_Untouched()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("contract key");
		byte[] data = Encoding.UTF8.GetBytes("contract payload");
		byte[] buffer = new byte[provider.HashLengthBytes + 16];
		buffer.AsSpan().Fill(0xCD);

		Assert.IsTrue(provider.TryHash(key, data, buffer, out int written));

		Assert.AreEqual(provider.HashLengthBytes, written);
		foreach (byte b in buffer.AsSpan(written).ToArray())
		{
			Assert.AreEqual(0xCD, b, "the tail of the caller's buffer must not be touched");
		}
	}

	#endregion
```

Add `using System.Security.Cryptography;` to the file's usings for the BCL comparison test.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~HmacSha256"`

Expected: compile failure, `HmacSha256KeyedHashProvider` does not exist.

- [ ] **Step 3: Write the shared core**

Create `Shared/HmacKeyedHashCore.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// The HMAC implementation shared by the keyed hash providers, parameterized by algorithm.
/// </summary>
/// <remarks>
/// Linked into each provider project rather than placed in the interfaces package, following
/// <c>NonCryptoIncrementalHash</c>. It is internal because every package compiles its own copy, so a
/// public type would collide for a consumer referencing more than one keyed hash package.
/// <para>
/// Key material is copied because <see cref="IncrementalHash.CreateHMAC(HashAlgorithmName, byte[])"/>
/// takes an array on the floor target framework. Every copy is zeroed once the HMAC owns it. Placing
/// that here rather than in each provider means it is written once instead of three times.
/// </para>
/// </remarks>
internal static class HmacKeyedHashCore
{
	/// <summary>
	/// Computes the authentication tag for a span of data.
	/// </summary>
	/// <param name="algorithm">The hash algorithm underlying the HMAC.</param>
	/// <param name="hashLengthBytes">The expected tag length.</param>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written.</param>
	/// <returns>True if the tag was written, false otherwise.</returns>
	internal static bool TryHash(HashAlgorithmName algorithm, int hashLengthBytes, ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < hashLengthBytes)
		{
			return false;
		}

		byte[] keyCopy = key.ToArray();
		try
		{
			using IncrementalHash hash = IncrementalHash.CreateHMAC(algorithm, keyCopy);
			hash.AppendData(data);
			return hash.TryGetHashAndReset(destination, out bytesWritten)
				&& bytesWritten == hashLengthBytes;
		}
		catch (ArgumentException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (CryptographicException)
		{
			bytesWritten = 0;
			return false;
		}
		finally
		{
			CryptographicOperations.ZeroMemory(keyCopy);
		}
	}

	/// <summary>
	/// Computes the authentication tag over a stream, reading it in one pass.
	/// </summary>
	/// <param name="algorithm">The hash algorithm underlying the HMAC.</param>
	/// <param name="hashLengthBytes">The expected tag length.</param>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written.</param>
	/// <returns>True if the tag was written, false otherwise.</returns>
	internal static bool TryHash(HashAlgorithmName algorithm, int hashLengthBytes, ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (data is null || destination.Length < hashLengthBytes)
		{
			return false;
		}

		byte[] keyCopy = key.ToArray();
		byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
		try
		{
			using IncrementalHash hash = IncrementalHash.CreateHMAC(algorithm, keyCopy);
			int read;
			while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
			{
				hash.AppendData(buffer.AsSpan(0, read));
			}

			return hash.TryGetHashAndReset(destination, out bytesWritten)
				&& bytesWritten == hashLengthBytes;
		}
		catch (ArgumentException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (CryptographicException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (IOException)
		{
			bytesWritten = 0;
			return false;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
			CryptographicOperations.ZeroMemory(keyCopy);
		}
	}

	/// <summary>
	/// Creates a genuinely incremental keyed hash.
	/// </summary>
	/// <param name="algorithm">The hash algorithm underlying the HMAC.</param>
	/// <param name="hashLengthBytes">The tag length.</param>
	/// <param name="key">The secret key.</param>
	/// <returns>An incremental hash the caller owns and should dispose.</returns>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership of the IncrementalHash transfers to the returned IncrementalHashAdapter, which disposes it.")]
	internal static IIncrementalHash CreateIncremental(HashAlgorithmName algorithm, int hashLengthBytes, ReadOnlySpan<byte> key)
	{
		byte[] keyCopy = key.ToArray();
		try
		{
			return new IncrementalHashAdapter(
				IncrementalHash.CreateHMAC(algorithm, keyCopy),
				hashLengthBytes);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(keyCopy);
		}
	}
}
```

- [ ] **Step 4: Create the project file**

Create `Essentials.KeyedHashProviders.HmacSha256/Essentials.KeyedHashProviders.HmacSha256.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1</TargetFrameworks>
    <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Essentials\Essentials.csproj" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Polyfill" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="..\Shared\HmacKeyedHashCore.cs" Link="HmacKeyedHashCore.cs" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ktsu.Essentials.Tests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Write the provider**

Create `Essentials.KeyedHashProviders.HmacSha256/HmacSha256KeyedHashProvider.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.KeyedHashProviders.HmacSha256;

using System;
using System.IO;
using System.Security.Cryptography;
using ktsu.Essentials;

/// <summary>
/// A keyed hash provider that uses HMAC-SHA-256 to authenticate data.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads, because the key is supplied per call
/// rather than held in a field. Every operation delegates to the shared HMAC core, which owns key
/// copying and zeroing.
/// </remarks>
public class HmacSha256KeyedHashProvider : IKeyedHashProvider
{
	/// <summary>
	/// The length of the HMAC-SHA-256 tag in bytes (32 bytes / 256 bits).
	/// </summary>
	public int HashLengthBytes => 32;

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA256, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA256, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key)
		=> HmacKeyedHashCore.CreateIncremental(HashAlgorithmName.SHA256, HashLengthBytes, key);
}
```

- [ ] **Step 6: Write the DI registration**

Create `Essentials.KeyedHashProviders.HmacSha256/ServiceCollectionExtensions.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.KeyedHashProviders.HmacSha256;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the HMAC-SHA-256 keyed hashing provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the HMAC-SHA-256 keyed hashing provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IKeyedHashProvider"/> in the resolvable set, so it can be resolved either way. The
	/// container constructs and owns each registration. Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddHmacSha256KeyedHashProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton<HmacSha256KeyedHashProvider>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IKeyedHashProvider, HmacSha256KeyedHashProvider>());
		return services;
	}
}
```

- [ ] **Step 7: Add the project to the solution and the test project**

In `Essentials.slnx`, add alongside the other provider entries, keeping alphabetical order:

```xml
    <Project Path="Essentials.KeyedHashProviders.HmacSha256/Essentials.KeyedHashProviders.HmacSha256.csproj" />
```

In `Essentials.Tests/Essentials.Tests.csproj`, add a `ProjectReference` to the new project, matching how the other provider projects are referenced there.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~KeyedHashProviderTests"`

Expected: 23 passed.

If `HmacSha256_Rfc4231_Case6_Oversized_Key` is the only failure, the key-hashing path is wrong. If cases 1, 2, and 6 all fail but `HmacSha256_Agrees_With_Bcl` passes, the vectors were mis-transcribed, so check RFC 4231 section 4.

- [ ] **Step 9: Verify every target framework builds**

Run: `dotnet build Essentials.KeyedHashProviders.HmacSha256/Essentials.KeyedHashProviders.HmacSha256.csproj`

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 10: Commit**

```bash
git add Shared/HmacKeyedHashCore.cs Essentials.KeyedHashProviders.HmacSha256 Essentials.slnx Essentials.Tests/Essentials.Tests.csproj Essentials.Tests/KeyedHashProviderTests.cs
git commit -m "feat: add HMAC-SHA-256 keyed hash provider over a shared core [patch]"
```

---

### Task 4: HmacSha384 and HmacSha512 packages

Proves the shared core generalizes. Both providers are created together because neither is interesting alone and the review question is the same for both.

**Files:**
- Create: `Essentials.KeyedHashProviders.HmacSha384/` (3 files, mirroring Task 3)
- Create: `Essentials.KeyedHashProviders.HmacSha512/` (3 files, mirroring Task 3)
- Modify: `Essentials.slnx`, `Essentials.Tests/Essentials.Tests.csproj`
- Test: `Essentials.Tests/KeyedHashProviderTests.cs`

**Interfaces:**
- Consumes: `HmacKeyedHashCore` and `IKeyedHashProvider` from Task 3.
- Produces: `HmacSha384KeyedHashProvider` (`HashLengthBytes` 48) and `HmacSha512KeyedHashProvider` (`HashLengthBytes` 64), plus `AddHmacSha384KeyedHashProvider` and `AddHmacSha512KeyedHashProvider`.

- [ ] **Step 1: Write the failing tests**

Add to `Essentials.Tests/KeyedHashProviderTests.cs`, with usings for both new namespaces:

```csharp
	#region HMAC-SHA384 and HMAC-SHA512 known answer vectors

	[TestMethod]
	public void HmacSha384_Rfc4231_Case1()
	{
		HmacSha384KeyedHashProvider provider = new();
		byte[] key = Enumerable.Repeat((byte)0x0b, 20).ToArray();

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Hi There"));

		CollectionAssert.AreEqual(
			FromHex("afd03944d84895626b0825f4ab46907f15f9dadbe4101ec682aa034c7cebc59cfaea9ea9076ede7f4af152e8b2fa9cb6"),
			actual);
	}

	[TestMethod]
	public void HmacSha384_Rfc4231_Case2()
	{
		HmacSha384KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("Jefe");

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("what do ya want for nothing?"));

		CollectionAssert.AreEqual(
			FromHex("af45d2e376484031617f78d2b58a6b1b9c7ef464f5a01b47e42ec3736322445e8e2240ca5e69e2c78b3239ecfab21649"),
			actual);
	}

	[TestMethod]
	public void HmacSha384_Rfc4231_Case6_Oversized_Key()
	{
		HmacSha384KeyedHashProvider provider = new();
		byte[] key = Enumerable.Repeat((byte)0xaa, 131).ToArray();

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First"));

		CollectionAssert.AreEqual(
			FromHex("4ece084485813e9088d2c63a041bc5b44f9ef1012a2b588f3cd11f05033ac4c60c2ef6ab4030fe8296248df163f44952"),
			actual);
	}

	[TestMethod]
	public void HmacSha512_Rfc4231_Case1()
	{
		HmacSha512KeyedHashProvider provider = new();
		byte[] key = Enumerable.Repeat((byte)0x0b, 20).ToArray();

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Hi There"));

		CollectionAssert.AreEqual(
			FromHex("87aa7cdea5ef619d4ff0b4241a1d6cb02379f4e2ce4ec2787ad0b30545e17cdedaa833b7d6b8a702038b274eaea3f4e4be9d914eeb61f1702e696c203a126854"),
			actual);
	}

	[TestMethod]
	public void HmacSha512_Rfc4231_Case2()
	{
		HmacSha512KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("Jefe");

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("what do ya want for nothing?"));

		CollectionAssert.AreEqual(
			FromHex("164b7a7bfcf819e2e395fbe73b56e0a387bd64222e831fd610270cd7ea2505549758bf75c05a994a6d034f65f8f0e6fdcaeab1a34d4a6b4b636e070a38bce737"),
			actual);
	}

	[TestMethod]
	public void HmacSha512_Rfc4231_Case6_Oversized_Key()
	{
		HmacSha512KeyedHashProvider provider = new();
		byte[] key = Enumerable.Repeat((byte)0xaa, 131).ToArray();

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First"));

		CollectionAssert.AreEqual(
			FromHex("80b24263c7c1a3ebb71493c1dd7be8b49b46d1f41b4aeec1121b013783f8f3526b56d037e05f2598bd0fd2215d6a1e5295e64f73f63f0aec8b915a985d786598"),
			actual);
	}

	[TestMethod]
	public void HmacSha384_And_512_Report_Their_Tag_Lengths()
	{
		Assert.AreEqual(48, new HmacSha384KeyedHashProvider().HashLengthBytes);
		Assert.AreEqual(64, new HmacSha512KeyedHashProvider().HashLengthBytes);
	}

	#endregion
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~HmacSha384|FullyQualifiedName~HmacSha512"`

Expected: compile failure, the provider types do not exist.

- [ ] **Step 3: Write the HmacSha384 provider**

Create `Essentials.KeyedHashProviders.HmacSha384/HmacSha384KeyedHashProvider.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.KeyedHashProviders.HmacSha384;

using System;
using System.IO;
using System.Security.Cryptography;
using ktsu.Essentials;

/// <summary>
/// A keyed hash provider that uses HMAC-SHA-384 to authenticate data.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads, because the key is supplied per call
/// rather than held in a field. Every operation delegates to the shared HMAC core, which owns key
/// copying and zeroing.
/// </remarks>
public class HmacSha384KeyedHashProvider : IKeyedHashProvider
{
	/// <summary>
	/// The length of the HMAC-SHA-384 tag in bytes (48 bytes / 384 bits).
	/// </summary>
	public int HashLengthBytes => 48;

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA384, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA384, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key)
		=> HmacKeyedHashCore.CreateIncremental(HashAlgorithmName.SHA384, HashLengthBytes, key);
}
```

Create `Essentials.KeyedHashProviders.HmacSha384/ServiceCollectionExtensions.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.KeyedHashProviders.HmacSha384;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the HMAC-SHA-384 keyed hashing provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the HMAC-SHA-384 keyed hashing provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IKeyedHashProvider"/> in the resolvable set, so it can be resolved either way. The
	/// container constructs and owns each registration. Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddHmacSha384KeyedHashProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton<HmacSha384KeyedHashProvider>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IKeyedHashProvider, HmacSha384KeyedHashProvider>());
		return services;
	}
}
```

- [ ] **Step 4: Write the HmacSha512 provider**

Create `Essentials.KeyedHashProviders.HmacSha512/HmacSha512KeyedHashProvider.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.KeyedHashProviders.HmacSha512;

using System;
using System.IO;
using System.Security.Cryptography;
using ktsu.Essentials;

/// <summary>
/// A keyed hash provider that uses HMAC-SHA-512 to authenticate data.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads, because the key is supplied per call
/// rather than held in a field. Every operation delegates to the shared HMAC core, which owns key
/// copying and zeroing.
/// </remarks>
public class HmacSha512KeyedHashProvider : IKeyedHashProvider
{
	/// <summary>
	/// The length of the HMAC-SHA-512 tag in bytes (64 bytes / 512 bits).
	/// </summary>
	public int HashLengthBytes => 64;

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA512, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA512, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key)
		=> HmacKeyedHashCore.CreateIncremental(HashAlgorithmName.SHA512, HashLengthBytes, key);
}
```

Create `Essentials.KeyedHashProviders.HmacSha512/ServiceCollectionExtensions.cs` as the HmacSha384 one above, replacing `384` with `512` throughout, in the namespace, the class names, the method name `AddHmacSha512KeyedHashProvider`, and the prose `HMAC-SHA-512`.

Create both `.csproj` files as the HmacSha256 one in Task 3 step 4, changing only the two occurrences of the project name in the file path comment and keeping the same `<Compile Include="..\Shared\HmacKeyedHashCore.cs" Link="HmacKeyedHashCore.cs" />` item. The csproj content is otherwise byte-identical, since it names no algorithm.

- [ ] **Step 5: Add both projects to the solution and the test project**

Add to `Essentials.slnx` in alphabetical order:

```xml
    <Project Path="Essentials.KeyedHashProviders.HmacSha384/Essentials.KeyedHashProviders.HmacSha384.csproj" />
    <Project Path="Essentials.KeyedHashProviders.HmacSha512/Essentials.KeyedHashProviders.HmacSha512.csproj" />
```

Add matching `ProjectReference` entries to `Essentials.Tests/Essentials.Tests.csproj`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~KeyedHashProviderTests"`

Expected: 30 passed.

- [ ] **Step 7: Commit**

```bash
git add Essentials.KeyedHashProviders.HmacSha384 Essentials.KeyedHashProviders.HmacSha512 Essentials.slnx Essentials.Tests/Essentials.Tests.csproj Essentials.Tests/KeyedHashProviderTests.cs
git commit -m "feat: add HMAC-SHA-384 and HMAC-SHA-512 keyed hash providers [patch]"
```

---

### Task 5: Essentials.All wiring

Makes the providers reachable from the meta-package and `AddEssentials()`.

**Files:**
- Modify: `Essentials.All/Essentials.All.csproj`
- Modify: `Essentials.All/ServiceCollectionExtensions.cs`
- Test: `Essentials.Tests/KeyedHashProviderTests.cs`

**Interfaces:**
- Consumes: the three `Add…KeyedHashProvider` methods from Tasks 3 and 4.
- Produces: `public static IServiceCollection AddKeyedHashProviders(this IServiceCollection services)`, called from `AddEssentials()`.

- [ ] **Step 1: Write the failing test**

Add to `Essentials.Tests/KeyedHashProviderTests.cs`, with `using ktsu.Essentials.All;` and `using Microsoft.Extensions.DependencyInjection;`:

```csharp
	#region Dependency injection

	[TestMethod]
	public void AddKeyedHashProviders_Registers_All_Three()
	{
		ServiceCollection services = new();
		services.AddKeyedHashProviders();
		using ServiceProvider provider = services.BuildServiceProvider();

		IKeyedHashProvider[] providers = [.. provider.GetServices<IKeyedHashProvider>()];

		Assert.AreEqual(3, providers.Length);
		Assert.AreEqual(1, providers.Count(p => p.HashLengthBytes == 32));
		Assert.AreEqual(1, providers.Count(p => p.HashLengthBytes == 48));
		Assert.AreEqual(1, providers.Count(p => p.HashLengthBytes == 64));
	}

	[TestMethod]
	public void AddKeyedHashProviders_Resolves_Concrete_Types()
	{
		ServiceCollection services = new();
		services.AddKeyedHashProviders();
		using ServiceProvider provider = services.BuildServiceProvider();

		Assert.IsNotNull(provider.GetService<HmacSha256KeyedHashProvider>());
		Assert.IsNotNull(provider.GetService<HmacSha384KeyedHashProvider>());
		Assert.IsNotNull(provider.GetService<HmacSha512KeyedHashProvider>());
	}

	[TestMethod]
	public void AddEssentials_Includes_Keyed_Hash_Providers()
	{
		ServiceCollection services = new();
		services.AddEssentials();
		using ServiceProvider provider = services.BuildServiceProvider();

		Assert.AreEqual(3, provider.GetServices<IKeyedHashProvider>().Count());
	}

	#endregion
```

Add `using System.Linq;` if it is not already present.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~AddKeyedHashProviders|FullyQualifiedName~AddEssentials_Includes"`

Expected: compile failure, `AddKeyedHashProviders` does not exist.

- [ ] **Step 3: Add the project references**

In `Essentials.All/Essentials.All.csproj`, add in alphabetical position among the existing `ProjectReference` items:

```xml
    <ProjectReference Include="..\Essentials.KeyedHashProviders.HmacSha256\Essentials.KeyedHashProviders.HmacSha256.csproj" />
    <ProjectReference Include="..\Essentials.KeyedHashProviders.HmacSha384\Essentials.KeyedHashProviders.HmacSha384.csproj" />
    <ProjectReference Include="..\Essentials.KeyedHashProviders.HmacSha512\Essentials.KeyedHashProviders.HmacSha512.csproj" />
```

- [ ] **Step 4: Add the registration method**

In `Essentials.All/ServiceCollectionExtensions.cs`, add the three namespaces to the usings, then add this method immediately after `AddHashProviders`, matching the surrounding style:

```csharp
	/// <summary>
	/// Registers every keyed hashing provider.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddKeyedHashProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddHmacSha256KeyedHashProvider()
			.AddHmacSha384KeyedHashProvider()
			.AddHmacSha512KeyedHashProvider();
	}
```

In the `AddEssentials` chain, add `.AddKeyedHashProviders()` immediately after `.AddHashProviders()` to keep the chain alphabetical.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: all tests pass, including the pre-existing `DiTests`. If a `DiTests` assertion counts registered providers, update the expected count and note it in the commit message.

- [ ] **Step 6: Commit**

```bash
git add Essentials.All Essentials.Tests/KeyedHashProviderTests.cs
git commit -m "feat: register keyed hash providers in Essentials.All [patch]"
```

---

### Task 6: Document the encryption guarantee

Documentation only. No code in either type changes. This is the half of issue #5 that is about the expectation gap rather than the missing algorithm.

**Files:**
- Modify: `Essentials/IEncryptionProvider.cs`
- Modify: `Essentials.EncryptionProviders.Aes/AesEncryptionProvider.cs`

**Interfaces:**
- Consumes: `IKeyedHashProvider` exists, so the remarks can point at it.
- Produces: nothing.

- [ ] **Step 1: Add remarks to the interface**

In `Essentials/IEncryptionProvider.cs`, replace the existing `<summary>` block above `public interface IEncryptionProvider` with:

```csharp
/// <summary>
/// Interface for encryption providers that can encrypt and decrypt data.
/// </summary>
/// <remarks>
/// Encryption providers give confidentiality only. Ciphertext produced through this interface is not
/// tamper-evident: nothing in the surface carries an authentication tag, so a modified ciphertext is
/// indistinguishable from an unmodified one and decryption of altered input succeeds or fails
/// depending only on whether the result happens to be well-formed.
/// <para>
/// A caller who needs to detect tampering must authenticate the ciphertext separately, computing a
/// tag over it with an <see cref="IKeyedHashProvider"/> and verifying that tag before decrypting.
/// Use a key for authentication that is separate from the encryption key.
/// </para>
/// </remarks>
```

Do not reference an authenticated encryption interface. That type does not exist yet, and pointing at it is worse than not pointing at all. It returns when the AEAD work lands.

- [ ] **Step 2: Add remarks to the AES provider**

In `Essentials.EncryptionProviders.Aes/AesEncryptionProvider.cs`, extend the existing `<remarks>` on the class (which currently covers thread safety) by appending these paragraphs inside the same block:

```csharp
/// <para>
/// This provider is AES in CBC mode with PKCS7 padding, which is what <c>Aes.Create()</c> defaults to.
/// CBC ciphertext is malleable: an attacker who can modify it can make predictable changes to the
/// decrypted plaintext without knowing the key. Decryption reports padding failures, so a caller who
/// decrypts attacker-supplied input and reveals whether it parsed becomes a padding oracle.
/// </para>
/// <para>
/// Authenticate the ciphertext before decrypting it if it crossed a boundary you do not control. See
/// the remarks on <see cref="IEncryptionProvider"/>.
/// </para>
```

Note that the existing remarks block on this type contains an em dash. Leave it alone. It is pre-existing and not part of this change.

- [ ] **Step 3: Verify the build**

Run: `dotnet build Essentials/Essentials.csproj && dotnet build Essentials.EncryptionProviders.Aes/Essentials.EncryptionProviders.Aes.csproj`

Expected: `0 Warning(s) 0 Error(s)` for both. Malformed doc XML fails the build here, so this step is the test.

- [ ] **Step 4: Commit**

```bash
git add Essentials/IEncryptionProvider.cs Essentials.EncryptionProviders.Aes/AesEncryptionProvider.cs
git commit -m "docs: state that encryption providers give confidentiality only [patch]"
```

---

### Task 7: README and CLAUDE.md

The last task, carrying the `[minor]` tag that releases the feature.

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything from Tasks 1 to 6.
- Produces: nothing.

- [ ] **Step 1: Update README.md**

Three edits:

1. In the feature list, reword the encryption bullet to say confidentiality only, and add a keyed hashing bullet after the hashing bullet:

```markdown
- **Keyed Hashing**: `IKeyedHashProvider` with HMAC-SHA256/384/512 implementations for authenticating data, plus `Verify` for fixed-time tag checking
```

2. In the Provider Implementations list, add a `KeyedHashProviders` entry naming the three packages, matching the shape of the `HashProviders` entry.

3. In the API Reference, add an `IKeyedHashProvider` section listing the two required members and the nine defaults, and add a confidentiality-only note to the `IEncryptionProvider` section.

Include a usage example showing the pairing the issue asks for:

```csharp
// Authenticate ciphertext that crossed a boundary you do not control.
byte[] tag = keyedHash.Hash(authenticationKey, ciphertext);

// On the way back in, verify before decrypting.
if (!keyedHash.Verify(authenticationKey, ciphertext, receivedTag))
{
    return false;
}
```

- [ ] **Step 2: Update CLAUDE.md**

- Add `Essentials/IKeyedHashProvider.cs` and `Essentials/FixedTimeComparison.cs` to the Key Files list, each with a one-line description matching the style of the surrounding entries.
- Add `Shared/HmacKeyedHashCore.cs`, describing it as linked into the three keyed hash provider projects rather than placed in the interfaces package.
- Add a **KeyedHashProviders** line to the Provider Implementations list: `HmacSha256, HmacSha384, HmacSha512`.
- Add `KeyedHashProviderTests.cs` to the Testing list.

- [ ] **Step 3: Verify the whole solution builds and every test passes**

Run: `dotnet build Essentials.slnx` then `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` and every test passing. The full solution build takes roughly 10 minutes because of the target framework matrix.

- [ ] **Step 4: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "feat: add keyed hash providers for message authentication [minor]"
```

- [ ] **Step 5: Refresh the generated metadata**

Run the `update-docs` skill to refresh `DESCRIPTION.md` and `TAGS.md` rather than hand-editing them. Commit whatever it changes with a `[patch]` tag.

Never hand-edit `VERSION.md`, `CHANGELOG.md`, or `LICENSE.md`.

- [ ] **Step 6: Open the pull request**

```bash
git push -u origin feat/keyed-hash-provider
```

The pull request body should lead with what a caller can now do that they could not before, name the three packages, and state that the change is purely additive so no major version is needed. Link issue #5.

---

## After the plan

File a follow-up issue for `IAuthenticatedEncryptionProvider` and AES-GCM, carrying across the design already written in Part 2 of `docs/superpowers/specs/2026-08-19-keyed-hashing-and-incremental-hashing-design.md` so that thinking is not lost. Label it `P3`.
