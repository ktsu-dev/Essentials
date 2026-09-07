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

		using MemoryStream fromMemoryDecoded = new();
		Assert.IsTrue(
			await encoder.TryDecodeAsync(selfAllocated.AsMemory(), fromMemoryDecoded, TestContext.CancellationToken).ConfigureAwait(false),
			$"{providerName} async should decode from memory to a stream");
		CollectionAssert.AreEqual(original, fromMemoryDecoded.ToArray(), $"{providerName} overloads should agree on decoding");
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

	/// <summary>
	/// Tests that a null stream is reported rather than thrown, matching the synchronous paths.
	/// </summary>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_RejectsNullStreams(IEncodingProvider encoder, string providerName)
	{
		using MemoryStream real = new([1, 2, 3]);

		Assert.IsFalse(await encoder.TryEncodeAsync((Stream)null!, real, TestContext.CancellationToken).ConfigureAwait(false), providerName);
		Assert.IsFalse(await encoder.TryEncodeAsync(real, null!, TestContext.CancellationToken).ConfigureAwait(false), providerName);
		Assert.IsFalse(await encoder.TryDecodeAsync((Stream)null!, real, TestContext.CancellationToken).ConfigureAwait(false), providerName);
		Assert.IsFalse(await encoder.TryDecodeAsync(real, null!, TestContext.CancellationToken).ConfigureAwait(false), providerName);
	}

	/// <summary>
	/// Tests that input the provider cannot decode is reported rather than producing garbage.
	/// </summary>
	/// <remarks>
	/// Both cases are malformed for both providers: a single character is neither an even number of
	/// hex digits nor a whole Base64 quantum, and <c>z</c> is not a hex digit while <c>!</c> is not a
	/// Base64 character.
	/// </remarks>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_ReportsMalformedInput(IEncodingProvider encoder, string providerName)
	{
		foreach (string malformed in MalformedInputs)
		{
			using MemoryStream input = new(Encoding.UTF8.GetBytes(malformed));
			using MemoryStream output = new();

			Assert.IsFalse(
				await encoder.TryDecodeAsync(input, output, TestContext.CancellationToken).ConfigureAwait(false),
				$"{providerName} should refuse '{malformed}'");
		}
	}

	/// <summary>
	/// Input neither provider can decode: a lone character is neither an even number of hex digits
	/// nor a whole Base64 quantum, and the four-character case holds a character neither alphabet
	/// contains.
	/// </summary>
	private static readonly string[] MalformedInputs = ["z", "zzz!"];

	/// <summary>
	/// Tests that a stream failing mid-operation is reported rather than thrown out of the provider.
	/// </summary>
	/// <remarks>
	/// A stream that faults part-way is ordinary — a dropped connection, a full disk — and these are
	/// <c>Try</c> methods, so the failure belongs in the return value. Covers both the read side and
	/// the write side, since they are separate <c>await</c>s with separate failure modes.
	/// </remarks>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_ReportsAFailingStream(IEncodingProvider encoder, string providerName)
	{
		byte[] payload = Encoding.UTF8.GetBytes("something to encode");

		using (FailingStream unreadable = new())
		using (MemoryStream output = new())
		{
			Assert.IsFalse(
				await encoder.TryEncodeAsync(unreadable, output, TestContext.CancellationToken).ConfigureAwait(false),
				$"{providerName} should report a source that fails to read");
		}

		using (MemoryStream input = new(payload))
		using (FailingStream unwritable = new())
		{
			Assert.IsFalse(
				await encoder.TryEncodeAsync(input, unwritable, TestContext.CancellationToken).ConfigureAwait(false),
				$"{providerName} should report a destination that fails to write");
		}
	}

	/// <summary>
	/// Tests that a stream disposed before use is reported rather than thrown.
	/// </summary>
	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_Async_ReportsADisposedStream(IEncodingProvider encoder, string providerName)
	{
		MemoryStream disposed = await ClosedStreamAsync().ConfigureAwait(false);
		using MemoryStream output = new();

		Assert.IsFalse(
			await encoder.TryEncodeAsync(disposed, output, TestContext.CancellationToken).ConfigureAwait(false),
			$"{providerName} should report a disposed source");
	}

	/// <summary>
	/// Builds a stream that has already been closed.
	/// </summary>
	/// <returns>A disposed stream.</returns>
	/// <remarks>
	/// The disposal happens here rather than in the test body so that nothing the test holds is an
	/// undisposed resource: a caller receives an object whose lifetime is already over and owns
	/// nothing. Expressing it inline is what turns awkward — a disposed local still looks like a leak
	/// to disposal analysis, and the shapes that convince it collide with the rules about awaiting.
	/// </remarks>
	private static async Task<MemoryStream> ClosedStreamAsync()
	{
		MemoryStream stream = new([1, 2, 3, 4]);
		await stream.DisposeAsync().ConfigureAwait(false);
		return stream;
	}

	/// <summary>
	/// A stream that fails whichever way it is used.
	/// </summary>
	private sealed class FailingStream : Stream
	{
		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush()
		{
		}

		public override int Read(byte[] buffer, int offset, int count) => throw new IOException("read failed");

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
			=> throw new IOException("read failed");

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new IOException("write failed");

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
			=> throw new IOException("write failed");
	}
}
