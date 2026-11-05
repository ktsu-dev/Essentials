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

	[TestMethod]
	public async Task Gzip_Rejects_A_Stored_Block_Payload_Cut_After_A_Zeroed_Field()
	{
		// Stored blocks copy the payload verbatim, so a cut just after four zero bytes leaves a "trailer" whose
		// length field reads as 0 and whose CRC field is payload data (issue #171).
		ICompressionProvider gzip = new GzipCompressionProvider();
		byte[] payload = new byte[100_000];
		new Random(42).NextBytes(payload);
		payload.AsSpan(50_000, 4).Clear();
		byte[] compressed = gzip.Compress(payload);
		int zeros = 20 + compressed.AsSpan(20).IndexOf(new byte[4]);
		byte[] truncated = compressed[..(zeros + 4)];

		await AssertRejectedOnEveryPath(gzip, truncated, payload.Length, $"cut at {truncated.Length}").ConfigureAwait(false);
	}

	[TestMethod]
	public async Task Gzip_Rejects_A_Stored_Block_Payload_Cut_At_Every_Offset()
	{
		// Random bytes with zero runs and small little-endian integers in them, which deflate stores verbatim:
		// whatever the cut, the input's last eight bytes are payload, not a trailer.
		ICompressionProvider gzip = new GzipCompressionProvider();
		byte[] payload = StoredBlockPayload(20_000);
		byte[] compressed = gzip.Compress(payload);

		for (int length = 1; length < compressed.Length; length++)
		{
			await AssertRejectedOnEveryPath(gzip, compressed[..length], payload.Length, $"cut at {length} of {compressed.Length}").ConfigureAwait(false);
		}
	}

	[TestMethod]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Deliberately exercises the synchronous stream path alongside the asynchronous one.")]
	public async Task Gzip_Accepts_Concatenated_Members_On_Every_Path()
	{
		ICompressionProvider gzip = new GzipCompressionProvider();
		byte[] first = StoredBlockPayload(150_000);
		byte[] second = [.. Payload().AsSpan(0, 5000)];
		byte[] third = StoredBlockPayload(3000);
		byte[] expected = [.. first, .. second, .. third];
		byte[] concatenated = [.. gzip.Compress(first), .. gzip.Compress(second), .. gzip.Compress(third)];

		byte[] destination = new byte[expected.Length];
		Assert.IsTrue(gzip.TryDecompress(concatenated, destination, out int written), "span rejected concatenated members");
		Assert.AreEqual(expected.Length, written);
		CollectionAssert.AreEqual(expected, destination);

		using (MemoryStream source = new(concatenated))
		using (MemoryStream sink = new())
		{
			Assert.IsTrue(gzip.TryDecompress(source, sink), "stream rejected concatenated members");
			CollectionAssert.AreEqual(expected, sink.ToArray());
		}

		using (AsyncOnlyStream source = new(concatenated))
		using (AsyncOnlyStream sink = new())
		{
			Assert.IsTrue(await gzip.TryDecompressAsync(source, sink, TestContext.CancellationToken).ConfigureAwait(false), "async rejected concatenated members");
			CollectionAssert.AreEqual(expected, sink.ToArray());
		}
	}

	[TestMethod]
	public async Task Gzip_Rejects_Concatenated_Members_Whose_Last_Member_Is_Cut()
	{
		ICompressionProvider gzip = new GzipCompressionProvider();
		byte[] first = gzip.Compress(Payload());
		byte[] last = gzip.Compress(StoredBlockPayload(4000));
		byte[] concatenated = [.. first, .. last];

		for (int length = first.Length + 1; length < concatenated.Length; length++)
		{
			await AssertRejectedOnEveryPath(gzip, concatenated[..length], concatenated.Length * 2, $"cut at {length} of {concatenated.Length}").ConfigureAwait(false);
		}
	}

	[TestMethod]
	[DynamicData(nameof(Providers))]
	public async Task Trailing_Bytes_After_The_Stream_Are_Reported_As_A_Failure_On_Every_Path(ICompressionProvider provider)
	{
		// A shortened payload reported as a success is the failure #80 ruled out for truncation; bytes after
		// the end of the stream must not produce one either (issue #178).
		string name = provider.GetType().Name;
		foreach (byte[] payload in (byte[][])[[], [97, 97, 97], Payload()])
		{
			byte[] compressed = provider.Compress(payload);
			foreach (byte[] junk in (byte[][])[[0xDE, 0xAD, 0xBE, 0xEF], [0], new byte[100_000]])
			{
				await AssertRejectedOnEveryPath(provider, [.. compressed, .. junk], payload.Length + junk.Length, $"{name}: {payload.Length}-byte payload with {junk.Length} trailing bytes").ConfigureAwait(false);
			}
		}
	}

	[TestMethod]
	public async Task Brotli_Rejects_Concatenated_Streams_On_Every_Path()
	{
		// Unlike gzip members, Brotli streams do not concatenate, so a second one is trailing data rather than
		// more output (issue #178).
		ICompressionProvider brotli = new BrotliCompressionProvider();
		byte[] first = Payload();
		byte[] second = [.. Payload().AsSpan(0, 5000)];
		foreach ((byte[] a, byte[] b) in new[] { ("aaa"u8.ToArray(), "bbb"u8.ToArray()), (first, second) })
		{
			byte[] concatenated = [.. brotli.Compress(a), .. brotli.Compress(b)];
			await AssertRejectedOnEveryPath(brotli, concatenated, a.Length + b.Length, $"{a.Length}+{b.Length} bytes in two streams").ConfigureAwait(false);
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Deliberately exercises the synchronous stream path alongside the asynchronous one.")]
	private async Task AssertRejectedOnEveryPath(ICompressionProvider provider, byte[] truncated, int capacity, string what)
	{
		Assert.IsFalse(provider.TryDecompress(truncated, new byte[capacity], out int written), $"span accepted {what}");
		Assert.AreEqual(0, written, $"span reported output for {what}");

		using (MemoryStream source = new(truncated))
		using (MemoryStream sink = new())
		{
			Assert.IsFalse(provider.TryDecompress(source, sink), $"stream accepted {what}");
		}

		using (AsyncOnlyStream source = new(truncated))
		using (AsyncOnlyStream sink = new())
		{
			Assert.IsFalse(await provider.TryDecompressAsync(source, sink, TestContext.CancellationToken).ConfigureAwait(false), $"async accepted {what}");
		}

		Assert.ThrowsExactly<InvalidOperationException>(() => provider.Decompress(truncated), $"Decompress returned short output for {what}");
	}

	/// <summary>Random bytes, which deflate leaves in stored blocks, with zero runs and small integers through them.</summary>
	private static byte[] StoredBlockPayload(int length)
	{
		Random random = new(171);
		byte[] payload = new byte[length];
		random.NextBytes(payload);
		for (int i = 0; i + 8 <= length; i += 300 + random.Next(300))
		{
			// A zeroed 32-bit field after a small little-endian one, as binary formats are full of
			payload.AsSpan(i, 8).Clear();
			payload[i] = (byte)random.Next(1, 16);
		}

		return payload;
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
