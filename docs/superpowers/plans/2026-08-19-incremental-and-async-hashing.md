# Incremental and Async Hashing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `IHashProvider` an incremental hashing form and a genuinely asynchronous stream overload, so a caller can digest multi-gigabyte data in one pass without buffering it or blocking a thread.

**Architecture:** A new `IIncrementalHash` interface is added to `ktsu.Essentials`. `IHashProvider` gains `CreateIncremental()` with a default body returning a buffering adapter, which keeps the change non-breaking. The async stream overload is then written once in the interface as a real `ReadAsync` loop over `CreateIncremental()`, so all fifteen providers inherit it. Each of the fifteen then overrides `CreateIncremental()` with a genuinely streaming implementation.

**Tech Stack:** C# with default interface methods, MSTest via MSTest.Sdk, `System.Security.Cryptography.IncrementalHash`, `System.IO.Hashing.NonCryptographicHashAlgorithm`, `System.Buffers.ArrayPool<byte>`.

**Spec:** `docs/superpowers/specs/2026-08-19-keyed-hashing-and-incremental-hashing-design.md`

**Resolves:** [#6](https://github.com/ktsu-dev/Essentials/issues/6)

## Global Constraints

- **Target frameworks:** `net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1`. Every API used must exist on netstandard2.1.
- **Verified available on netstandard2.1** (do not add `#if` for these): `IncrementalHash` including `AppendData(ReadOnlySpan<byte>)`, `TryGetHashAndReset(Span<byte>, out int)` and `GetHashAndReset()`; `Stream.Write(ReadOnlySpan<byte>)`; `Stream.ReadAsync(byte[], int, int, CancellationToken)`; `ArrayPool<byte>`.
- **Not available on netstandard2.1:** `IncrementalHash.HashLengthInBytes` (.NET 6+). Never use it — each provider knows its hash length as a constant.
- **This plan introduces zero `#if` directives.** If you think you need one, you have used an API from the line above.
- **Warnings are errors.** A build with any warning fails.
- **Indentation is tabs.** CRLF line endings. File-scoped namespaces. Using directives inside the namespace. Braces on all control flow. Explicit accessibility modifiers. No `this.` qualifiers. Prefer primary constructors.
- **Every file starts with:** `// Copyright (c) 2023-2026 ktsu-dev contributors`
- **No global warning suppressions.** Targeted `[SuppressMessage]` with a justification only if unavoidable.
- **`ktsu.Essentials` must not gain new package dependencies.** It is advertised as interfaces-only. `System.IO.Hashing` is referenced per-provider and must stay that way.
- **Tests use semantic asserts** (`Assert.AreEqual`, `Assert.HasCount`) in preference to `Assert.IsTrue`/`IsFalse`.
- **Do not edit** `VERSION.md`, `CHANGELOG.md`, `LICENSE.md`, or the `.editorconfig`/`.gitignore`/`icon.png` files the SDK rewrites during builds.

### Build and test commands

The test project targets net10.0 only, so project references build for that single framework. This is far faster than a full solution build, which takes roughly 16 minutes across all six frameworks.

```bash
# Run one test
dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run the hashing tests
dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"
```

Before the final commit only, verify all six frameworks compile. Never pipe this through `tail` — the output buffers and you see nothing until it finishes. Redirect and inspect the file:

```bash
dotnet build -c Release > build.log 2>&1; tail -30 build.log
```

---

## File Structure

**New files in `Essentials/` (the interfaces package):**

| File | Responsibility |
| --- | --- |
| `IIncrementalHash.cs` | The public incremental hashing contract |
| `BufferingIncrementalHash.cs` | Internal fallback adapter backing the `CreateIncremental()` default body |
| `IncrementalHashAdapter.cs` | Public adapter over `System.Security.Cryptography.IncrementalHash`, shared by the five cryptographic providers |

`IncrementalHashAdapter` is public and lives in `ktsu.Essentials` because five separate provider packages need it. This follows the existing precedent of `PersistenceProviderUtilities`, which is a `public static class` in the same package shared across the persistence provider packages. It adds no package dependency, since `System.Security.Cryptography` is in-box.

**New shared source file:**

| File | Responsibility |
| --- | --- |
| `Shared/NonCryptoIncrementalHash.cs` | Adapter over `System.IO.Hashing.NonCryptographicHashAlgorithm`, linked into the six providers that use it |

This one is a linked source file rather than a type in `ktsu.Essentials`, because putting it there would force a `System.IO.Hashing` package dependency onto the interfaces-only package. Linking compiles one copy of the source into each of the six assemblies as an internal type. All six algorithm types (`Crc32`, `Crc64`, `XxHash32`, `XxHash64`, `XxHash3`, `XxHash128`) derive from `NonCryptographicHashAlgorithm`, so one adapter serves all of them.

**Modified:**

| File | Change |
| --- | --- |
| `Essentials/IHashProvider.cs` | Add `CreateIncremental()`, `TryHashAsync(Stream, …)`, `HashAsync(Stream, …)` |
| 15 × `Essentials.HashProviders.*/…HashProvider.cs` | Override `CreateIncremental()` |
| 6 × `Essentials.HashProviders.{CRC32,CRC64,XxHash32,XxHash64,XxHash3,XxHash128}/*.csproj` | Link the shared adapter source |
| `README.md`, `CLAUDE.md` | Documentation |

**New test file:** `Essentials.Tests/IncrementalHashTests.cs`

---

## Task 1: The `IIncrementalHash` contract and buffering fallback

**Files:**
- Create: `Essentials/IIncrementalHash.cs`
- Create: `Essentials/BufferingIncrementalHash.cs`
- Modify: `Essentials/IHashProvider.cs`
- Test: `Essentials.Tests/IncrementalHashTests.cs`

**Interfaces:**
- Consumes: `IHashProvider.HashLengthBytes`, `IHashProvider.TryHash(Stream, Span<byte>, out int)` — both already exist.
- Produces: `IIncrementalHash` with `int HashLengthBytes { get; }`, `void Append(ReadOnlySpan<byte> data)`, `bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)`, `byte[] GetHashAndReset()`. Also `IHashProvider.CreateIncremental()` returning `IIncrementalHash`. Every later task depends on these exact names.

At the end of this task every provider has a working `CreateIncremental()` via the buffering fallback, and the contract test passes for all fifteen. Later tasks swap in real streaming implementations without changing the test.

- [ ] **Step 1: Write the failing test**

Create `Essentials.Tests/IncrementalHashTests.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Text;
using ktsu.Essentials;
using ktsu.Essentials.All;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class IncrementalHashTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddHashProviders();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> HashProviders => BuildProvider().EnumerateProviders<IHashProvider>();

	/// <summary>
	/// Chunk boundaries are deliberately uneven and not aligned to any algorithm's block size,
	/// so a provider that mishandles partial blocks or fails to carry state across appends fails here.
	/// </summary>
	private static readonly int[] ChunkSizes = [1, 7, 64, 3, 200, 17];

	private static byte[] BuildPayload()
	{
		byte[] payload = new byte[292];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i * 31 % 251);
		}

		return payload;
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_In_Chunks_Equals_OneShot(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using IIncrementalHash incremental = provider.CreateIncremental();
		int offset = 0;
		int chunkIndex = 0;
		while (offset < payload.Length)
		{
			int size = Math.Min(ChunkSizes[chunkIndex++ % ChunkSizes.Length], payload.Length - offset);
			incremental.Append(payload.AsSpan(offset, size));
			offset += size;
		}

		byte[] incrementalHash = incremental.GetHashAndReset();
		byte[] oneShotHash = provider.Hash(payload);

		Assert.AreEqual(
			Convert.ToHexString(oneShotHash),
			Convert.ToHexString(incrementalHash),
			$"{providerName} incremental hash should equal its one-shot hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_Reports_Provider_HashLength(IHashProvider provider, string providerName)
	{
		using IIncrementalHash incremental = provider.CreateIncremental();
		Assert.AreEqual(provider.HashLengthBytes, incremental.HashLengthBytes, $"{providerName} length mismatch");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_Empty_Input_Equals_OneShot(IHashProvider provider, string providerName)
	{
		using IIncrementalHash incremental = provider.CreateIncremental();
		byte[] incrementalHash = incremental.GetHashAndReset();

		Assert.AreEqual(
			Convert.ToHexString(provider.Hash(Array.Empty<byte>())),
			Convert.ToHexString(incrementalHash),
			$"{providerName} incremental empty hash should equal its one-shot empty hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_Resets_Between_Uses(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using IIncrementalHash incremental = provider.CreateIncremental();
		incremental.Append(payload);
		byte[] first = incremental.GetHashAndReset();

		incremental.Append(payload);
		byte[] second = incremental.GetHashAndReset();

		Assert.AreEqual(
			Convert.ToHexString(first),
			Convert.ToHexString(second),
			$"{providerName} should produce the same hash after a reset");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_TryGetHashAndReset_Rejects_Small_Buffer(IHashProvider provider, string providerName)
	{
		using IIncrementalHash incremental = provider.CreateIncremental();
		incremental.Append("data"u8);

		byte[] tooSmall = new byte[Math.Max(1, provider.HashLengthBytes - 1)];
		bool ok = incremental.TryGetHashAndReset(tooSmall, out int bytesWritten);

		Assert.IsFalse(ok, $"{providerName} should reject an undersized destination");
		Assert.AreEqual(0, bytesWritten, $"{providerName} should report no bytes written on failure");
	}
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"`

Expected: compile failure — `'IHashProvider' does not contain a definition for 'CreateIncremental'` and `The type or namespace name 'IIncrementalHash' could not be found`.

- [ ] **Step 3: Create the interface**

Create `Essentials/IIncrementalHash.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// A hash computation that accepts data in successive chunks, so a caller can digest bytes it is
/// already moving for another reason instead of handing over a stream to be read.
/// </summary>
/// <remarks>
/// Obtained from <see cref="IHashProvider.CreateIncremental"/>. Instances are stateful and are not
/// safe to share across threads. Dispose when finished.
/// </remarks>
public interface IIncrementalHash : IDisposable
{
	/// <summary>
	/// The length of the hash in bytes.
	/// </summary>
	public int HashLengthBytes { get; }

	/// <summary>
	/// Appends data to the running hash.
	/// </summary>
	/// <param name="data">The data to append.</param>
	public void Append(ReadOnlySpan<byte> data);

	/// <summary>
	/// Tries to write the hash of everything appended so far into <paramref name="destination"/>,
	/// then resets so the instance can be reused.
	/// </summary>
	/// <param name="destination">The buffer to write the hash to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the hash was written, false if <paramref name="destination"/> was too small.</returns>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Returns the hash of everything appended so far, then resets so the instance can be reused.
	/// </summary>
	/// <returns>A byte array containing the hash.</returns>
	/// <exception cref="InvalidOperationException">The hash could not be produced.</exception>
	public byte[] GetHashAndReset()
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryGetHashAndReset(hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}
}
```

- [ ] **Step 4: Create the buffering fallback**

Create `Essentials/BufferingIncrementalHash.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// An <see cref="IIncrementalHash"/> that accumulates every appended byte and hashes the result in
/// one pass, for providers that do not supply a genuinely incremental implementation.
/// </summary>
/// <remarks>
/// Correct for any provider, but it holds the whole input in memory, which is the cost this feature
/// exists to avoid. It backs the default body of <see cref="IHashProvider.CreateIncremental"/> so that
/// adding the member breaks no existing implementer; providers are expected to override it.
/// </remarks>
/// <param name="provider">The provider whose one-shot stream hashing is used to produce the digest.</param>
internal sealed class BufferingIncrementalHash(IHashProvider provider) : IIncrementalHash
{
	private readonly MemoryStream buffer = new();

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
		bool hashed = provider.TryHash(buffer, destination, out bytesWritten);
		buffer.SetLength(0);
		return hashed;
	}

	/// <inheritdoc/>
	public void Dispose() => buffer.Dispose();
}
```

- [ ] **Step 5: Add `CreateIncremental()` to `IHashProvider`**

In `Essentials/IHashProvider.cs`, add this member immediately after the `TryHash(Stream, …)` declaration:

```csharp
	/// <summary>
	/// Creates an incremental hash that accepts data in successive chunks.
	/// </summary>
	/// <remarks>
	/// The default implementation accumulates every appended byte in memory and hashes it in one pass
	/// when the digest is requested. That is correct but it buffers the entire input, so implementers
	/// should override this with a genuinely incremental implementation. Doing so also makes
	/// <see cref="TryHashAsync(Stream, Memory{byte}, CancellationToken)"/> stream properly, because
	/// that method is built on this one.
	/// </remarks>
	/// <returns>A new incremental hash. The caller owns it and should dispose it.</returns>
	public IIncrementalHash CreateIncremental() => new BufferingIncrementalHash(this);
```

The `<see cref>` above refers to a method added in Task 2. Until that task lands the XML reference will not resolve and the build will fail, so for this task write the remarks sentence as plain text without the `<see cref>` tag, and restore the tag in Task 2 Step 5.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"`

Expected: PASS, 75 test cases (5 test methods × 15 providers).

- [ ] **Step 7: Commit**

```bash
git add Essentials/IIncrementalHash.cs Essentials/BufferingIncrementalHash.cs Essentials/IHashProvider.cs Essentials.Tests/IncrementalHashTests.cs
git commit -m "[minor] Add IIncrementalHash and IHashProvider.CreateIncremental

Adds the incremental hashing contract with a buffering default body, so
the new member breaks no existing implementer. Providers override it with
genuinely streaming implementations in later commits.

Part of #6."
```

---

## Task 2: Genuinely asynchronous stream hashing

**Files:**
- Modify: `Essentials/IHashProvider.cs`
- Test: `Essentials.Tests/IncrementalHashTests.cs`

**Interfaces:**
- Consumes: `IHashProvider.CreateIncremental()` and `IIncrementalHash` from Task 1.
- Produces: `Task<bool> TryHashAsync(Stream data, Memory<byte> destination, CancellationToken cancellationToken = default)` and `Task<byte[]> HashAsync(Stream data, CancellationToken cancellationToken = default)` on `IHashProvider`.

This must be a real `ReadAsync` loop, **not** `ProviderHelpers.RunAsync`. Every other async method in this library is a `Task.Run` wrapper over synchronous work, which does not solve the problem issue #6 describes — it relocates the blocked thread to the pool rather than freeing it. See issue #8 for the general case.

- [ ] **Step 1: Write the failing test**

Add to `Essentials.Tests/IncrementalHashTests.cs`, inside the class:

```csharp
	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task HashAsync_Stream_Equals_OneShot(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using MemoryStream stream = new(payload);
		byte[] asyncHash = await provider.HashAsync(stream, TestContext.CancellationTokenSource.Token);

		Assert.AreEqual(
			Convert.ToHexString(provider.Hash(payload)),
			Convert.ToHexString(asyncHash),
			$"{providerName} async stream hash should equal its one-shot hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Reports_Success_And_Fills_Destination(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using MemoryStream stream = new(payload);
		byte[] destination = new byte[provider.HashLengthBytes];
		bool ok = await provider.TryHashAsync(stream, destination, TestContext.CancellationTokenSource.Token);

		Assert.IsTrue(ok, $"{providerName} should hash the stream successfully");
		Assert.AreEqual(
			Convert.ToHexString(provider.Hash(payload)),
			Convert.ToHexString(destination),
			$"{providerName} async destination should hold the correct hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Rejects_Small_Destination(IHashProvider provider, string providerName)
	{
		using MemoryStream stream = new(BuildPayload());
		byte[] tooSmall = new byte[Math.Max(1, provider.HashLengthBytes - 1)];

		bool ok = await provider.TryHashAsync(stream, tooSmall, TestContext.CancellationTokenSource.Token);

		Assert.IsFalse(ok, $"{providerName} should reject an undersized destination");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Honours_Cancellation(IHashProvider provider, string providerName)
	{
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		using MemoryStream stream = new(BuildPayload());
		byte[] destination = new byte[provider.HashLengthBytes];

		// Caught by base type on purpose: the framework may surface either
		// OperationCanceledException or its TaskCanceledException subclass, and which one
		// depends on the stream implementation. An exact-type assertion would be brittle here.
		bool cancelled = false;
		try
		{
			await provider.TryHashAsync(stream, destination, cts.Token);
		}
		catch (OperationCanceledException)
		{
			cancelled = true;
		}

		Assert.IsTrue(cancelled, $"{providerName} should surface cancellation");
	}
```

Add `using System.Threading;` and `using System.Threading.Tasks;` to the file's using block. The class as written in Task 1 has no `TestContext` property, so add one now — the tests above use it for the ambient cancellation token:

```csharp
	public TestContext TestContext { get; set; } = null!;
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"`

Expected: compile failure — `'IHashProvider' does not contain a definition for 'HashAsync'` taking a `Stream`, and no definition for `TryHashAsync`.

- [ ] **Step 3: Add `System.Buffers` to the using block**

`System.Buffers` is not among the implicit usings. At the top of `Essentials/IHashProvider.cs`, inside the namespace, the using block becomes:

```csharp
using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
```

- [ ] **Step 4: Implement the async stream overloads**

In `Essentials/IHashProvider.cs`, add both members after `CreateIncremental()`:

```csharp
	/// <summary>
	/// Asynchronously hashes a stream into the provided buffer, reading it in one pass.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: the stream is read with <see cref="Stream.ReadAsync(byte[], int, int, CancellationToken)"/>
	/// and no thread is held for the duration. The result is not reported through an <c>out</c> parameter
	/// because one cannot cross an await boundary; a return value of true guarantees exactly
	/// <see cref="HashLengthBytes"/> bytes were written.
	/// </remarks>
	/// <param name="data">The stream to hash.</param>
	/// <param name="destination">The buffer to write the hash to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the hash was written, false if the stream was null or the buffer too small.</returns>
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

	/// <summary>
	/// Asynchronously hashes a stream, reading it in one pass.
	/// </summary>
	/// <param name="data">The stream to hash.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A byte array containing the hash of the stream.</returns>
	/// <exception cref="InvalidOperationException">The hash could not be produced.</exception>
	public async Task<byte[]> HashAsync(Stream data, CancellationToken cancellationToken = default)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !await TryHashAsync(data, hash, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException($"Hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}
```

`destination.Span` is accessed after every await, in a single expression, so no span local crosses an await boundary.

- [ ] **Step 5: Restore the cross-reference in `CreateIncremental`**

Now that `TryHashAsync` exists, restore the XML reference deferred in Task 1 Step 5, so the remarks read:

```csharp
	/// should override this with a genuinely incremental implementation. Doing so also makes
	/// <see cref="TryHashAsync(Stream, Memory{byte}, CancellationToken)"/> stream properly, because
	/// that method is built on this one.
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"`

Expected: PASS, 135 test cases (9 test methods × 15 providers).

- [ ] **Step 7: Commit**

```bash
git add Essentials/IHashProvider.cs Essentials.Tests/IncrementalHashTests.cs
git commit -m "Add genuinely async stream hashing to IHashProvider

TryHashAsync and HashAsync read the stream with ReadAsync over the
incremental hash rather than wrapping synchronous work in Task.Run, so no
thread is held for the duration of the read.

Part of #6."
```

---

## Task 3: Streaming overrides for the five cryptographic providers

**Files:**
- Create: `Essentials/IncrementalHashAdapter.cs`
- Modify: `Essentials.HashProviders.MD5/MD5HashProvider.cs`
- Modify: `Essentials.HashProviders.SHA1/SHA1HashProvider.cs`
- Modify: `Essentials.HashProviders.SHA256/SHA256HashProvider.cs`
- Modify: `Essentials.HashProviders.SHA384/SHA384HashProvider.cs`
- Modify: `Essentials.HashProviders.SHA512/SHA512HashProvider.cs`

**Interfaces:**
- Consumes: `IIncrementalHash` from Task 1.
- Produces: `public sealed class IncrementalHashAdapter : IIncrementalHash` in namespace `ktsu.Essentials`, with constructor `IncrementalHashAdapter(IncrementalHash inner, int hashLengthBytes)`.

The existing contract tests from Tasks 1 and 2 already cover these providers and must keep passing — they are what proves the streaming implementation agrees with the one-shot implementation.

- [ ] **Step 1: Create the adapter**

Create `Essentials/IncrementalHashAdapter.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Security.Cryptography;

/// <summary>
/// An <see cref="IIncrementalHash"/> backed by <see cref="IncrementalHash"/>, for providers built on
/// <see cref="System.Security.Cryptography"/>.
/// </summary>
/// <remarks>
/// Public because the cryptographic hash providers each ship as their own package and all need it.
/// The hash length is supplied by the caller rather than read from
/// <see cref="IncrementalHash"/>, whose <c>HashLengthInBytes</c> property does not exist on
/// netstandard2.1.
/// </remarks>
/// <param name="inner">The underlying incremental hash. This instance takes ownership and disposes it.</param>
/// <param name="hashLengthBytes">The length of the hash in bytes.</param>
public sealed class IncrementalHashAdapter(IncrementalHash inner, int hashLengthBytes) : IIncrementalHash
{
	/// <inheritdoc/>
	public int HashLengthBytes => hashLengthBytes;

	/// <inheritdoc/>
	public void Append(ReadOnlySpan<byte> data) => inner.AppendData(data);

	/// <inheritdoc/>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		return destination.Length >= hashLengthBytes
			&& inner.TryGetHashAndReset(destination, out bytesWritten);
	}

	/// <inheritdoc/>
	public void Dispose() => inner.Dispose();
}
```

- [ ] **Step 2: Override `CreateIncremental` in all five providers**

Each provider gets the same member, differing only in the algorithm name and length. Add to each class, after its `TryHash(Stream, …)` method.

For `Essentials.HashProviders.SHA256/SHA256HashProvider.cs`:

```csharp
	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental()
		=> new IncrementalHashAdapter(
			IncrementalHash.CreateHash(HashAlgorithmName.SHA256),
			HashLengthBytes);
```

Apply the identical member to the other four, substituting from this table:

| File | Class | `HashAlgorithmName` | `HashLengthBytes` |
| --- | --- | --- | --- |
| `Essentials.HashProviders.MD5/MD5HashProvider.cs` | `MD5HashProvider` | `HashAlgorithmName.MD5` | 16 |
| `Essentials.HashProviders.SHA1/SHA1HashProvider.cs` | `SHA1HashProvider` | `HashAlgorithmName.SHA1` | 20 |
| `Essentials.HashProviders.SHA256/SHA256HashProvider.cs` | `SHA256HashProvider` | `HashAlgorithmName.SHA256` | 32 |
| `Essentials.HashProviders.SHA384/SHA384HashProvider.cs` | `SHA384HashProvider` | `HashAlgorithmName.SHA384` | 48 |
| `Essentials.HashProviders.SHA512/SHA512HashProvider.cs` | `SHA512HashProvider` | `HashAlgorithmName.SHA512` | 64 |

Use the `HashLengthBytes` property in the code rather than the literal; the literal column is listed only so you can confirm each provider's existing property is what you expect.

Each of these five files already has `using System.Security.Cryptography;`. Verify it is present before assuming, and confirm `IncrementalHash` resolves — these files alias the algorithm types (for example `System.Security.Cryptography.SHA256`) to dodge a name clash with their own namespace, so check that `IncrementalHash` and `HashAlgorithmName` are not shadowed. If the namespace clash bites, fully qualify as `System.Security.Cryptography.IncrementalHash` and `System.Security.Cryptography.HashAlgorithmName`.

- [ ] **Step 3: Run the tests to verify they still pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"`

Expected: PASS, 135 test cases. The five cryptographic providers now take the streaming path; the test asserting incremental equals one-shot is what proves they agree.

- [ ] **Step 4: Commit**

```bash
git add Essentials/IncrementalHashAdapter.cs Essentials.HashProviders.MD5 Essentials.HashProviders.SHA1 Essentials.HashProviders.SHA256 Essentials.HashProviders.SHA384 Essentials.HashProviders.SHA512
git commit -m "Add streaming incremental hashing to the cryptographic providers

MD5, SHA1, SHA256, SHA384 and SHA512 now delegate to IncrementalHash
instead of inheriting the buffering fallback.

Part of #6."
```

---

## Task 4: Streaming overrides for the six `System.IO.Hashing` providers

**Files:**
- Create: `Shared/NonCryptoIncrementalHash.cs`
- Modify: `Essentials.HashProviders.CRC32/Essentials.HashProviders.CRC32.csproj` and `CRC32HashProvider.cs`
- Modify: the same pair in `CRC64`, `XxHash32`, `XxHash64`, `XxHash3`, `XxHash128`

**Interfaces:**
- Consumes: `IIncrementalHash` from Task 1.
- Produces: `internal sealed class NonCryptoIncrementalHash : IIncrementalHash` in namespace `ktsu.Essentials`, constructor `NonCryptoIncrementalHash(NonCryptographicHashAlgorithm inner, int hashLengthBytes)`. Compiled separately into each of the six assemblies.

- [ ] **Step 1: Create the shared source file**

Create `Shared/NonCryptoIncrementalHash.cs` at the repository root:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO.Hashing;

/// <summary>
/// An <see cref="IIncrementalHash"/> backed by a <see cref="NonCryptographicHashAlgorithm"/>.
/// </summary>
/// <remarks>
/// This file is linked into each non-cryptographic hash provider project rather than living in
/// ktsu.Essentials, because that package is interfaces-only and must not take a dependency on
/// System.IO.Hashing. Every algorithm it serves — Crc32, Crc64, XxHash32, XxHash64, XxHash3 and
/// XxHash128 — derives from NonCryptographicHashAlgorithm, so one adapter covers all six.
/// </remarks>
/// <param name="inner">The underlying algorithm instance.</param>
/// <param name="hashLengthBytes">The length of the hash in bytes.</param>
internal sealed class NonCryptoIncrementalHash(NonCryptographicHashAlgorithm inner, int hashLengthBytes) : IIncrementalHash
{
	/// <inheritdoc/>
	public int HashLengthBytes => hashLengthBytes;

	/// <inheritdoc/>
	public void Append(ReadOnlySpan<byte> data) => inner.Append(data);

	/// <inheritdoc/>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		return destination.Length >= hashLengthBytes
			&& inner.TryGetHashAndReset(destination, out bytesWritten);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// NonCryptographicHashAlgorithm holds no unmanaged state and is not IDisposable.
	}
}
```

- [ ] **Step 2: Link the shared file into all six projects**

Add this `ItemGroup` to each of the six `.csproj` files, for example `Essentials.HashProviders.CRC32/Essentials.HashProviders.CRC32.csproj`:

```xml
  <ItemGroup>
    <Compile Include="..\Shared\NonCryptoIncrementalHash.cs" Link="NonCryptoIncrementalHash.cs" />
  </ItemGroup>
