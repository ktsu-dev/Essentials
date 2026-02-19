// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Text;
using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class HashProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddHashProviders();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> HashProviders => BuildProvider().EnumerateProviders<IHashProvider>();

	public TestContext TestContext { get; set; } = null!;

	#region Generic Hash Provider Tests

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_Produce_Correct_Length(IHashProvider provider, string providerName)
	{
		byte[] data = Encoding.UTF8.GetBytes("hash me with " + providerName);
		byte[] result = provider.Hash(data);
		Assert.HasCount(provider.HashLengthBytes, result, $"{providerName} should produce hash of correct length");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2014:Do not use stackalloc in loops", Justification = "Test code")]
	public void HashProviders_TryHash_Span_And_Stream(IHashProvider provider, string providerName)
	{
		byte[] data = Encoding.UTF8.GetBytes("hash inputs with " + providerName);

		// Test insufficient buffer
		Span<byte> tooSmall = stackalloc byte[Math.Max(1, provider.HashLengthBytes - 1)];
		bool smallOk = provider.TryHash(data, tooSmall);
		Assert.IsFalse(smallOk, $"{providerName} should return false for insufficient buffer");

		// Test sufficient buffer for byte array
		byte[] dest = new byte[provider.HashLengthBytes];
		bool ok = provider.TryHash(data, dest);
		Assert.IsTrue(ok, $"{providerName} should return true for sufficient buffer");

		// Test stream hashing
		using MemoryStream stream = new(data);
		Array.Fill(dest, (byte)0);
		bool streamOk = provider.TryHash(stream, dest);
		Assert.IsTrue(streamOk, $"{providerName} should hash streams successfully");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_Async_Variants(IHashProvider provider, string providerName)
	{
		byte[] data = Encoding.UTF8.GetBytes("async hash with " + providerName);

		// Test HashAsync
		byte[] result = provider.HashAsync(data, TestContext.CancellationToken).Result;
		Assert.HasCount(provider.HashLengthBytes, result, $"{providerName} async should produce correct length");

		// Test TryHashAsync with byte array
		byte[] dest = new byte[provider.HashLengthBytes];
		bool tryOk = provider.TryHashAsync(data, dest, TestContext.CancellationToken).Result;
		Assert.IsTrue(tryOk, $"{providerName} async should return true for sufficient buffer");

		// Test TryHashAsync with stream
		using MemoryStream stream = new(data);
		Array.Fill(dest, (byte)0);
		bool tryStreamOk = provider.TryHashAsync(stream, dest, TestContext.CancellationToken).Result;
		Assert.IsTrue(tryStreamOk, $"{providerName} async should hash streams successfully");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_String_Hashing_Works(IHashProvider provider, string providerName)
	{
		string testString = "string hash test with " + providerName;
		byte[] result = provider.Hash(testString);
		Assert.HasCount(provider.HashLengthBytes, result, $"{providerName} should produce correct length for string input");
		Assert.IsTrue(result.Any(b => b != 0), $"{providerName} should not produce all zero hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_Empty_Input_Produces_Valid_Hash(IHashProvider provider, string providerName)
	{
		byte[] emptyInput = [];
		byte[] result = provider.Hash(emptyInput);
		Assert.HasCount(provider.HashLengthBytes, result, $"{providerName} should produce correct length for empty input");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_Deterministic_Results(IHashProvider provider, string providerName)
	{
		byte[] testData = Encoding.UTF8.GetBytes("deterministic test with " + providerName);
		byte[] hash1 = provider.Hash(testData);
		byte[] hash2 = provider.Hash(testData);
		CollectionAssert.AreEqual(hash1, hash2, $"{providerName} should produce deterministic results");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_Different_Inputs_Produce_Different_Hashes(IHashProvider provider, string providerName)
	{
		byte[] input1 = Encoding.UTF8.GetBytes("input one for " + providerName);
		byte[] input2 = Encoding.UTF8.GetBytes("input two for " + providerName);

		byte[] hash1 = provider.Hash(input1);
		byte[] hash2 = provider.Hash(input2);
		CollectionAssert.AreNotEqual(hash1, hash2, $"{providerName} should produce different hashes for different inputs");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_Large_Input_Handling(IHashProvider provider, string providerName)
	{
		// Create a large input (1MB)
		byte[] largeInput = new byte[1024 * 1024];
		Random.Shared.NextBytes(largeInput);

		byte[] result = provider.Hash(largeInput);
		Assert.HasCount(provider.HashLengthBytes, result, $"{providerName} should handle large inputs correctly");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void HashProviders_Stream_Hashing_Matches_Byte_Array(IHashProvider provider, string providerName)
	{
		byte[] testData = Encoding.UTF8.GetBytes("stream vs byte array test for " + providerName);

		byte[] arrayHash = provider.Hash(testData);

		using MemoryStream stream = new(testData);
		byte[] streamHash = provider.Hash(stream);

		CollectionAssert.AreEqual(arrayHash, streamHash, $"{providerName} should produce same hash for stream and byte array");
	}

	#endregion

	#region Known Vector Tests

	[TestMethod]
	public void HashProviders_MD5_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? md5 = hashes.FirstOrDefault(h => h.GetType().Name == "MD5");
		Assert.IsNotNull(md5);

		// MD5 of "abc" should be 900150983cd24fb0d6963f7d28e17f72
		byte[] input = Encoding.UTF8.GetBytes("abc");
		byte[] result = md5!.Hash(input);
		string hex = Convert.ToHexString(result).ToLowerInvariant();
		Assert.AreEqual("900150983cd24fb0d6963f7d28e17f72", hex);
	}

	[TestMethod]
	public void HashProviders_SHA1_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? sha1 = hashes.FirstOrDefault(h => h.GetType().Name == "SHA1");
		Assert.IsNotNull(sha1);

		// SHA1 of "abc" should be a9993e364706816aba3e25717850c26c9cd0d89d
		byte[] input = Encoding.UTF8.GetBytes("abc");
		byte[] result = sha1!.Hash(input);
		string hex = Convert.ToHexString(result).ToLowerInvariant();
		Assert.AreEqual("a9993e364706816aba3e25717850c26c9cd0d89d", hex);
	}

	[TestMethod]
	public void HashProviders_SHA256_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? sha256 = hashes.FirstOrDefault(h => h.GetType().Name == "SHA256");
		Assert.IsNotNull(sha256);

		// SHA256 of "abc" should be ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
		byte[] input = Encoding.UTF8.GetBytes("abc");
		byte[] result = sha256!.Hash(input);
		string hex = Convert.ToHexString(result).ToLowerInvariant();
		Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hex);
	}

	[TestMethod]
	public void HashProviders_SHA384_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? sha384 = hashes.FirstOrDefault(h => h.GetType().Name == "SHA384");
		Assert.IsNotNull(sha384);

		// SHA384 of "abc" should be cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7
		byte[] input = Encoding.UTF8.GetBytes("abc");
		byte[] result = sha384!.Hash(input);
		string hex = Convert.ToHexString(result).ToLowerInvariant();
		Assert.AreEqual("cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7", hex);
	}

	[TestMethod]
	public void HashProviders_SHA512_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? sha512 = hashes.FirstOrDefault(h => h.GetType().Name == "SHA512");
		Assert.IsNotNull(sha512);

		// SHA512 of "abc" should be ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f
		byte[] input = Encoding.UTF8.GetBytes("abc");
		byte[] result = sha512!.Hash(input);
		string hex = Convert.ToHexString(result).ToLowerInvariant();
		Assert.AreEqual("ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f", hex);
	}

	[TestMethod]
	public void HashProviders_FNV1_32_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? fnv1_32 = hashes.FirstOrDefault(h => h.GetType().Name == "FNV1_32");
		Assert.IsNotNull(fnv1_32);

		// FNV-1 32-bit of "hello" produces 0xb7049f97 (3069866343)
		byte[] input = Encoding.UTF8.GetBytes("hello");
		byte[] result = fnv1_32!.Hash(input);
		uint hash = BitConverter.ToUInt32(result, 0);
		Assert.AreEqual(3069866343u, hash);
	}

	[TestMethod]
	public void HashProviders_FNV1a_32_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? fnv1a_32 = hashes.FirstOrDefault(h => h.GetType().Name == "FNV1a_32");
		Assert.IsNotNull(fnv1a_32);

		// FNV-1a 32-bit of "hello" produces 0x4f9f2cab (1335831723)
		byte[] input = Encoding.UTF8.GetBytes("hello");
		byte[] result = fnv1a_32!.Hash(input);
		uint hash = BitConverter.ToUInt32(result, 0);
		Assert.AreEqual(0x4f9f2cabu, hash);
	}

	[TestMethod]
	public void HashProviders_FNV1_64_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? fnv1_64 = hashes.FirstOrDefault(h => h.GetType().Name == "FNV1_64");
		Assert.IsNotNull(fnv1_64);

		// FNV-1 64-bit of "hello" produces 0x7b495389bdbdd4c7 (8883723591023973575)
		byte[] input = Encoding.UTF8.GetBytes("hello");
		byte[] result = fnv1_64!.Hash(input);
		ulong hash = BitConverter.ToUInt64(result, 0);
		Assert.AreEqual(0x7b495389bdbdd4c7ul, hash);
	}

	[TestMethod]
	public void HashProviders_FNV1a_64_Known_Vector()
	{
		using ServiceProvider serviceProvider = BuildProvider();
		IEnumerable<IHashProvider> hashes = serviceProvider.GetServices<IHashProvider>();
		IHashProvider? fnv1a_64 = hashes.FirstOrDefault(h => h.GetType().Name == "FNV1a_64");
		Assert.IsNotNull(fnv1a_64);

		// FNV-1a 64-bit of "hello" produces 0xa430d84680aabd0b (11831194018420276491)
		byte[] input = Encoding.UTF8.GetBytes("hello");
		byte[] result = fnv1a_64!.Hash(input);
		ulong hash = BitConverter.ToUInt64(result, 0);
		Assert.AreEqual(0xa430d84680aabd0bul, hash);
	}

	#endregion
}
