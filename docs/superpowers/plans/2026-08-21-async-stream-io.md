# Genuine async stream I/O Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the compression and encryption stream paths genuinely asynchronous, so they hold no thread-pool thread for the duration of the I/O.

**Architecture:** The `*Async` members on the provider interfaces are default interface implementations that wrap synchronous work in `Task.Run`. A provider that declares a matching member supplies the interface implementation itself and the default is not used, so genuine async is added provider by provider with no interface change. Each interface has two stream→stream *primitives* and four *derived* stream members; the derived defaults are rewritten to compose over the primitives, so a provider only overrides two methods and the other four become genuinely async for free.

**Tech Stack:** C# 13, .NET 10 down to netstandard2.1, MSTest (MSTest.Sdk), `GZipStream`/`DeflateStream`/`ZLibStream`/`BrotliStream`, `CryptoStream`.

**Spec:** `docs/superpowers/specs/2026-08-21-async-surface-design.md`

## Global Constraints

- Target frameworks are `net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1`. There is **no** netstandard2.0 target, so `IAsyncDisposable`, `await using` and `Stream.DisposeAsync` are available on every target.
- Use `Stream.CopyToAsync(Stream, int bufferSize, CancellationToken)` — the three-argument overload — because it is present on every target framework listed above. Pass `81920`, which is the framework's own default buffer size.
- Every `await` uses `.ConfigureAwait(false)`. For `await using`, that means the `await using (x.ConfigureAwait(false))` block form, not the declaration form.
- Tabs for indentation. File-scoped namespaces. Usings inside the namespace. Braces on all control flow. No `this.` qualifiers.
- `TreatWarningsAsErrors` is on and `AnalysisLevel` is `10.0-all`. Expect CA2007 (missing `ConfigureAwait`) to be an error, not a warning.
- Do not change any method signature. This release is non-breaking; it is `[minor]` because it adds behaviour, not surface.
- Do not edit `VERSION.md`, `CHANGELOG.md` or `LICENSE.md` by hand.
- Commit messages carry no `Co-Authored-By` line. Tag the final release commit `[minor]`; intermediate commits `[patch]`.

## File Structure

| File | Responsibility |
| --- | --- |
| `Essentials.Tests/Infrastructure/AsyncOnlyStream.cs` | *Create.* Test double whose synchronous members throw, proving an implementation never touches them. |
| `Essentials.Tests/AsyncStreamIoTests.cs` | *Create.* All tests asserting genuine async, round-trip equivalence and cancellation. |
| `Essentials/ICompressionProvider.cs` | *Modify.* Rewrite four derived stream defaults to compose over the two primitives. |
| `Essentials/IEncryptionProvider.cs` | *Modify.* Same, for encryption. |
| `Essentials.CompressionProviders.Gzip/GzipCompressionProvider.cs` | *Modify.* Add the two async primitives. |
| `Essentials.CompressionProviders.Deflate/DeflateCompressionProvider.cs` | *Modify.* Same. |
| `Essentials.CompressionProviders.ZLib/ZLibCompressionProvider.cs` | *Modify.* Same. |
| `Essentials.CompressionProviders.Brotli/BrotliCompressionProvider.cs` | *Modify.* Same. |
| `Essentials.EncryptionProviders.Aes/AesEncryptionProvider.cs` | *Modify.* Same. |
| `README.md` | *Modify.* State which operations are genuinely asynchronous. |
| `CLAUDE.md` | *Modify.* Same, for the internal note. |

---

### Task 1: The `AsyncOnlyStream` test double

A timing assertion cannot prove "holds no thread" without flaking. This double proves it by construction: an implementation that is asynchronous in name only reaches for a synchronous member and throws.

**Files:**
- Create: `Essentials.Tests/Infrastructure/AsyncOnlyStream.cs`
- Test: `Essentials.Tests/AsyncStreamIoTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal sealed class AsyncOnlyStream : Stream` in namespace `ktsu.Essentials.Tests.Infrastructure`, with constructor `AsyncOnlyStream(byte[] contents)` for a readable source and `AsyncOnlyStream()` for an empty writable sink, plus `byte[] ToArray()`.

