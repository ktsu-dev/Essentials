// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.All;
using ktsu.Essentials.KeyedHashProviders.HmacSha256;
using ktsu.Essentials.KeyedHashProviders.HmacSha384;
using ktsu.Essentials.KeyedHashProviders.HmacSha512;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class KeyedHashProviderTests
{
	#region FixedTimeEquals

	[TestMethod]
	public void FixedTimeEquals_Matches_Identical_Spans()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3, 4];

		Assert.IsTrue(FixedTimeComparison.FixedTimeEquals(left, right));
	}

	[TestMethod]
	public void FixedTimeEquals_Rejects_Single_Bit_Difference()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3, 5];

		Assert.IsFalse(FixedTimeComparison.FixedTimeEquals(left, right));
	}

	[TestMethod]
	public void FixedTimeEquals_Rejects_Different_Lengths()
	{
		byte[] left = [1, 2, 3, 4];
		byte[] right = [1, 2, 3];

		Assert.IsFalse(FixedTimeComparison.FixedTimeEquals(left, right));
	}

	[TestMethod]
	public void FixedTimeEquals_Matches_Empty_Spans()
	{
		Assert.IsTrue(FixedTimeComparison.FixedTimeEquals([], []));
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

	#region HMAC-SHA256 known answer vectors

	private static byte[] FromHex(string hex)
	{
		byte[] bytes = new byte[hex.Length / 2];
		for (int i = 0; i < bytes.Length; i++)
		{
			bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}

		return bytes;
	}

	[TestMethod]
	public void HmacSha256_Rfc4231_Case1()
	{
		IKeyedHashProvider provider = new HmacSha256KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0x0b, 20)];
		byte[] data = Encoding.UTF8.GetBytes("Hi There");

		byte[] actual = provider.Hash(key, data);

		CollectionAssert.AreEqual(
			FromHex("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7"),
			actual);
	}

	[TestMethod]
	public void HmacSha256_Rfc4231_Case2()
	{
		IKeyedHashProvider provider = new HmacSha256KeyedHashProvider();
		byte[] key = Encoding.UTF8.GetBytes("Jefe");
		byte[] data = Encoding.UTF8.GetBytes("what do ya want for nothing?");

		byte[] actual = provider.Hash(key, data);

		CollectionAssert.AreEqual(
			FromHex("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843"),
			actual);
	}

	[TestMethod]
	public void HmacSha256_Rfc4231_Case6_Oversized_Key()
	{
		IKeyedHashProvider provider = new HmacSha256KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0xaa, 131)];
		byte[] data = Encoding.UTF8.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First");

		byte[] actual = provider.Hash(key, data);

		CollectionAssert.AreEqual(
			FromHex("60e431591ee0b67f0d8a26aacbf5b77f8e0bc6213728c5140546040f0ee37f54"),
			actual);
	}

	[TestMethod]
	public void HmacSha256_Agrees_With_Bcl()
	{
		IKeyedHashProvider provider = new HmacSha256KeyedHashProvider();
		byte[] key = Encoding.UTF8.GetBytes("a key of some length");
		byte[] data = Encoding.UTF8.GetBytes("a payload to authenticate");

		byte[] actual = provider.Hash(key, data);

		using HMACSHA256 reference = new(key);
		CollectionAssert.AreEqual(reference.ComputeHash(data), actual);
	}

	[TestMethod]
	public void HmacSha256_All_Four_Paths_Agree()
	{
		IKeyedHashProvider provider = new HmacSha256KeyedHashProvider();
		byte[] key = Encoding.UTF8.GetBytes("agreement key");
		byte[] data = Encoding.UTF8.GetBytes("a payload long enough to span several appends");
		byte[] oneShot = provider.Hash(key, data);

		using MemoryStream stream = new(data);
		byte[] fromStream = provider.Hash(key, stream);

		using IIncrementalHash incremental = provider.CreateIncremental(key);
		incremental.Append(data.AsSpan(0, 7));
		incremental.Append(data.AsSpan(7, 20));
		incremental.Append(data.AsSpan(27));
		byte[] fromIncremental = incremental.GetHashAndReset();

		CollectionAssert.AreEqual(oneShot, fromStream);
		CollectionAssert.AreEqual(oneShot, fromIncremental);
	}

	[TestMethod]
	public async Task HmacSha256_Async_Agrees_With_One_Shot()
	{
		IKeyedHashProvider provider = new HmacSha256KeyedHashProvider();
		byte[] key = Encoding.UTF8.GetBytes("async key");
		byte[] data = Encoding.UTF8.GetBytes("a payload to authenticate asynchronously");
		using MemoryStream stream = new(data);

		byte[] fromAsync = await provider.HashAsync(key, stream).ConfigureAwait(false);

		CollectionAssert.AreEqual(provider.Hash(key, data), fromAsync);
	}

	[TestMethod]
	public void HmacSha256_Reports_Exact_Length_And_Leaves_Tail_Untouched()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] key = Encoding.UTF8.GetBytes("contract key");
		byte[] data = Encoding.UTF8.GetBytes("contract payload");
		byte[] buffer = new byte[provider.HashLengthBytes + 16];
		buffer.AsSpan().Fill(0xCD);

		Assert.IsTrue(provider.TryHash(key, data, buffer, out int written));

		Assert.AreEqual(provider.HashLengthBytes, written);
		foreach (byte b in buffer.AsSpan(written).ToArray())
		{
			Assert.AreEqual(0xCD, b, "the tail of the caller's buffer must not be touched");
		}
	}

	#endregion

	#region HMAC-SHA384 and HMAC-SHA512 known answer vectors

	[TestMethod]
	public void HmacSha384_Rfc4231_Case1()
	{
		IKeyedHashProvider provider = new HmacSha384KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0x0b, 20)];

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Hi There"));

		CollectionAssert.AreEqual(
			FromHex("afd03944d84895626b0825f4ab46907f15f9dadbe4101ec682aa034c7cebc59cfaea9ea9076ede7f4af152e8b2fa9cb6"),
			actual);
	}

	[TestMethod]
	public void HmacSha384_Rfc4231_Case2()
	{
		IKeyedHashProvider provider = new HmacSha384KeyedHashProvider();
		byte[] key = Encoding.UTF8.GetBytes("Jefe");

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("what do ya want for nothing?"));

		CollectionAssert.AreEqual(
			FromHex("af45d2e376484031617f78d2b58a6b1b9c7ef464f5a01b47e42ec3736322445e8e2240ca5e69e2c78b3239ecfab21649"),
			actual);
	}

	[TestMethod]
	public void HmacSha384_Rfc4231_Case6_Oversized_Key()
	{
		IKeyedHashProvider provider = new HmacSha384KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0xaa, 131)];

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First"));

		CollectionAssert.AreEqual(
			FromHex("4ece084485813e9088d2c63a041bc5b44f9ef1012a2b588f3cd11f05033ac4c60c2ef6ab4030fe8296248df163f44952"),
			actual);
	}

	[TestMethod]
	public void HmacSha512_Rfc4231_Case1()
	{
		IKeyedHashProvider provider = new HmacSha512KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0x0b, 20)];

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Hi There"));

		CollectionAssert.AreEqual(
			FromHex("87aa7cdea5ef619d4ff0b4241a1d6cb02379f4e2ce4ec2787ad0b30545e17cdedaa833b7d6b8a702038b274eaea3f4e4be9d914eeb61f1702e696c203a126854"),
			actual);
	}

	[TestMethod]
	public void HmacSha512_Rfc4231_Case2()
	{
		IKeyedHashProvider provider = new HmacSha512KeyedHashProvider();
		byte[] key = Encoding.UTF8.GetBytes("Jefe");

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("what do ya want for nothing?"));

		CollectionAssert.AreEqual(
			FromHex("164b7a7bfcf819e2e395fbe73b56e0a387bd64222e831fd610270cd7ea2505549758bf75c05a994a6d034f65f8f0e6fdcaeab1a34d4a6b4b636e070a38bce737"),
			actual);
	}

	[TestMethod]
	public void HmacSha512_Rfc4231_Case6_Oversized_Key()
	{
		IKeyedHashProvider provider = new HmacSha512KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0xaa, 131)];

		byte[] actual = provider.Hash(key, Encoding.UTF8.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First"));

		CollectionAssert.AreEqual(
			FromHex("80b24263c7c1a3ebb71493c1dd7be8b49b46d1f41b4aeec1121b013783f8f3526b56d037e05f2598bd0fd2215d6a1e5295e64f73f63f0aec8b915a985d786598"),
			actual);
	}

	[TestMethod]
	public void HmacSha384_And_512_Report_Their_Tag_Lengths()
	{
		Assert.AreEqual(48, new HmacSha384KeyedHashProvider().HashLengthBytes);
		Assert.AreEqual(64, new HmacSha512KeyedHashProvider().HashLengthBytes);
	}

	[TestMethod]
	public void HmacSha384_Stream_And_Incremental_Paths_Agree_With_Rfc4231()
	{
		IKeyedHashProvider provider = new HmacSha384KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0x0b, 20)];
		byte[] data = Encoding.UTF8.GetBytes("Hi There");
		byte[] expected = FromHex("afd03944d84895626b0825f4ab46907f15f9dadbe4101ec682aa034c7cebc59cfaea9ea9076ede7f4af152e8b2fa9cb6");

		using MemoryStream stream = new(data);
		byte[] fromStream = provider.Hash(key, stream);

		using IIncrementalHash incremental = provider.CreateIncremental(key);
		incremental.Append(data.AsSpan(0, 2));
		incremental.Append(data.AsSpan(2));
		byte[] fromIncremental = incremental.GetHashAndReset();

		CollectionAssert.AreEqual(expected, fromStream);
		CollectionAssert.AreEqual(expected, fromIncremental);
	}

	[TestMethod]
	public void HmacSha512_Stream_And_Incremental_Paths_Agree_With_Rfc4231()
	{
		IKeyedHashProvider provider = new HmacSha512KeyedHashProvider();
		byte[] key = [.. Enumerable.Repeat((byte)0x0b, 20)];
		byte[] data = Encoding.UTF8.GetBytes("Hi There");
		byte[] expected = FromHex("87aa7cdea5ef619d4ff0b4241a1d6cb02379f4e2ce4ec2787ad0b30545e17cdedaa833b7d6b8a702038b274eaea3f4e4be9d914eeb61f1702e696c203a126854");

		using MemoryStream stream = new(data);
		byte[] fromStream = provider.Hash(key, stream);

		using IIncrementalHash incremental = provider.CreateIncremental(key);
		incremental.Append(data.AsSpan(0, 2));
		incremental.Append(data.AsSpan(2));
		byte[] fromIncremental = incremental.GetHashAndReset();

		CollectionAssert.AreEqual(expected, fromStream);
		CollectionAssert.AreEqual(expected, fromIncremental);
	}

	#endregion

	#region Dependency injection

	[TestMethod]
	public void AddKeyedHashProviders_Registers_All_Three()
	{
		ServiceCollection services = new();
		services.AddKeyedHashProviders();
		using ServiceProvider provider = services.BuildServiceProvider();

		IKeyedHashProvider[] providers = [.. provider.GetServices<IKeyedHashProvider>()];

		Assert.AreEqual(3, providers.Length);
		Assert.AreEqual(1, providers.Count(p => p.HashLengthBytes == 32));
		Assert.AreEqual(1, providers.Count(p => p.HashLengthBytes == 48));
		Assert.AreEqual(1, providers.Count(p => p.HashLengthBytes == 64));
	}

	[TestMethod]
	public void AddKeyedHashProviders_Resolves_Concrete_Types()
	{
		ServiceCollection services = new();
		services.AddKeyedHashProviders();
		using ServiceProvider provider = services.BuildServiceProvider();

		Assert.IsNotNull(provider.GetService<HmacSha256KeyedHashProvider>());
		Assert.IsNotNull(provider.GetService<HmacSha384KeyedHashProvider>());
		Assert.IsNotNull(provider.GetService<HmacSha512KeyedHashProvider>());
	}

	[TestMethod]
	public void AddEssentials_Includes_Keyed_Hash_Providers()
	{
		ServiceCollection services = new();
		services.AddEssentials();
		using ServiceProvider provider = services.BuildServiceProvider();

		Assert.AreEqual(3, provider.GetServices<IKeyedHashProvider>().Count());
	}

	#endregion
}
