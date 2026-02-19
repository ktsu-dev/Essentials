// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Common.Tests;

using System.Collections.Generic;
using System.Text;
using ktsu.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class RoundTripTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddCommon();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> CompressionProviders => BuildProvider().EnumerateProviders<ICompressionProvider>();

	public static IEnumerable<object[]> EncryptionProviders => BuildProvider().EnumerateProviders<IEncryptionProvider>();

	public static IEnumerable<object[]> ObfuscationProviders => BuildProvider().EnumerateProviders<IObfuscationProvider>();

	public static IEnumerable<object[]> SerializationProviders => BuildProvider().EnumerateProviders<ISerializationProvider>();

	public TestContext TestContext { get; set; } = null!;

	#region Compression Tests

	[TestMethod]
	[DynamicData(nameof(CompressionProviders))]
	public void Compression_Roundtrip_Bytes(ICompressionProvider compressor, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("hello world from " + providerName);

		byte[] compressed = compressor.Compress(original);
		Assert.IsGreaterThan(0, compressed.Length, $"{providerName} should produce compressed output");

		byte[] decompressed = compressor.Decompress(compressed);
		CollectionAssert.AreEqual(original, decompressed, $"{providerName} should decompress to original data");
	}

	[TestMethod]
	[DynamicData(nameof(CompressionProviders))]
	public void Compression_TryCompress_Span_Buffer_Size_Behavior(ICompressionProvider compressor, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("some data that will compress using " + providerName);
		Span<byte> smallDestination = stackalloc byte[4];
		bool smallResult = compressor.TryCompress(original, smallDestination);
		Assert.IsFalse(smallResult, $"{providerName} should return false for insufficient buffer");

		byte[] largeBuffer = new byte[original.Length * 10];
		bool largeResult = compressor.TryCompress(original, largeBuffer);
		Assert.IsTrue(largeResult, $"{providerName} should return true for sufficient buffer");
	}

	[TestMethod]
	[DynamicData(nameof(CompressionProviders))]
	public void Compression_TryDecompress_Span_From_Bytes(ICompressionProvider compressor, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("payload to compress and then decompress with " + providerName);
		byte[] compressed = compressor.Compress(original);
		byte[] destination = new byte[original.Length];
		bool ok = compressor.TryDecompress(compressed, destination);
		Assert.IsTrue(ok, $"{providerName} should successfully decompress");
		CollectionAssert.AreEqual(original, destination, $"{providerName} should produce original data");
	}

	[TestMethod]
	[DynamicData(nameof(CompressionProviders))]
	public void Compression_Stream_To_Stream_Roundtrip(ICompressionProvider compressor, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("stream roundtrip with " + providerName);
		using MemoryStream input = new(original);
		using MemoryStream compressed = new();
		bool compressedOk = compressor.TryCompress(input, compressed);
		Assert.IsTrue(compressedOk, $"{providerName} should successfully compress stream");
		compressed.Position = 0;
		using MemoryStream decompressed = new();
		bool decompressedOk = compressor.TryDecompress(compressed, decompressed);
		Assert.IsTrue(decompressedOk, $"{providerName} should successfully decompress stream");
		byte[] result = decompressed.ToArray();
		CollectionAssert.AreEqual(original, result, $"{providerName} should produce original data from stream");
	}

	[TestMethod]
	[DynamicData(nameof(CompressionProviders))]
	public void Compression_Async_Roundtrip(ICompressionProvider compressor, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("async compression with " + providerName);
		byte[] compressed = compressor.CompressAsync(original, TestContext.CancellationToken).Result;
		Assert.IsGreaterThan(0, compressed.Length, $"{providerName} async should produce compressed output");
		byte[] decompressed = compressor.DecompressAsync(compressed, TestContext.CancellationToken).Result;
		CollectionAssert.AreEqual(original, decompressed, $"{providerName} async should decompress to original data");
	}

	#endregion

	#region Encryption Tests

	[TestMethod]
	[DynamicData(nameof(EncryptionProviders))]
	public void Encryption_Roundtrip_Bytes(IEncryptionProvider encryptor, string providerName)
	{
		byte[] data = Encoding.UTF8.GetBytes("secret data for " + providerName);

		byte[] key = encryptor.GenerateKey();
		byte[] iv = encryptor.GenerateIV();

		byte[] encrypted = encryptor.Encrypt(data, key, iv);
		Assert.IsGreaterThan(0, encrypted.Length, $"{providerName} should produce encrypted output");

		byte[] decrypted = encryptor.Decrypt(encrypted, key, iv);
		CollectionAssert.AreEqual(data, decrypted, $"{providerName} should decrypt to original data");
	}

	[TestMethod]
	[DynamicData(nameof(EncryptionProviders))]
	public void Encryption_TryEncrypt_And_TryDecrypt_Span(IEncryptionProvider encryptor, string providerName)
	{
		byte[] data = Encoding.UTF8.GetBytes("span encrypt with " + providerName);
		byte[] key = encryptor.GenerateKey();
		byte[] iv = encryptor.GenerateIV();

		// Test insufficient buffer
		Span<byte> smallDest = stackalloc byte[4];
		bool small = encryptor.TryEncrypt(data, key, iv, smallDest);
		Assert.IsFalse(small, $"{providerName} should return false for insufficient encryption buffer");

		// Test sufficient buffer
		byte[] large = new byte[data.Length * 4];
		bool ok = encryptor.TryEncrypt(data, key, iv, large);
		Assert.IsTrue(ok, $"{providerName} should return true for sufficient encryption buffer");

		// Test decryption
		byte[] decryptedDest = new byte[data.Length * 4];
		bool decOk = encryptor.TryDecrypt(large, key, iv, decryptedDest);
		Assert.IsTrue(decOk, $"{providerName} should successfully decrypt");
		CollectionAssert.AreEqual(data, decryptedDest[..data.Length], $"{providerName} should produce original data");
	}

	[TestMethod]
	[DynamicData(nameof(EncryptionProviders))]
	public void Encryption_Stream_To_Stream_Roundtrip(IEncryptionProvider encryptor, string providerName)
	{
		byte[] plaintext = Encoding.UTF8.GetBytes("stream encryption with " + providerName);
		byte[] key = encryptor.GenerateKey();
		byte[] iv = encryptor.GenerateIV();

		using MemoryStream input = new(plaintext);
		using MemoryStream ciphertext = new();
		bool encOk = encryptor.TryEncrypt(input, key, iv, ciphertext);
		Assert.IsTrue(encOk, $"{providerName} should successfully encrypt stream");

		ciphertext.Position = 0;
		using MemoryStream decrypted = new();
		bool decOk = encryptor.TryDecrypt(ciphertext, key, iv, decrypted);
		Assert.IsTrue(decOk, $"{providerName} should successfully decrypt stream");

		byte[] result = decrypted.ToArray();
		CollectionAssert.AreEqual(plaintext, result, $"{providerName} should produce original data from stream");
	}

	[TestMethod]
	[DynamicData(nameof(EncryptionProviders))]
	public void Encryption_Async_Roundtrip(IEncryptionProvider encryptor, string providerName)
	{
		byte[] data = Encoding.UTF8.GetBytes("async encryption with " + providerName);
		byte[] key = encryptor.GenerateKey();
		byte[] iv = encryptor.GenerateIV();

		byte[] encrypted = encryptor.EncryptAsync(data, key, iv, TestContext.CancellationToken).Result;
		Assert.IsGreaterThan(0, encrypted.Length, $"{providerName} async should produce encrypted output");

		byte[] decrypted = encryptor.DecryptAsync(encrypted, key, iv, TestContext.CancellationToken).Result;
		CollectionAssert.AreEqual(data, decrypted, $"{providerName} async should decrypt to original data");
	}

	#endregion

	#region Obfuscation Tests

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Roundtrip(IObfuscationProvider obfuscator, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("obfuscate me with " + providerName);

		byte[] obfuscated = obfuscator.Obfuscate(original);
		Assert.IsGreaterThan(0, obfuscated.Length, $"{providerName} should produce obfuscated output");

		byte[] deobfuscated = obfuscator.Deobfuscate(obfuscated);
		CollectionAssert.AreEqual(original, deobfuscated, $"{providerName} should deobfuscate to original data");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_TryObfuscate_And_TryDeobfuscate_Span(IObfuscationProvider obfuscator, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("span obfuscate with " + providerName);

		// Test insufficient buffer
		Span<byte> small = stackalloc byte[4];
		bool smallOk = obfuscator.TryObfuscate(original, small);
		Assert.IsFalse(smallOk, $"{providerName} should return false for insufficient obfuscation buffer");

		// Test sufficient buffer
		byte[] dest = new byte[original.Length * 4];
		bool ok = obfuscator.TryObfuscate(original, dest);
		Assert.IsTrue(ok, $"{providerName} should return true for sufficient obfuscation buffer");

		// Test deobfuscation
		byte[] decodedDest = new byte[original.Length * 4];
		bool decOk = obfuscator.TryDeobfuscate(dest, decodedDest);
		Assert.IsTrue(decOk, $"{providerName} should successfully deobfuscate");
		CollectionAssert.AreEqual(original, decodedDest[..original.Length], $"{providerName} should produce original data");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Stream_To_Stream_Roundtrip(IObfuscationProvider obfuscator, string providerName)
	{
		byte[] input = Encoding.UTF8.GetBytes("stream obfuscation with " + providerName);

		using MemoryStream inputStream = new(input);
		using MemoryStream obfuscated = new();
		bool obOk = obfuscator.TryObfuscate(inputStream, obfuscated);
		Assert.IsTrue(obOk, $"{providerName} should successfully obfuscate stream");

		obfuscated.Position = 0;
		using MemoryStream deobfuscated = new();
		bool deobOk = obfuscator.TryDeobfuscate(obfuscated, deobfuscated);
		Assert.IsTrue(deobOk, $"{providerName} should successfully deobfuscate stream");

		byte[] result = deobfuscated.ToArray();
		CollectionAssert.AreEqual(input, result, $"{providerName} should produce original data from stream");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Async_Variants(IObfuscationProvider obfuscator, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("async obfuscation with " + providerName);

		using MemoryStream reader = new(original);
		using MemoryStream writer = new();
		bool ok = obfuscator.TryObfuscateAsync(reader, writer, TestContext.CancellationToken).Result;
		Assert.IsTrue(ok, $"{providerName} async should successfully obfuscate");

		// Reset writer position to beginning for reading
		writer.Position = 0;
		using MemoryStream round = new();
		bool deok = obfuscator.TryDeobfuscateAsync(writer, round, TestContext.CancellationToken).Result;
		Assert.IsTrue(deok, $"{providerName} async should successfully deobfuscate");

		byte[] result = round.ToArray();
		CollectionAssert.AreEqual(original, result, $"{providerName} async should produce original data");
	}

	#endregion

	#region Serialization Tests

	[TestMethod]
	[DynamicData(nameof(SerializationProviders))]
	public void Serialization_Can_Serialize_And_Deserialize(ISerializationProvider serializer, string providerName)
	{
		TestObject original = new() { Name = $"Test with {providerName}", Value = 42 };

		string json = serializer.Serialize(original);
		Assert.IsFalse(string.IsNullOrEmpty(json), $"{providerName} should produce serialized output");

		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
		TestObject? deserialized = serializer.Deserialize<TestObject>(jsonBytes);
		Assert.IsNotNull(deserialized, $"{providerName} should deserialize to non-null object");
		Assert.AreEqual(original.Name, deserialized.Name, $"{providerName} should preserve Name property");
		Assert.AreEqual(original.Value, deserialized.Value, $"{providerName} should preserve Value property");
	}

	[TestMethod]
	[DynamicData(nameof(SerializationProviders))]
	public void Serialization_Async_Variants(ISerializationProvider serializer, string providerName)
	{
		TestObject original = new() { Name = $"Async test with {providerName}", Value = 123 };

		// Use TrySerializeAsync with TextWriter
		using StringWriter writer = new();
		bool serializeResult = serializer.TrySerializeAsync(original, writer, TestContext.CancellationToken).Result;
		Assert.IsTrue(serializeResult, $"{providerName} async should serialize successfully");
		string json = writer.ToString();
		Assert.IsFalse(string.IsNullOrEmpty(json), $"{providerName} async should produce serialized output");

		// Use DeserializeAsync with ReadOnlyMemory<byte>
		byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
		TestObject? deserialized = serializer.DeserializeAsync<TestObject>(jsonBytes, TestContext.CancellationToken).Result;
		Assert.IsNotNull(deserialized, $"{providerName} async should deserialize to non-null object");
		Assert.AreEqual(original.Name, deserialized.Name, $"{providerName} async should preserve Name property");
		Assert.AreEqual(original.Value, deserialized.Value, $"{providerName} async should preserve Value property");
	}

	#endregion

	private sealed class TestObject
	{
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}
}