```

Apply to: `Essentials.HashProviders.CRC32`, `Essentials.HashProviders.CRC64`, `Essentials.HashProviders.XxHash32`, `Essentials.HashProviders.XxHash64`, `Essentials.HashProviders.XxHash3`, `Essentials.HashProviders.XxHash128`.

- [ ] **Step 3: Override `CreateIncremental` in all six providers**

For `Essentials.HashProviders.CRC32/CRC32HashProvider.cs`:

```csharp
	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental() => new NonCryptoIncrementalHash(new Crc32(), HashLengthBytes);
```

Apply the identical member to the other five, substituting the algorithm type:

| File | Class | Algorithm type to construct |
| --- | --- | --- |
| `Essentials.HashProviders.CRC32/CRC32HashProvider.cs` | `CRC32HashProvider` | `new Crc32()` |
| `Essentials.HashProviders.CRC64/CRC64HashProvider.cs` | `CRC64HashProvider` | `new Crc64()` |
| `Essentials.HashProviders.XxHash32/XxHash32HashProvider.cs` | `XxHash32HashProvider` | `new XxHash32()` |
| `Essentials.HashProviders.XxHash64/XxHash64HashProvider.cs` | `XxHash64HashProvider` | `new XxHash64()` |
| `Essentials.HashProviders.XxHash3/XxHash3HashProvider.cs` | `XxHash3HashProvider` | `new SysXxHash3()` |
| `Essentials.HashProviders.XxHash128/XxHash128HashProvider.cs` | `XxHash128HashProvider` | `new XxHash128()` |

`XxHash3HashProvider` aliases the algorithm as `SysXxHash3` at the top of its file to avoid clashing with its own namespace, so use the alias there. Check whether `XxHash128HashProvider` does the same and follow whatever alias that file already establishes.

- [ ] **Step 4: Run the tests to verify they still pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"`

