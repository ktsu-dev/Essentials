// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.IO;
using System.Text;
using System.Threading.Tasks;
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
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		byte[] expected = new byte[provider.HashLengthBytes];
		Assert.IsTrue(provider.TryHash(FakeKey, FakePayload, expected, out int written));
		Assert.AreEqual(provider.HashLengthBytes, written);

		byte[] actual = provider.Hash(FakeKey, FakePayload);

		CollectionAssert.AreEqual(expected, actual);
	}

	[TestMethod]
	public void Defaults_Hash_Stream_Matches_Hash_Span()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		using MemoryStream stream = new(FakePayload);

		byte[] fromStream = provider.Hash(FakeKey, stream);

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), fromStream);
	}

	[TestMethod]
	public void Defaults_Hash_String_Matches_Utf8_Bytes()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();

		byte[] fromString = provider.Hash(FakeKey, "the quick brown fox");

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), fromString);
	}

	[TestMethod]
	public void Defaults_CreateIncremental_Matches_One_Shot()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		using IIncrementalHash incremental = provider.CreateIncremental(FakeKey);
		incremental.Append(FakePayload.AsSpan(0, 5));
		incremental.Append(FakePayload.AsSpan(5));

		byte[] actual = incremental.GetHashAndReset();

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public async Task Defaults_TryHashAsync_Matches_One_Shot()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		using MemoryStream stream = new(FakePayload);
		byte[] actual = new byte[provider.HashLengthBytes];

		bool ok = await provider.TryHashAsync(FakeKey, stream, actual).ConfigureAwait(false);

		Assert.IsTrue(ok);
		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public async Task Defaults_HashAsync_Memory_Matches_One_Shot()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();

		byte[] actual = await provider.HashAsync(FakeKey, FakePayload).ConfigureAwait(false);

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public async Task Defaults_HashAsync_Stream_Matches_One_Shot()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		using MemoryStream stream = new(FakePayload);

		byte[] actual = await provider.HashAsync(FakeKey, stream).ConfigureAwait(false);

		CollectionAssert.AreEqual(provider.Hash(FakeKey, FakePayload), actual);
	}

	[TestMethod]
	public void Defaults_TryHash_Rejects_Undersized_Destination()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		byte[] tooSmall = new byte[provider.HashLengthBytes - 1];

		Assert.IsFalse(provider.TryHash(FakeKey, FakePayload, tooSmall, out int written));
		Assert.AreEqual(0, written);
	}

	[TestMethod]
	public void Defaults_Verify_Accepts_Correct_Tag()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		byte[] tag = provider.Hash(FakeKey, FakePayload);

		Assert.IsTrue(provider.Verify(FakeKey, FakePayload, tag));
	}

	[TestMethod]
	public void Defaults_Verify_Rejects_Flipped_Tag_Bit()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		byte[] tag = provider.Hash(FakeKey, FakePayload);
		tag[0] ^= 0x01;

		Assert.IsFalse(provider.Verify(FakeKey, FakePayload, tag));
	}

	[TestMethod]
	public void Defaults_Verify_Rejects_Wrong_Key()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		byte[] tag = provider.Hash(FakeKey, FakePayload);
		byte[] wrongKey = Encoding.UTF8.GetBytes("other-key");

		Assert.IsFalse(provider.Verify(wrongKey, FakePayload, tag));
	}

	[TestMethod]
	public void Defaults_Verify_Rejects_Truncated_Tag()
	{
		IKeyedHashProvider provider = new FakeKeyedHashProvider();
		byte[] tag = provider.Hash(FakeKey, FakePayload);

		Assert.IsFalse(provider.Verify(FakeKey, FakePayload, tag.AsSpan(0, tag.Length - 1)));
	}

	#endregion
}
