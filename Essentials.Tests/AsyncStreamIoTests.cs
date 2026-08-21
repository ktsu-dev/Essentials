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

	public TestContext TestContext { get; set; } = null!;
}