Expected: PASS, 135 test cases.

This step matters more than it looks. These six providers use the static one-shot API on the span path (`Crc32.TryHash`) and the instance API on the stream path. `Incremental_In_Chunks_Equals_OneShot` is what proves the two agree on byte order. If it fails here, the mismatch is real and pre-existing — investigate rather than adjusting the test.

- [ ] **Step 5: Commit**

```bash
git add Shared/NonCryptoIncrementalHash.cs Essentials.HashProviders.CRC32 Essentials.HashProviders.CRC64 Essentials.HashProviders.XxHash32 Essentials.HashProviders.XxHash64 Essentials.HashProviders.XxHash3 Essentials.HashProviders.XxHash128
git commit -m "Add streaming incremental hashing to the non-cryptographic providers

CRC32, CRC64 and the xxHash family delegate to their underlying
NonCryptographicHashAlgorithm through a shared linked adapter, which keeps
System.IO.Hashing out of the interfaces-only package.

Part of #6."
```

---

## Task 5: Streaming overrides for the four FNV providers

**Files:**
- Modify: `Essentials.HashProviders.FNV1_32/FNV1_32HashProvider.cs`
- Modify: `Essentials.HashProviders.FNV1a_32/FNV1a_32HashProvider.cs`
- Modify: `Essentials.HashProviders.FNV1_64/FNV1_64HashProvider.cs`
- Modify: `Essentials.HashProviders.FNV1a_64/FNV1a_64HashProvider.cs`