- [ ] **Step 1: Write the failing test**

Create `Essentials.Tests/AsyncStreamIoTests.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using ktsu.Essentials.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Proves the stream paths are genuinely asynchronous rather than synchronous work on a pool thread.
/// </summary>
/// <remarks>
/// "Holds no thread" cannot be established by measuring elapsed time without flaking. These tests
/// establish it by construction instead: <see cref="AsyncOnlyStream"/> throws from every synchronous
/// member, so an implementation that wraps synchronous work fails loudly rather than passing quietly.
/// </remarks>
[TestClass]
public class AsyncStreamIoTests
{
	[TestMethod]
	public void AsyncOnlyStream_ThrowsFromSynchronousRead()
	{
		using AsyncOnlyStream source = new([1, 2, 3]);

		Assert.ThrowsExactly<NotSupportedException>(() => source.ReadByte());
	}

	[TestMethod]
	public void AsyncOnlyStream_ThrowsFromSynchronousWrite()
	{
		using AsyncOnlyStream sink = new();

		Assert.ThrowsExactly<NotSupportedException>(() => sink.WriteByte(1));
	}

	[TestMethod]
	public async Task AsyncOnlyStream_RoundTripsThroughTheAsynchronousMembersAsync()
	{
		byte[] payload = [1, 2, 3, 4, 5];
		using AsyncOnlyStream source = new(payload);
		using AsyncOnlyStream sink = new();

		await source.CopyToAsync(sink, 81920, TestContext.CancellationTokenSource.Token).ConfigureAwait(false);

		CollectionAssert.AreEqual(payload, sink.ToArray());
	}

	public TestContext TestContext { get; set; } = null!;
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~AsyncStreamIoTests"`

Expected: FAIL to compile with `CS0246: The type or namespace name 'AsyncOnlyStream' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `Essentials.Tests/Infrastructure/AsyncOnlyStream.cs`:

```csharp
// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests.Infrastructure;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A stream that supports only its asynchronous members. Every synchronous read or write throws.
/// </summary>
/// <remarks>
/// Both the array-based and memory-based asynchronous overloads are overridden. The base class routes
/// one to the other and, for an unoverridden stream, ultimately to the synchronous member — so
/// overriding only one would let a synchronous implementation slip through the very check this exists
/// to make. <see cref="Flush"/> is deliberately a no-op rather than a throw: it moves no data, and
/// disposal calls it.
/// </remarks>
internal sealed class AsyncOnlyStream : Stream
{
	private readonly MemoryStream inner;

	/// <summary>Creates a readable stream over the given contents.</summary>
	public AsyncOnlyStream(byte[] contents) => inner = new MemoryStream(contents, writable: false);

	/// <summary>Creates an empty writable stream.</summary>
	public AsyncOnlyStream() => inner = new MemoryStream();

	/// <summary>The bytes written to this stream.</summary>
	public byte[] ToArray() => inner.ToArray();

	/// <inheritdoc/>
	public override bool CanRead => inner.CanRead;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => inner.CanWrite;

	/// <inheritdoc/>
	public override long Length => inner.Length;

	/// <inheritdoc/>
	public override long Position
	{
		get => inner.Position;
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		// Moves no data, and disposal calls it. Throwing here would fail every test for the wrong reason.
	}

