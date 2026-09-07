// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class EncodingProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddCommon();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> EncodingProviders => BuildProvider().EnumerateProviders<IEncodingProvider>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public void Encoding_Roundtrip_Stream(IEncodingProvider encoder, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("encode me with " + providerName);

		using MemoryStream inputStream = new(original);
		using MemoryStream encodedStream = new();
		bool encodeOk = encoder.TryEncode(inputStream, encodedStream);
		Assert.IsTrue(encodeOk, $"{providerName} should successfully encode stream");

		encodedStream.Position = 0;
		using MemoryStream decodedStream = new();
		bool decodeOk = encoder.TryDecode(encodedStream, decodedStream);
		Assert.IsTrue(decodeOk, $"{providerName} should successfully decode stream");

		byte[] result = decodedStream.ToArray();
		CollectionAssert.AreEqual(original, result, $"{providerName} should produce original data from stream");
	}

	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public void Encoding_Roundtrip_Bytes(IEncodingProvider encoder, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("hello world from " + providerName);

		byte[] encoded = encoder.Encode(original);
		Assert.IsTrue(encoded.Length > 0, $"{providerName} should produce encoded output");

		byte[] decoded = encoder.Decode(encoded);
		CollectionAssert.AreEqual(original, decoded, $"{providerName} should decode to original data");
	}

	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public void Encoding_Roundtrip_String(IEncodingProvider encoder, string providerName)
	{
		string original = "string encode test with " + providerName;

		string encoded = encoder.Encode(original);
		Assert.IsFalse(string.IsNullOrEmpty(encoded), $"{providerName} should produce encoded string");

		// Decode the encoded bytes back to original
		byte[] encodedBytes = Encoding.UTF8.GetBytes(encoded);
		byte[] decodedBytes = encoder.Decode(encodedBytes);
		string decoded = Encoding.UTF8.GetString(decodedBytes);
		Assert.AreEqual(original, decoded, $"{providerName} should decode to original string");
	}

	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public void Encoding_TryEncode_Insufficient_Buffer(IEncodingProvider encoder, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("data for buffer test with " + providerName);
		Span<byte> small = stackalloc byte[1];
		bool result = encoder.TryEncode(original, small, out int written);
		Assert.IsFalse(result, $"{providerName} should return false for insufficient buffer");
		Assert.AreEqual(0, written, $"{providerName} should report no bytes written on failure");
	}

	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public void Encoding_Async_Roundtrip(IEncodingProvider encoder, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("async encode with " + providerName);

		using MemoryStream inputStream = new(original);
		using MemoryStream encodedStream = new();
		bool encodeOk = encoder.TryEncodeAsync(inputStream, encodedStream, TestContext.CancellationToken).Result;
		Assert.IsTrue(encodeOk, $"{providerName} async should successfully encode");

		encodedStream.Position = 0;
		using MemoryStream decodedStream = new();
		bool decodeOk = encoder.TryDecodeAsync(encodedStream, decodedStream, TestContext.CancellationToken).Result;
		Assert.IsTrue(decodeOk, $"{providerName} async should successfully decode");

		CollectionAssert.AreEqual(original, decodedStream.ToArray(), $"{providerName} async should produce original data");
	}

	/// <summary>
	/// Tests the async stream paths over an input large enough to cross a provider's internal chunk
	/// boundary, which the short round-trip above never reaches.
	/// </summary>
	/// <remarks>
	/// The chunked provider is the one this matters for: a boundary that split a character pair, or a
	/// short read treated as end-of-stream, would corrupt or truncate the output, and neither shows up
	/// on a payload that fits in one chunk.
	/// </remarks>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_RoundtripsAcrossChunkBoundaries(IEncodingProvider encoder, string providerName)
	{
		byte[] original = new byte[40_000];
		for (int i = 0; i < original.Length; i++)
		{
			original[i] = (byte)(i % 251);
		}

		using MemoryStream inputStream = new(original);
		using MemoryStream encodedStream = new();
		Assert.IsTrue(
			await encoder.TryEncodeAsync(inputStream, encodedStream, TestContext.CancellationToken).ConfigureAwait(false),
			$"{providerName} async should encode a multi-chunk payload");

		encodedStream.Position = 0;
		using MemoryStream decodedStream = new();
		Assert.IsTrue(
			await encoder.TryDecodeAsync(encodedStream, decodedStream, TestContext.CancellationToken).ConfigureAwait(false),
			$"{providerName} async should decode a multi-chunk payload");

		CollectionAssert.AreEqual(original, decodedStream.ToArray(), $"{providerName} async should survive a multi-chunk round trip");
	}

	/// <summary>
	/// Tests that a short read part-way through a stream is treated as "more to come" rather than as
	/// the end of it.
	/// </summary>
	/// <remarks>
	/// A stream may legally return fewer bytes than asked for at any point — a network stream routinely
	/// does — and a chunked reader that mistakes that for end-of-stream silently truncates. A
	/// <see cref="MemoryStream"/> never does it, so it cannot catch this; this drip-feeds a byte at a
	/// time to force the case.
	/// </remarks>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_SurvivesShortReads(IEncodingProvider encoder, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("a payload delivered one byte at a time, via " + providerName);

		using MemoryStream encodedStream = new();
		using (DripStream input = new(original))
		{
			Assert.IsTrue(
				await encoder.TryEncodeAsync(input, encodedStream, TestContext.CancellationToken).ConfigureAwait(false),
				$"{providerName} async should encode from a stream that reads short");
		}

		byte[] encoded = encodedStream.ToArray();
		using MemoryStream decodedStream = new();
		using (DripStream input = new(encoded))
		{
			Assert.IsTrue(
				await encoder.TryDecodeAsync(input, decodedStream, TestContext.CancellationToken).ConfigureAwait(false),
				$"{providerName} async should decode from a stream that reads short");
		}

		CollectionAssert.AreEqual(original, decodedStream.ToArray(), $"{providerName} async should not truncate on short reads");
	}

	/// <summary>
	/// Tests that the memory-source and self-allocating stream overloads reach the same result as the
	/// stream-to-stream primitive, since they are now layered over it rather than over the synchronous
	/// path.
	/// </summary>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_DerivedOverloadsAgreeWithThePrimitive(IEncodingProvider encoder, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("derived overloads for " + providerName);

		using MemoryStream fromMemory = new();
		Assert.IsTrue(
			await encoder.TryEncodeAsync(original.AsMemory(), fromMemory, TestContext.CancellationToken).ConfigureAwait(false),
			$"{providerName} async should encode from memory to a stream");

		using MemoryStream source = new(original);
		byte[] selfAllocated = await encoder.EncodeAsync(source, TestContext.CancellationToken).ConfigureAwait(false);

		CollectionAssert.AreEqual(fromMemory.ToArray(), selfAllocated, $"{providerName} overloads should agree");

		using MemoryStream encoded = new(selfAllocated);
		byte[] decoded = await encoder.DecodeAsync(encoded, TestContext.CancellationToken).ConfigureAwait(false);
		CollectionAssert.AreEqual(original, decoded, $"{providerName} should decode what it encoded");
	}

	/// <summary>
	/// Tests that a cancelled token stops the async stream paths rather than being ignored.
	/// </summary>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_HonoursCancellation(IEncodingProvider encoder, string providerName)
	{
		byte[] original = new byte[40_000];
		using CancellationTokenSource cancelled = new();
		await cancelled.CancelAsync().ConfigureAwait(false);

		using MemoryStream input = new(original);
		using MemoryStream output = new();

		// Caught by base type on purpose: the framework may surface either OperationCanceledException
		// or its TaskCanceledException subclass, and which one depends on the stream implementation.
		// An exact-type assertion would be brittle here.
		bool observed = false;
		try
		{
			await encoder.TryEncodeAsync(input, output, cancelled.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			observed = true;
		}

		Assert.IsTrue(observed, $"{providerName} should observe a cancelled token");
	}

	/// <summary>
	/// A stream that returns one byte per read, however much is asked for.
	/// </summary>
	/// <param name="content">What the stream holds.</param>
	private sealed class DripStream(byte[] content) : MemoryStream(content)
	{
		public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));

		public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
			=> base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
	}
}