**Interfaces:**
- Consumes: `IIncrementalHash` from Task 1.
- Produces: a private nested `sealed class Incremental : IIncrementalHash` inside each of the four providers. Nested and private because each carries algorithm-specific state and constants, and nothing outside the provider needs it.

FNV-1 and FNV-1a differ only in operation order, confirmed against the existing code: **FNV-1 multiplies then XORs; FNV-1a XORs then multiplies.** Getting this backwards produces a plausible-looking hash that fails `Incremental_In_Chunks_Equals_OneShot`.

Constants, also confirmed against the existing code:

| Width | Offset basis | Prime |
| --- | --- | --- |
| 32-bit | `0x811c9dc5` | `0x01000193` |
| 64-bit | `0xcbf29ce484222325` | `0x00000100000001b3` |

- [ ] **Step 1: Add the nested incremental class to `FNV1a_32HashProvider`**

In `Essentials.HashProviders.FNV1a_32/FNV1a_32HashProvider.cs`, add inside the class:

```csharp
	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental() => new Incremental();

	/// <summary>
	/// The running FNV-1a 32-bit state. Naturally incremental: the accumulator is the entire state.
	/// </summary>
	private sealed class Incremental : IIncrementalHash
	{
		private uint hash = FnvOffsetBasis32;

		public int HashLengthBytes => 4;

		public void Append(ReadOnlySpan<byte> data)
		{
			foreach (byte b in data)
			{
				hash ^= b;
				hash *= FnvPrime32;
			}
		}

		public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
		{
			bytesWritten = 0;

			if (destination.Length < HashLengthBytes)
			{
				return false;
			}

			// Little-endian, matching the one-shot implementation above.
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);

			bytesWritten = HashLengthBytes;
			hash = FnvOffsetBasis32;
			return true;
		}

		public void Dispose()
		{
			// No unmanaged state.
		}
	}
```