	/// <inheritdoc/>
	public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) =>
		throw new NotSupportedException("This stream is asynchronous only; Read was called.");

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) =>
		throw new NotSupportedException("This stream is asynchronous only; Write was called.");

	/// <inheritdoc/>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		inner.ReadAsync(buffer, offset, count, cancellationToken);

	/// <inheritdoc/>
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
		inner.ReadAsync(buffer, cancellationToken);

	/// <inheritdoc/>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		inner.WriteAsync(buffer, offset, count, cancellationToken);

	/// <inheritdoc/>
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
		inner.WriteAsync(buffer, cancellationToken);

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			inner.Dispose();
		}

		base.Dispose(disposing);
	}
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~AsyncStreamIoTests"`

Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add Essentials.Tests/Infrastructure/AsyncOnlyStream.cs Essentials.Tests/AsyncStreamIoTests.cs
git commit -m "test: add an async-only stream double [patch]"
```

---

### Task 2: Gzip stream primitives

**Files:**
- Modify: `Essentials.CompressionProviders.Gzip/GzipCompressionProvider.cs`
- Test: `Essentials.Tests/AsyncStreamIoTests.cs`

**Interfaces:**
- Consumes: `AsyncOnlyStream` from Task 1.
- Produces: on `GzipCompressionProvider`, `public async Task<bool> TryCompressAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)` and `public async Task<bool> TryDecompressAsync(Stream compressedData, Stream destination, CancellationToken cancellationToken = default)`. Tasks 3 and 4 rely on these exact names and signatures.

- [ ] **Step 1: Write the failing test**

Append to `Essentials.Tests/AsyncStreamIoTests.cs`, inside the class:

```csharp
	[TestMethod]
	public async Task GzipTryCompressAsync_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		using AsyncOnlyStream source = new([1, 2, 3, 4, 5, 6, 7, 8]);
		using AsyncOnlyStream destination = new();

		bool compressed = await provider
			.TryCompressAsync(source, destination, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		Assert.IsTrue(compressed, "Expected the asynchronous compression to succeed.");
		Assert.AreNotEqual(0, destination.ToArray().Length, "Expected compressed bytes to be written.");
	}

	[TestMethod]
	public async Task GzipAsyncCompression_ProducesTheSameBytesAsTheSynchronousPathAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		byte[] payload = new byte[4096];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i % 251);
		}

		using MemoryStream syncSource = new(payload);
		using MemoryStream syncDestination = new();
		Assert.IsTrue(provider.TryCompress(syncSource, syncDestination));

		using AsyncOnlyStream asyncSource = new(payload);
		using AsyncOnlyStream asyncDestination = new();
		Assert.IsTrue(await provider
			.TryCompressAsync(asyncSource, asyncDestination, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false));

		CollectionAssert.AreEqual(syncDestination.ToArray(), asyncDestination.ToArray());
	}

	[TestMethod]
	public async Task GzipTryDecompressAsync_RoundTripsThroughAsyncOnlyStreamsAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		byte[] payload = [9, 8, 7, 6, 5, 4, 3, 2, 1];

		using AsyncOnlyStream source = new(payload);
		using AsyncOnlyStream compressed = new();
		Assert.IsTrue(await provider
			.TryCompressAsync(source, compressed, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false));

		using AsyncOnlyStream compressedSource = new(compressed.ToArray());
		using AsyncOnlyStream restored = new();
		Assert.IsTrue(await provider
			.TryDecompressAsync(compressedSource, restored, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false));

		CollectionAssert.AreEqual(payload, restored.ToArray());
	}
```

```csharp
	[TestMethod]
	public async Task GzipTryCompressAsync_HonoursAnAlreadyCancelledTokenAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		using AsyncOnlyStream source = new([1, 2, 3]);
		using AsyncOnlyStream destination = new();
		using CancellationTokenSource cancelled = new();
		cancelled.Cancel();

		bool honoured = false;
		try
		{
			_ = await provider.TryCompressAsync(source, destination, cancelled.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			honoured = true;
		}

		Assert.IsTrue(honoured, "An already-cancelled token must be honoured before any work begins.");
		Assert.AreEqual(0, destination.ToArray().Length, "Nothing should have been written.");
	}
```

The cancellation assertion is written as an explicit try/catch rather than an assertion helper, because
the exact name of MSTest's asynchronous throws-helper varies by version and this compiles on all of
them. `ThrowIfCancellationRequested` throws `OperationCanceledException`; awaiting the faulted task
rethrows it unchanged.

Add these usings to the top of the file:

```csharp
using System.Threading;
using ktsu.Essentials.CompressionProviders.Gzip;
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~GzipTryCompressAsync"`

Expected: FAIL with `NotSupportedException: This stream is asynchronous only; Read was called.` — the default implementation is `Task.Run(() => TryCompress(...))`, which reaches for the synchronous member. That failure is the whole point: it is the fake-async being caught.

- [ ] **Step 3: Write minimal implementation**

