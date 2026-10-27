// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.CompressionProviders.Brotli;
using ktsu.Essentials.CompressionProviders.Gzip;
using ktsu.Essentials.CompressionProviders.ZLib;
using ktsu.Essentials.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Compressed data cut off part way is a failure, not a shorter success (issue #80). The framework decompressors
/// treat the end of the input as a clean end of stream, so the providers check the format's own end: Gzip's
/// CRC-32 and size trailer, ZLib's Adler-32 trailer, and Brotli's final block. Raw Deflate carries none of these
/// and is documented as unable to tell, so it is not covered here.
/// </summary>
[TestClass]
public sealed class TruncatedCompressionTests
{
	public TestContext TestContext { get; set; } = null!;

	public static IEnumerable<object[]> Providers =>
	[
		[new GzipCompressionProvider()],
		[new ZLibCompressionProvider()],
		[new BrotliCompressionProvider()],
	];

	[TestMethod]
	[DynamicData(nameof(Providers))]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Deliberately exercises the synchronous stream path alongside the asynchronous one.")]
	public async Task Truncated_Input_Is_Reported_As_A_Failure_On_Every_Path(ICompressionProvider provider)
	{
		string name = provider.GetType().Name;
		byte[] payload = Payload();
		byte[] compressed = provider.Compress(payload);

		foreach (int length in Cuts(compressed.Length))
		{
			byte[] truncated = compressed[..length];

			Assert.IsFalse(provider.TryDecompress(truncated, new byte[payload.Length], out int written), $"{name} span accepted {length} of {compressed.Length} bytes");
			Assert.AreEqual(0, written, $"{name} span reported output for {length} bytes");

			using (MemoryStream source = new(truncated))
			using (MemoryStream sink = new())
			{
				Assert.IsFalse(provider.TryDecompress(source, sink), $"{name} stream accepted {length} of {compressed.Length} bytes");
			}

			using (AsyncOnlyStream source = new(truncated))
			using (AsyncOnlyStream sink = new())
			{
				Assert.IsFalse(await provider.TryDecompressAsync(source, sink, TestContext.CancellationToken).ConfigureAwait(false), $"{name} async accepted {length} of {compressed.Length} bytes");
			}

			Assert.ThrowsExactly<InvalidOperationException>(() => provider.Decompress(truncated), $"{name} Decompress returned short output for {length} bytes");
		}
	}

	[TestMethod]
	[DynamicData(nameof(Providers))]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Deliberately exercises the synchronous stream path alongside the asynchronous one.")]
	public async Task Complete_Input_Still_Decompresses_On_Every_Path(ICompressionProvider provider)
	{
		string name = provider.GetType().Name;
		foreach (byte[] payload in (byte[][])[[], [42], Payload()])
		{
			byte[] compressed = provider.Compress(payload);

			byte[] destination = new byte[payload.Length];
			Assert.IsTrue(provider.TryDecompress(compressed, destination, out int written), $"{name} span rejected {payload.Length} bytes");
			Assert.AreEqual(payload.Length, written);
			CollectionAssert.AreEqual(payload, destination);

			using (MemoryStream source = new(compressed))
			using (MemoryStream sink = new())
			{
				Assert.IsTrue(provider.TryDecompress(source, sink), $"{name} stream rejected {payload.Length} bytes");
				CollectionAssert.AreEqual(payload, sink.ToArray());
			}

			using (AsyncOnlyStream source = new(compressed))
			using (AsyncOnlyStream sink = new())
			{
				Assert.IsTrue(await provider.TryDecompressAsync(source, sink, TestContext.CancellationToken).ConfigureAwait(false), $"{name} async rejected {payload.Length} bytes");
				CollectionAssert.AreEqual(payload, sink.ToArray());
			}
		}
	}

	[TestMethod]
	public void Gzip_Accepts_Concatenated_Members()
	{
		// GZipStream reads every member of a concatenated file, as gzip itself does, and so must the check on
		// its trailer, which is the last member's alone.
		ICompressionProvider gzip = new GzipCompressionProvider();
		byte[] first = Payload();
		byte[] second = [.. Payload().AsSpan(0, 5000)];
		byte[] concatenated = [.. gzip.Compress(first), .. gzip.Compress(second)];

		CollectionAssert.AreEqual((byte[])[.. first, .. second], gzip.Decompress(concatenated));
	}

	private static IEnumerable<int> Cuts(int length)
	{
		foreach (int cut in new[] { 1, 2, 3, 4, 8, 12, length / 3, length / 2, length - 9, length - 8, length - 5, length - 4, length - 2, length - 1 })
		{
			if (cut > 0 && cut < length)
			{
				yield return cut;
			}
		}
	}

	private static byte[] Payload()
	{
		Random random = new(80);
		byte[] payload = new byte[100_000];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i % 3 == 0 ? random.Next(256) : i % 17);
		}

		return payload;
	}
}