- [ ] **Step 2: Add the equivalent to `FNV1_32HashProvider`**

Copy the whole `CreateIncremental()` member and nested `Incremental` class from Step 1 into `Essentials.HashProviders.FNV1_32/FNV1_32HashProvider.cs` verbatim, then change only the `Append` body to reverse the operation order, and the doc comment to say FNV-1 rather than FNV-1a:

```csharp
		public void Append(ReadOnlySpan<byte> data)
		{
			foreach (byte b in data)
			{
				hash *= FnvPrime32;
				hash ^= b;
			}
		}
```

- [ ] **Step 3: Add the equivalent to `FNV1a_64HashProvider`**

Same structure, 64-bit. Note the eight-byte little-endian write and the `ulong` accumulator:

```csharp
	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental() => new Incremental();

	/// <summary>
	/// The running FNV-1a 64-bit state. Naturally incremental: the accumulator is the entire state.
	/// </summary>
	private sealed class Incremental : IIncrementalHash
	{
		private ulong hash = FnvOffsetBasis64;

		public int HashLengthBytes => 8;

		public void Append(ReadOnlySpan<byte> data)
		{
			foreach (byte b in data)
			{
				hash ^= b;
				hash *= FnvPrime64;
			}
		}

		public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
		{
			bytesWritten = 0;

			if (destination.Length < HashLengthBytes)
			{
				return false;
			}

			// Little-endian, matching the one-shot implementation above.
			for (int i = 0; i < HashLengthBytes; i++)
			{
				destination[i] = (byte)((hash >> (i * 8)) & 0xFF);
			}

			bytesWritten = HashLengthBytes;
			hash = FnvOffsetBasis64;
			return true;
		}

		public void Dispose()
		{
			// No unmanaged state.
		}
	}
```