In `GzipCompressionProvider.cs`, add these two members. Add `using System.Threading;` and `using System.Threading.Tasks;` to the namespace usings.

```csharp
	/// <summary>
	/// Tries to compress the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: no thread is held for the duration. The compression stream is disposed
	/// with <c>await using</c> rather than <c>using</c> because disposal writes the trailer, and a
	/// synchronous dispose would make that final write synchronous.
	/// </remarks>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination to write the compressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public async Task<bool> TryCompressAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			GZipStream gzipStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
			await using (gzipStream.ConfigureAwait(false))
			{
				await data.CopyToAsync(gzipStream, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decompress the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>Genuinely asynchronous: no thread is held for the duration.</remarks>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public async Task<bool> TryDecompressAsync(Stream compressedData, Stream destination, CancellationToken cancellationToken = default)
	{
		if (compressedData is null || destination is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			GZipStream gzipStream = new(compressedData, CompressionMode.Decompress, leaveOpen: true);
			await using (gzipStream.ConfigureAwait(false))
			{
				await gzipStream.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
```

Note that `OperationCanceledException` is deliberately absent from the catch lists, so cancellation propagates to the caller instead of being reported as a plain `false`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~AsyncStreamIoTests"`

Expected: PASS, 7 tests.

- [ ] **Step 5: Verify every target framework still builds**

Run: `dotnet build Essentials.CompressionProviders.Gzip/Essentials.CompressionProviders.Gzip.csproj`

Expected: `0 Warning(s) 0 Error(s)` across all six target frameworks. A CA2007 error here means an `await` is missing `.ConfigureAwait(false)`.

- [ ] **Step 6: Commit**

```bash
git add Essentials.CompressionProviders.Gzip/GzipCompressionProvider.cs Essentials.Tests/AsyncStreamIoTests.cs
git commit -m "feat: make gzip stream compression genuinely asynchronous [minor]"
```

---

### Task 3: Compose the derived compression defaults over the primitives

The four remaining stream-ish members on `ICompressionProvider` still wrap synchronous work. Rewriting them to call the async primitives means a provider that overrides only the two primitives gets all six genuinely async. For a provider that overrides nothing, behaviour is unchanged: the primitives are still `Task.Run` defaults.

**Files:**
- Modify: `Essentials/ICompressionProvider.cs:86-114` and `Essentials/ICompressionProvider.cs:200-228`
- Test: `Essentials.Tests/AsyncStreamIoTests.cs`

**Interfaces:**
- Consumes: `TryCompressAsync(Stream, Stream, CancellationToken)` and `TryDecompressAsync(Stream, Stream, CancellationToken)` from Task 2.
- Produces: no new names. Four existing defaults change body only.

- [ ] **Step 1: Write the failing test**

Append inside the class in `Essentials.Tests/AsyncStreamIoTests.cs`:

```csharp
	[TestMethod]
	public async Task GzipCompressAsyncFromStream_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		using AsyncOnlyStream source = new([1, 2, 3, 4, 5, 6, 7, 8]);

		byte[] compressed = await provider
			.CompressAsync(source, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		Assert.AreNotEqual(0, compressed.Length, "Expected compressed bytes to be returned.");
	}

	[TestMethod]
	public async Task GzipTryCompressAsyncFromMemory_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		using AsyncOnlyStream destination = new();

		bool compressed = await provider
			.TryCompressAsync(new byte[] { 1, 2, 3, 4 }.AsMemory(), destination, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		Assert.IsTrue(compressed, "Expected the asynchronous compression to succeed.");
		Assert.AreNotEqual(0, destination.ToArray().Length, "Expected compressed bytes to be written.");
	}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~FromStream_UsesOnly|FullyQualifiedName~FromMemory_UsesOnly"`

Expected: FAIL with `NotSupportedException: This stream is asynchronous only; Read was called.`

- [ ] **Step 3: Write minimal implementation**

In `Essentials/ICompressionProvider.cs`, replace the four derived bodies. Leave every signature and doc comment untouched; only the expression body changes.

Replace the body at line 86:

