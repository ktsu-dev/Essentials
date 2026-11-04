// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using ktsu.Essentials;
using ktsu.Essentials.EncodingProviders.Hex;
using ktsu.Essentials.HashProviders.FNV1_32;
using ktsu.Essentials.HashProviders.FNV1_64;
using ktsu.Essentials.HashProviders.FNV1a_32;
using ktsu.Essentials.HashProviders.FNV1a_64;
using ktsu.Essentials.ObfuscationProviders.BitRotate;
using ktsu.Essentials.ObfuscationProviders.Caesar;
using ktsu.Essentials.ObfuscationProviders.Xor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The FNV, Hex and byte-wise obfuscation stream paths read a chunk at a time rather than calling
/// <see cref="Stream.ReadByte"/> per byte (issue #91). Their output must be exactly what the span paths give, for
/// an empty stream, for one shorter than a chunk and for several chunks, read through a stream that returns short
/// reads and overrides neither <see cref="Stream.ReadByte"/> nor <see cref="Stream.WriteByte"/>.
/// </summary>
[TestClass]
public sealed class ChunkedStreamPathTests
{
	public static IEnumerable<object[]> Lengths => [[0], [1], [1000], [200_001]];

	public static IEnumerable<object[]> HashCases()
	{
		IHashProvider[] providers = [new FNV1_32HashProvider(), new FNV1a_32HashProvider(), new FNV1_64HashProvider(), new FNV1a_64HashProvider()];
		foreach (IHashProvider provider in providers)
		{
			foreach (object[] length in Lengths)
			{
				yield return [provider, provider.GetType().Name, length[0]];
			}
		}
	}

	public static IEnumerable<object[]> ObfuscationCases()
	{
		IObfuscationProvider[] providers =
		[
			new XorObfuscationProvider([0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77]),
			new CaesarObfuscationProvider(200),
			new BitRotateObfuscationProvider(5),
		];
		foreach (IObfuscationProvider provider in providers)
		{
			foreach (object[] length in Lengths)
			{
				yield return [provider, provider.GetType().Name, length[0]];
			}
		}
	}

	[TestMethod]
	[DynamicData(nameof(HashCases))]
	public void Hash_Stream_Path_Matches_The_Span_Path(IHashProvider provider, string name, int length)
	{
		byte[] payload = Payload(length);
		byte[] expected = provider.Hash(payload);

		using ShortReadStream source = new(payload);
		byte[] actual = new byte[provider.HashLengthBytes];
		Assert.IsTrue(provider.TryHash(source, actual, out int written), name);

		Assert.AreEqual(provider.HashLengthBytes, written, name);
		CollectionAssert.AreEqual(expected, actual, $"{name} hashed {length} streamed bytes differently");
	}

	[TestMethod]
	[DynamicData(nameof(Lengths))]
	public void Hex_Stream_Paths_Match_The_Span_Paths(int length)
	{
		IEncodingProvider hex = new HexEncodingProvider();
		byte[] payload = Payload(length);
		byte[] encoded = hex.Encode(payload);

		using ShortReadStream source = new(payload);
		using ShortReadStream sink = new([]);
		Assert.IsTrue(hex.TryEncode(source, sink));
		CollectionAssert.AreEqual(encoded, sink.ToArray(), "streamed encoding differs");

		using ShortReadStream encodedSource = new(encoded);
		using ShortReadStream decodedSink = new([]);
		Assert.IsTrue(hex.TryDecode(encodedSource, decodedSink));
		CollectionAssert.AreEqual(payload, decodedSink.ToArray(), "streamed decoding differs");
	}

	[TestMethod]
	public void Hex_Stream_Decode_Still_Rejects_Bad_Input()
	{
		IEncodingProvider hex = new HexEncodingProvider();
		byte[] encoded = hex.Encode(Payload(200_001));

		byte[] odd = encoded[..^1];
		using (ShortReadStream source = new(odd))
		using (ShortReadStream sink = new([]))
		{
			Assert.IsFalse(hex.TryDecode(source, sink), "an odd number of characters was accepted");
		}

		byte[] invalid = [.. encoded];
		invalid[150_000] = (byte)'g';
		using (ShortReadStream source = new(invalid))
		using (ShortReadStream sink = new([]))
		{
			Assert.IsFalse(hex.TryDecode(source, sink), "a character that is not hex was accepted");
		}
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationCases))]
	public void Obfuscation_Stream_Paths_Match_The_Span_Paths(IObfuscationProvider provider, string name, int length)
	{
		byte[] payload = Payload(length);
		byte[] obfuscated = provider.Obfuscate(payload);

		using ShortReadStream source = new(payload);
		using ShortReadStream sink = new([]);
		Assert.IsTrue(provider.TryObfuscate(source, sink), name);
		CollectionAssert.AreEqual(obfuscated, sink.ToArray(), $"{name} obfuscated {length} streamed bytes differently");

		using ShortReadStream obfuscatedSource = new(obfuscated);
		using ShortReadStream restored = new([]);
		Assert.IsTrue(provider.TryDeobfuscate(obfuscatedSource, restored), name);
		CollectionAssert.AreEqual(payload, restored.ToArray(), $"{name} deobfuscated {length} streamed bytes differently");
	}

	private static byte[] Payload(int length)
	{
		byte[] payload = new byte[length];
		for (int i = 0; i < length; i++)
		{
			payload[i] = (byte)((i * 31) ^ (i >> 7));
		}

		return payload;
	}

	/// <summary>
	/// A stream over a growable buffer whose reads return at most 1,000 bytes, and which leaves
	/// <see cref="Stream.ReadByte"/> and <see cref="Stream.WriteByte"/> to the base class, as most wrapping
	/// streams do.
	/// </summary>
	private sealed class ShortReadStream : Stream
	{
		private readonly MemoryStream inner = new();

		public ShortReadStream(byte[] content)
		{
			inner.Write(content, 0, content.Length);
			inner.Position = 0;
		}

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public byte[] ToArray() => inner.ToArray();

		public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 1000));

		public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				inner.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}