Before writing the byte-order loop, read the existing `TryHash` in that file and match exactly how it writes its bytes. If it writes each byte explicitly rather than in a loop, mirror that. The loop above is correct little-endian, but the test compares against the one-shot output, so the one-shot output is the authority.

- [ ] **Step 4: Add the equivalent to `FNV1_64HashProvider`**

Copy the whole `CreateIncremental()` member and nested `Incremental` class from Step 3 into `Essentials.HashProviders.FNV1_64/FNV1_64HashProvider.cs` verbatim, then change only the `Append` body to reverse the operation order, and the doc comment to say FNV-1 rather than FNV-1a:

```csharp
		public void Append(ReadOnlySpan<byte> data)
		{
			foreach (byte b in data)
			{
				hash *= FnvPrime64;
				hash ^= b;
			}
		}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~IncrementalHashTests"`

Expected: PASS, 135 test cases. All fifteen providers now use a genuinely streaming implementation and none fall back to buffering.

- [ ] **Step 6: Verify the whole suite still passes**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: PASS. The pre-existing `HashProviderTests` must be unaffected.

- [ ] **Step 7: Commit**

```bash
git add Essentials.HashProviders.FNV1_32 Essentials.HashProviders.FNV1a_32 Essentials.HashProviders.FNV1_64 Essentials.HashProviders.FNV1a_64
git commit -m "Add streaming incremental hashing to the FNV providers

All fifteen hash providers now implement CreateIncremental directly and
none rely on the buffering fallback.

Part of #6."
```