```csharp
	public async Task<bool> TryCompressAsync(ReadOnlyMemory<byte> data, Stream destination, CancellationToken cancellationToken = default)
	{
		using MemoryStream source = new();
		await source.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		source.Position = 0;
		return await TryCompressAsync(source, destination, cancellationToken).ConfigureAwait(false);
	}
```

Replace the body at line 114:

```csharp
	public async Task<byte[]> CompressAsync(Stream data, CancellationToken cancellationToken = default)
	{
		using MemoryStream destination = new();
		return !await TryCompressAsync(data, destination, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException("Compression failed to produce output with the allocated buffer.")
			: destination.ToArray();
	}
```

Replace the body at line 200:

```csharp
	public async Task<bool> TryDecompressAsync(ReadOnlyMemory<byte> compressedData, Stream destination, CancellationToken cancellationToken = default)
	{
		using MemoryStream source = new();
		await source.WriteAsync(compressedData, cancellationToken).ConfigureAwait(false);
		source.Position = 0;
		return await TryDecompressAsync(source, destination, cancellationToken).ConfigureAwait(false);
	}
```

Replace the body at line 228:

```csharp
	public async Task<byte[]> DecompressAsync(Stream compressedData, CancellationToken cancellationToken = default)
	{
		using MemoryStream destination = new();
		return !await TryDecompressAsync(compressedData, destination, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException("Decompression failed to produce output with the allocated buffer.")
			: destination.ToArray();
	}
```

Confirm `using System.IO;` and `using System.Threading.Tasks;` are already present in the file's namespace usings; add whichever is missing.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: PASS, whole suite green. The existing round-trip and provider-contract tests are what prove the rewritten defaults did not change behaviour for providers that override nothing.

- [ ] **Step 5: Commit**

```bash
git add Essentials/ICompressionProvider.cs Essentials.Tests/AsyncStreamIoTests.cs
git commit -m "feat: compose derived compression async paths over the stream primitives [minor]"
```

---

### Task 4: Deflate, ZLib and Brotli stream primitives

Same two members as Task 2, once per provider. The only differences are the stream type and the exception list, which mirrors each provider's existing synchronous method.

**Files:**
- Modify: `Essentials.CompressionProviders.Deflate/DeflateCompressionProvider.cs`
- Modify: `Essentials.CompressionProviders.ZLib/ZLibCompressionProvider.cs`
- Modify: `Essentials.CompressionProviders.Brotli/BrotliCompressionProvider.cs`
- Test: `Essentials.Tests/AsyncStreamIoTests.cs`

**Interfaces:**
- Consumes: `AsyncOnlyStream` from Task 1; the pattern established in Task 2.
- Produces: `TryCompressAsync(Stream, Stream, CancellationToken)` and `TryDecompressAsync(Stream, Stream, CancellationToken)` on each of the three providers.

- [ ] **Step 1: Write the failing test**

Append inside the class in `Essentials.Tests/AsyncStreamIoTests.cs`, and add usings for the three provider namespaces:

```csharp
	public static IEnumerable<object[]> StreamingCompressionProviders =>
	[
		[new DeflateCompressionProvider()],
		[new ZLibCompressionProvider()],
		[new BrotliCompressionProvider()],
	];

	[TestMethod]
	[DynamicData(nameof(StreamingCompressionProviders))]
	public async Task CompressionProvider_RoundTripsThroughAsyncOnlyStreamsAsync(ICompressionProvider provider)
	{
		byte[] payload = new byte[2048];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i % 97);
		}

		using AsyncOnlyStream source = new(payload);
		using AsyncOnlyStream compressed = new();
		Assert.IsTrue(await provider
			.TryCompressAsync(source, compressed, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false));

		using AsyncOnlyStream compressedSource = new(compressed.ToArray());
		using AsyncOnlyStream restored = new();
		Assert.IsTrue(await provider
			.TryDecompressAsync(compressedSource, restored, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false));

		CollectionAssert.AreEqual(payload, restored.ToArray());
	}

	[TestMethod]
	[DynamicData(nameof(StreamingCompressionProviders))]
	public async Task CompressionProvider_HonoursAnAlreadyCancelledTokenAsync(ICompressionProvider provider)
	{
		using AsyncOnlyStream source = new([1, 2, 3]);
		using AsyncOnlyStream destination = new();
		using CancellationTokenSource cancelled = new();
		cancelled.Cancel();

		bool honoured = false;
		try
		{
			_ = await provider.TryCompressAsync(source, destination, cancelled.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			honoured = true;
		}

		Assert.IsTrue(honoured, "An already-cancelled token must be honoured before any work begins.");
		Assert.AreEqual(0, destination.ToArray().Length, "Nothing should have been written.");
	}
```

