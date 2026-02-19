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
		bool result = encoder.TryEncode(original, small);
		Assert.IsFalse(result, $"{providerName} should return false for insufficient buffer");
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
}