---

## Task 6: Documentation

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: every member added in Tasks 1 through 5.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Correct the async claim**

`README.md` line 35 currently reads:

```markdown
- **Comprehensive Async Support**: Every operation has async variants with proper `CancellationToken` support
```

This is the overclaim issue #6 names, and it stays false even after this work: span-destination async overloads cannot exist because an `out` parameter cannot cross an await. Replace with:

```markdown
- **Async Support**: Operations expose async variants with `CancellationToken` support. Stream hashing is genuinely asynchronous — it reads with `ReadAsync` and holds no thread. Most other async variants are convenience wrappers that run synchronous work on the thread pool; span-destination operations have no async form, because an `out` parameter cannot cross an await boundary
```

- [ ] **Step 2: Add the incremental hashing usage example**

In `README.md`, after the existing hashing example that ends with `byte[] asyncHash = await hashProvider.HashAsync("Hello, World!");`, add inside the same fenced block:

```csharp
// Async stream hashing — one pass, no thread held, nothing buffered
using FileStream file = File.OpenRead("large-object.bin");
byte[] streamHash = await hashProvider.HashAsync(file);

// Incremental — digest bytes you are already moving for another reason
using IIncrementalHash incremental = hashProvider.CreateIncremental();
await foreach (ReadOnlyMemory<byte> chunk in source)
{
    incremental.Append(chunk.Span);
    await destination.WriteAsync(chunk);
}

byte[] digest = incremental.GetHashAndReset();
```