Add to the usings:

```csharp
using System.Collections.Generic;
using ktsu.Essentials.CompressionProviders.Brotli;
using ktsu.Essentials.CompressionProviders.Deflate;
using ktsu.Essentials.CompressionProviders.ZLib;
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~CompressionProvider_RoundTripsThroughAsyncOnlyStreams"`

Expected: FAIL, three cases, each `NotSupportedException: This stream is asynchronous only; Read was called.`

- [ ] **Step 3: Write minimal implementation**

In `DeflateCompressionProvider.cs`, add the two members. Add `using System.Threading;` and `using System.Threading.Tasks;`:

```csharp
	/// <summary>
	/// Tries to compress the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: no thread is held for the duration. Disposal writes the trailer, so the
	/// compression stream is disposed with <c>await using</c> to keep that final write asynchronous too.
	/// </remarks>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination to write the compressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public async Task<bool> TryCompressAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			DeflateStream deflateStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
			await using (deflateStream.ConfigureAwait(false))
			{
				await data.CopyToAsync(deflateStream, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decompress the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>Genuinely asynchronous: no thread is held for the duration.</remarks>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public async Task<bool> TryDecompressAsync(Stream compressedData, Stream destination, CancellationToken cancellationToken = default)
	{
		if (compressedData is null || destination is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			DeflateStream deflateStream = new(compressedData, CompressionMode.Decompress, leaveOpen: true);
			await using (deflateStream.ConfigureAwait(false))
			{
				await deflateStream.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
```

In `ZLibCompressionProvider.cs`, add the same two members with `ZLibStream` substituted for `DeflateStream` in both places (the local variable may be named `zlibStream`).

In `BrotliCompressionProvider.cs`, add the same two members with `BrotliStream` substituted for `DeflateStream` in both places (the local variable may be named `brotliStream`).

Before writing each, open the provider's existing synchronous `TryCompress(Stream, Stream)` and confirm its `catch` list matches the one above. If a provider catches a different set, mirror that provider's list rather than this one — the asynchronous member must fail in exactly the same cases as its synchronous twin.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: PASS, whole suite green.

- [ ] **Step 5: Verify every target framework still builds**

