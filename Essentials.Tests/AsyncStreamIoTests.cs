// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.CompressionProviders.Brotli;
using ktsu.Essentials.CompressionProviders.Deflate;
using ktsu.Essentials.CompressionProviders.Gzip;
using ktsu.Essentials.CompressionProviders.ZLib;
using ktsu.Essentials.EncryptionProviders.Aes;
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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Deliberately exercises the synchronous path to compare its output against the asynchronous path.")]
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

	[TestMethod]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Cancelling before any awaited work starts; the token must be cancelled synchronously up front.")]
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

	[TestMethod]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Cancelling before any awaited work starts; the token must be cancelled synchronously up front.")]
	public async Task GzipTryCompressAsync_NullSourceWithAnAlreadyCancelledTokenReturnsFalseAsync()
	{
		// Pins the current guard ordering: the null check runs before the cancellation check, so a null
		// argument combined with an already-cancelled token returns false rather than throwing. Either
		// ordering would be defensible; this test documents which one is actually in effect.
		ICompressionProvider provider = new GzipCompressionProvider();
		using AsyncOnlyStream destination = new();
		using CancellationTokenSource cancelled = new();
		cancelled.Cancel();

		bool compressed = await provider.TryCompressAsync((Stream)null!, destination, cancelled.Token).ConfigureAwait(false);

		Assert.IsFalse(compressed, "A null source combined with an already-cancelled token is expected to return false, not throw.");
	}

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

	[TestMethod]
	public async Task GzipTryDecompressAsyncFromMemory_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		byte[] payload = [9, 8, 7, 6, 5, 4, 3, 2, 1];
		byte[] compressed = provider.Compress(payload);

		using AsyncOnlyStream destination = new();

		bool decompressed = await provider
			.TryDecompressAsync(compressed.AsMemory(), destination, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		Assert.IsTrue(decompressed, "Expected the asynchronous decompression to succeed.");
		CollectionAssert.AreEqual(payload, destination.ToArray());
	}

	[TestMethod]
	public async Task GzipDecompressAsyncFromStream_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		ICompressionProvider provider = new GzipCompressionProvider();
		byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8];
		byte[] compressed = provider.Compress(payload);

		using AsyncOnlyStream source = new(compressed);

		byte[] decompressed = await provider
			.DecompressAsync(source, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		CollectionAssert.AreEqual(payload, decompressed);
	}

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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Cancelling before any awaited work starts; the token must be cancelled synchronously up front.")]
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

	[TestMethod]
	public async Task AesEncryptionRoundTripsThroughAsyncOnlyStreamsAsync()
	{
		// GenerateKey and GenerateIV are declared on the concrete provider, not the interface. The
		// interface reference is kept deliberately to prove that interface dispatch reaches the
		// provider's own implementation. Hence the two references to one instance.
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
	public async Task AesTryEncryptAsyncFromMemory_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		AesEncryptionProvider aes = new();
		IEncryptionProvider provider = aes;
		byte[] key = aes.GenerateKey();
		byte[] iv = aes.GenerateIV();
		byte[] payload = [10, 20, 30, 40, 50, 60, 70, 80, 90];

		using AsyncOnlyStream destination = new();

		bool encrypted = await provider
			.TryEncryptAsync(payload.AsMemory(), key, iv, destination, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		Assert.IsTrue(encrypted, "Expected the asynchronous encryption to succeed.");

		byte[] decrypted = provider.Decrypt(destination.ToArray(), key, iv);
		CollectionAssert.AreEqual(payload, decrypted);
	}

	[TestMethod]
	public async Task AesEncryptAsyncFromStream_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		AesEncryptionProvider aes = new();
		IEncryptionProvider provider = aes;
		byte[] key = aes.GenerateKey();
		byte[] iv = aes.GenerateIV();
		byte[] payload = [10, 20, 30, 40, 50, 60, 70, 80, 90];

		using AsyncOnlyStream source = new(payload);

		byte[] encrypted = await provider
			.EncryptAsync(source, key, iv, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		byte[] decrypted = provider.Decrypt(encrypted, key, iv);
		CollectionAssert.AreEqual(payload, decrypted);
	}

	[TestMethod]
	public async Task AesTryDecryptAsyncFromMemory_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		AesEncryptionProvider aes = new();
		IEncryptionProvider provider = aes;
		byte[] key = aes.GenerateKey();
		byte[] iv = aes.GenerateIV();
		byte[] payload = [10, 20, 30, 40, 50, 60, 70, 80, 90];
		byte[] encrypted = provider.Encrypt(payload, key, iv);

		using AsyncOnlyStream destination = new();

		bool decrypted = await provider
			.TryDecryptAsync(encrypted.AsMemory(), key, iv, destination, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		Assert.IsTrue(decrypted, "Expected the asynchronous decryption to succeed.");
		CollectionAssert.AreEqual(payload, destination.ToArray());
	}

	[TestMethod]
	public async Task AesDecryptAsyncFromStream_UsesOnlyTheAsynchronousStreamMembersAsync()
	{
		AesEncryptionProvider aes = new();
		IEncryptionProvider provider = aes;
		byte[] key = aes.GenerateKey();
		byte[] iv = aes.GenerateIV();
		byte[] payload = [10, 20, 30, 40, 50, 60, 70, 80, 90];
		byte[] encrypted = provider.Encrypt(payload, key, iv);

		using AsyncOnlyStream source = new(encrypted);

		byte[] decrypted = await provider
			.DecryptAsync(source, key, iv, TestContext.CancellationTokenSource.Token)
			.ConfigureAwait(false);

		CollectionAssert.AreEqual(payload, decrypted);
	}

	[TestMethod]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Cancelling before any awaited work starts; the token must be cancelled synchronously up front.")]
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

	public TestContext TestContext { get; set; } = null!;
}