- [ ] **Step 3: Update the custom provider example**

In `README.md`, the custom provider example ends with the comment `// Hash(), HashAsync(), string overloads — all inherited`. Extend it so implementers learn about the buffering trap:

```csharp
    // Hash(), HashAsync(), string overloads — all inherited

    // Override this. The inherited default buffers the whole input in memory,
    // and TryHashAsync(Stream, ...) is built on it.
    public IIncrementalHash CreateIncremental() => new MyIncrementalHash();
}
```

- [ ] **Step 4: Add the API reference rows**

In `README.md`, in the `### IHashProvider` table, add:

```markdown
| `CreateIncremental()` | `IIncrementalHash` | Create an incremental hash for chunk-by-chunk digesting |
| `TryHashAsync(Stream, Memory<byte>, CancellationToken)` | `Task<bool>` | Genuinely async stream hashing into a caller-owned buffer |
| `HashAsync(Stream, CancellationToken)` | `Task<byte[]>` | Genuinely async self-allocating stream hashing |
```

Then add a new section immediately after the `IHashProvider` table:

```markdown
### `IIncrementalHash`

A hash computation that accepts data in successive chunks. Obtained from `IHashProvider.CreateIncremental()`. Stateful, not thread-safe, and disposable.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `HashLengthBytes` | `int` | Length of the hash in bytes |
| `Append(ReadOnlySpan<byte>)` | `void` | Append data to the running hash |
| `TryGetHashAndReset(Span<byte>, out int)` | `bool` | Write the hash and reset, reporting bytes written |
| `GetHashAndReset()` | `byte[]` | Self-allocating variant of the above |
```

- [ ] **Step 5: Update `CLAUDE.md`**

Add to the Key Files list, after the `IHashProvider.cs` entry:

```markdown
- `Essentials/IIncrementalHash.cs` - Chunk-by-chunk hashing contract; `IHashProvider.CreateIncremental()` returns one
- `Essentials/IncrementalHashAdapter.cs` - Public adapter over `System.Security.Cryptography.IncrementalHash`, shared by the cryptographic hash providers
- `Essentials/BufferingIncrementalHash.cs` - Internal buffering fallback behind the `CreateIncremental()` default body
- `Shared/NonCryptoIncrementalHash.cs` - Adapter over `NonCryptographicHashAlgorithm`, linked into the six `System.IO.Hashing` providers rather than placed in the interfaces-only package
```

In the Architecture section, the third bullet currently claims all async variants are `Task.Run` wrappers. Amend it to note the exception:

```markdown
3. **Async variants**: Task-based async versions with `CancellationToken` support, mostly provided via `ProviderHelpers.RunAsync()`. Note these are `Task.Run` wrappers over synchronous work, not genuine async I/O — the exception is `IHashProvider.TryHashAsync(Stream, ...)`, which is a real `ReadAsync` loop. See issue #8. Span-destination async overloads do not exist — an `out` parameter cannot cross an async boundary.
```

Add to the Testing section's test file list:

```markdown
- `IncrementalHashTests.cs` - Tests `CreateIncremental()` and async stream hashing across all 15 hash providers, asserting incremental output equals one-shot output
```

- [ ] **Step 6: Check `DESCRIPTION.md` and `TAGS.md`**

The spec calls for these to be refreshed via the `update-docs` skill rather than hand-edited, so they match house style. This change adds no provider category and no new package, so they may well need nothing.

Read both files. If neither mentions hashing in a way this change makes inaccurate, leave them and note that in the commit. If either needs updating, invoke the `update-docs` skill rather than editing by hand.

- [ ] **Step 7: Verify all six target frameworks build clean**

Run:

```bash
dotnet build -c Release > build.log 2>&1; tail -30 build.log
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. This takes roughly 16 minutes. Do not pipe it through `tail` directly — the output buffers and shows nothing until it completes.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: PASS, no failures.

- [ ] **Step 9: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "Document incremental and async stream hashing

Corrects the README's claim that every operation has an async variant,
which was the documentation half of #6, and states which async variants
are genuine and which are thread pool conveniences.

Closes #6."
```

---

## Definition of Done

- [ ] All fifteen hash providers override `CreateIncremental()`; none inherit the buffering fallback
- [ ] `Incremental_In_Chunks_Equals_OneShot` passes for all fifteen
- [ ] `TryHashAsync(Stream, …)` contains no `ProviderHelpers.RunAsync` call and no `Task.Run`
- [ ] Zero new `#if` directives
- [ ] `ktsu.Essentials` has gained no package dependency — its `.csproj` `ItemGroup` is unchanged
- [ ] `dotnet build -c Release` succeeds across all six frameworks with zero warnings
- [ ] The full test suite passes
- [ ] README no longer claims every operation has an async variant
- [ ] One commit in the branch carries the `[minor]` tag