Run: `dotnet build`

Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add Essentials.CompressionProviders.Deflate Essentials.CompressionProviders.ZLib Essentials.CompressionProviders.Brotli Essentials.Tests/AsyncStreamIoTests.cs
git commit -m "feat: make deflate, zlib and brotli stream compression genuinely asynchronous [minor]"
```

---

### Task 5: Aes stream primitives and the derived encryption defaults

**Files:**
- Modify: `Essentials.EncryptionProviders.Aes/AesEncryptionProvider.cs`
- Modify: `Essentials/IEncryptionProvider.cs`
- Test: `Essentials.Tests/AsyncStreamIoTests.cs`

**Interfaces:**
- Consumes: `AsyncOnlyStream` from Task 1.
- Produces: on `AesEncryptionProvider`, `public async Task<bool> TryEncryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)` and the matching `TryDecryptAsync`.

- [ ] **Step 1: Write the failing test**

Append inside the class in `Essentials.Tests/AsyncStreamIoTests.cs`, and add `using ktsu.Essentials.EncryptionProviders.Aes;`:

```csharp
	[TestMethod]
	public async Task AesEncryptionRoundTripsThroughAsyncOnlyStreamsAsync()
	{
		// GenerateKey and GenerateIV are declared on the concrete provider, not the interface, while the
		// async members are default interface implementations and so need an interface-typed reference
		// until Task 5 gives the provider its own. Hence the two references to one instance.
		AesEncryptionProvider aes = new();
		IEncryptionProvider provider = aes;
		byte[] key = aes.GenerateKey();
		byte[] iv = aes.GenerateIV();

		byte[] payload = [10, 20, 30, 40, 50, 60, 70, 80, 90];

		using AsyncOnlyStream source = new(payload);
		using AsyncOnlyStream encrypted = new();
		Assert.IsTrue(await provider
			.TryEncryptAsync(source, key, iv, encrypted, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false));

		using AsyncOnlyStream encryptedSource = new(encrypted.ToArray());
		using AsyncOnlyStream restored = new();
		Assert.IsTrue(await provider
			.TryDecryptAsync(encryptedSource, key, iv, restored, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false));

		CollectionAssert.AreEqual(payload, restored.ToArray());
	}

	[TestMethod]
	public async Task AesTryEncryptAsync_HonoursAnAlreadyCancelledTokenAsync()
	{
		AesEncryptionProvider aes = new();
		IEncryptionProvider provider = aes;
		byte[] key = aes.GenerateKey();
		byte[] iv = aes.GenerateIV();

		using AsyncOnlyStream source = new([1, 2, 3]);
		using AsyncOnlyStream destination = new();
		using CancellationTokenSource cancelled = new();
		cancelled.Cancel();

		bool honoured = false;
		try
		{
			_ = await provider.TryEncryptAsync(source, key, iv, destination, cancelled.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			honoured = true;
		}

		Assert.IsTrue(honoured, "An already-cancelled token must be honoured before any work begins.");
		Assert.AreEqual(0, destination.ToArray().Length, "Nothing should have been written.");
	}
```

The parameter order is `(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken)` — verified against `Essentials/IEncryptionProvider.cs:79`. `byte[]` converts implicitly to `ReadOnlyMemory<byte>`, so the key and IV are passed directly.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj --filter "FullyQualifiedName~AesEncryptionRoundTripsThroughAsyncOnlyStreams"`

Expected: FAIL with `NotSupportedException: This stream is asynchronous only; Read was called.`

- [ ] **Step 3: Write minimal implementation**

In `AesEncryptionProvider.cs`, add the two members, mirroring the existing synchronous `TryEncrypt(Stream, …)` including its `catch` list. Add `using System.Threading;` and `using System.Threading.Tasks;`:

```csharp
	/// <summary>
	/// Tries to encrypt the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: no thread is held for the duration. The crypto stream is disposed with
	/// <c>await using</c> because disposal writes the final block, and a synchronous dispose would make
	/// that write synchronous.
	/// </remarks>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public async Task<bool> TryEncryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null || key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform encryptor = aes.CreateEncryptor(key.ToArray(), iv.ToArray());
			CryptoStream cryptoStream = new(destination, encryptor, CryptoStreamMode.Write, leaveOpen: true);
			await using (cryptoStream.ConfigureAwait(false))
			{
				await data.CopyToAsync(cryptoStream, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decrypt the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>Genuinely asynchronous: no thread is held for the duration.</remarks>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public async Task<bool> TryDecryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null || key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform decryptor = aes.CreateDecryptor(key.ToArray(), iv.ToArray());
			CryptoStream cryptoStream = new(data, decryptor, CryptoStreamMode.Read, leaveOpen: true);
			await using (cryptoStream.ConfigureAwait(false))
			{
				await cryptoStream.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
```

Then replace the four derived stream-ish bodies in `Essentials/IEncryptionProvider.cs`. Signatures and doc comments stay exactly as they are; only the expression body changes.

Replace the body at line 91:

```csharp
	public async Task<bool> TryEncryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		using MemoryStream source = new();
		await source.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		source.Position = 0;
		return await TryEncryptAsync(source, key, iv, destination, cancellationToken).ConfigureAwait(false);
	}
```

Replace the body at line 165:

```csharp
	public async Task<byte[]> EncryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
	{
		using MemoryStream destination = new();
		return !await TryEncryptAsync(data, key, iv, destination, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException("Encryption failed to produce output with the allocated buffer.")
			: destination.ToArray();
	}
```

Replace the body at line 233:

```csharp
	public async Task<bool> TryDecryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		using MemoryStream source = new();
		await source.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		source.Position = 0;
		return await TryDecryptAsync(source, key, iv, destination, cancellationToken).ConfigureAwait(false);
	}
```

Replace the body at line 304:

```csharp
	public async Task<byte[]> DecryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
	{
		using MemoryStream destination = new();
		return !await TryDecryptAsync(data, key, iv, destination, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException("Decryption failed to produce output with the allocated buffer.")
			: destination.ToArray();
	}
```

The failure messages are copied verbatim from the synchronous overloads at lines 105, 122, 247 and 264 — they must not drift, because existing tests assert on them.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: PASS, whole suite green.

- [ ] **Step 5: Commit**

```bash
git add Essentials.EncryptionProviders.Aes Essentials/IEncryptionProvider.cs Essentials.Tests/AsyncStreamIoTests.cs
git commit -m "feat: make aes stream encryption genuinely asynchronous [minor]"
```

---

### Task 6: Documentation

**Files:**
- Modify: `README.md:35`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: the behaviour delivered by Tasks 2 through 5.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Update the README async claim**

The line currently reads:

> - **Async Support**: Operations expose async variants with `CancellationToken` support. Stream hashing is genuinely asynchronous — it reads with `ReadAsync` and holds no thread. Most other async variants are convenience wrappers that run synchronous work on the thread pool; span-destination operations have no async form, because an `out` parameter cannot cross an await boundary

Replace it with:

> - **Async Support**: Operations expose async variants with `CancellationToken` support. Stream hashing, and the stream paths of the compression and AES encryption providers, are genuinely asynchronous — they read and write with `ReadAsync`/`WriteAsync` and hold no thread. The encoding, obfuscation, serialization and in-memory variants are convenience wrappers that run synchronous work on the thread pool; span-destination operations have no async form, because an `out` parameter cannot cross an await boundary

- [ ] **Step 2: Update the CLAUDE.md note**

`CLAUDE.md:87` currently reads:

> 3. **Async variants**: Task-based async versions with `CancellationToken` support, mostly provided via `ProviderHelpers.RunAsync()`. Note these are `Task.Run` wrappers over synchronous work, not genuine async I/O — the exception is `IHashProvider.TryHashAsync(Stream, ...)`, which is a real `ReadAsync` loop. See issue #8. Span-destination async overloads do not exist — an `out` parameter cannot cross an async boundary.

Replace it with:

> 3. **Async variants**: Task-based async versions with `CancellationToken` support. The stream paths of the compression providers and of `AesEncryptionProvider`, along with `IHashProvider.TryHashAsync(Stream, ...)`, are genuinely asynchronous — real `ReadAsync`/`WriteAsync`, no thread held. The rest are still `Task.Run` wrappers over synchronous work via `ProviderHelpers.RunAsync()`; see issue #8. A provider makes its stream paths genuine by declaring the two `Try…Async(Stream, Stream, ...)` primitives itself, which replaces the default implementation; the four derived stream defaults compose over those primitives, so overriding two members converts all six. Span-destination async overloads do not exist — an `out` parameter cannot cross an async boundary.

`CLAUDE.md:91` currently reads:

> - `RunAsync()` - Wraps sync methods in `Task.Run` with cancellation

Replace it with:

> - `RunAsync()` - Wraps sync methods in `Task.Run` with cancellation. Used by the in-memory async variants and by any stream path whose provider has not declared its own asynchronous primitives.

- [ ] **Step 3: Verify the docs build nothing and break nothing**

Run: `dotnet test Essentials.Tests/Essentials.Tests.csproj`

Expected: PASS, whole suite green. Documentation-only, so this is a regression check rather than a new assertion.

- [ ] **Step 4: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "docs: record which async paths are genuine [patch]"
```

---

## Closing out

After Task 6, open the pull request against `main`. The PR body should state plainly that this addresses **12 of the 26** stream-ish sites — the compression and encryption halves — and that encoding, obfuscation and serialization are deliberately excluded for the reasons in the spec. `#8` stays open, or is closed with an explicit note that the `ValueTask` half is tracked separately for 3.0.0.

Do not tag any commit `[major]`. Nothing in this plan changes a signature.
