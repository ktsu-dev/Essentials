// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.EncryptionProviders.Aes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests applied uniformly to every registered provider, across every overload shape.
/// </summary>
/// <remarks>
/// The per-provider test classes cover behaviour specific to one implementation. This class covers the
/// promises the interfaces make to every implementation — in particular that each transform round-trips
/// through <em>all</em> of its entry points, not just the byte-array one. The string overloads went
/// untested for a long time and silently corrupted data; the payloads below deliberately include content
/// whose transformed bytes are not valid UTF8, which is what made that corruption possible.
/// </remarks>
[TestClass]
public class ProviderContractTests
{
	/// <summary>The Unicode replacement character, the signature of a lossy byte-to-text conversion.</summary>
	private const char ReplacementChar = '�';

	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddCommon();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> CompressionProviders => BuildProvider().EnumerateProviders<ICompressionProvider>();

	public static IEnumerable<object[]> ObfuscationProviders => BuildProvider().EnumerateProviders<IObfuscationProvider>();

	public static IEnumerable<object[]> EncodingProviders => BuildProvider().EnumerateProviders<IEncodingProvider>();

	public static IEnumerable<object[]> EncryptionProviders => BuildProvider().EnumerateProviders<IEncryptionProvider>();

	public static IEnumerable<object[]> HashProviders => BuildProvider().EnumerateProviders<IHashProvider>();

	public TestContext TestContext { get; set; } = null!;

	/// <summary>
	/// Payloads chosen so their transformed bytes are unlikely to be valid UTF8. Plain ASCII is included
	/// because even that compresses to bytes containing 0x8B, which is not a legal UTF8 start byte.
	/// </summary>
	private static IEnumerable<string> Payloads =>
	[
		"Hello, World!",
		"a",
		"héllo wörld 日本語 🎉 — mixed multi-byte content",
		new string('x', 4096),
		"line one\r\nline two\ttabbed\0embedded null",
	];

	#region String round-trips

	[TestMethod]
	[DynamicData(nameof(CompressionProviders))]
	public void Compression_String_Roundtrips(ICompressionProvider compressor, string providerName)
	{
		foreach (string original in Payloads)
		{
			string compressed = compressor.Compress(original);
			AssertLosslessText(compressed, providerName, nameof(ICompressionProvider.Compress));

			string restored = compressor.Decompress(compressed);
			Assert.AreEqual(original, restored, $"{providerName} should restore the original string exactly");
		}
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_String_Roundtrips(IObfuscationProvider obfuscator, string providerName)
	{
		foreach (string original in Payloads)
		{
			string obfuscated = obfuscator.Obfuscate(original);
			AssertLosslessText(obfuscated, providerName, nameof(IObfuscationProvider.Obfuscate));

			string restored = obfuscator.Deobfuscate(obfuscated);
			Assert.AreEqual(original, restored, $"{providerName} should restore the original string exactly");
		}
	}

	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public void Encoding_String_Roundtrips(IEncodingProvider encoder, string providerName)
	{
		foreach (string original in Payloads)
		{
			string encoded = encoder.Encode(original);
			string restored = encoder.Decode(encoded);

			Assert.AreEqual(original, restored, $"{providerName} should restore the original string exactly");
		}
	}

	[TestMethod]
	[DynamicData(nameof(EncryptionProviders))]
	public void Encryption_String_Roundtrips(IEncryptionProvider encryptor, string providerName)
	{
		byte[] key = encryptor.GenerateKey();
		byte[] iv = encryptor.GenerateIV();

		foreach (string original in Payloads)
		{
			string encrypted = encryptor.Encrypt(original, key, iv);
			AssertLosslessText(encrypted, providerName, nameof(IEncryptionProvider.Encrypt));

			string restored = encryptor.Decrypt(encrypted, key, iv);
			Assert.AreEqual(original, restored, $"{providerName} should restore the original string exactly");
		}
	}

	/// <summary>
	/// Asserts that a string returned from a binary transform survived as text.
	/// </summary>
	/// <remarks>
	/// A U+FFFD anywhere in the output means the bytes were decoded as UTF8 rather than encoded for
	/// transport, which destroys them irreversibly. Valid Base64 confirms the intended representation.
	/// </remarks>
	private static void AssertLosslessText(string produced, string providerName, string operation)
	{
		Assert.IsFalse(
			produced.Contains(ReplacementChar, StringComparison.Ordinal),
			$"{providerName}.{operation} produced U+FFFD, meaning binary output was decoded as UTF8 and corrupted");

		Assert.IsTrue(
			IsBase64(produced),
			$"{providerName}.{operation} should return Base64 text so binary output survives as a string");
	}

	private static bool IsBase64(string value)
	{
		Span<byte> scratch = new byte[((value.Length / 4) + 1) * 3];
		return Convert.TryFromBase64String(value, scratch, out _);
	}

	#endregion

	#region Async string round-trips

	[TestMethod]
	[DynamicData(nameof(CompressionProviders))]
	public async Task Compression_String_Async_Roundtrips(ICompressionProvider compressor, string providerName)
	{
		const string original = "async string compression round-trip";

		string compressed = await compressor.CompressAsync(original, TestContext.CancellationToken).ConfigureAwait(false);
		string restored = await compressor.DecompressAsync(compressed, TestContext.CancellationToken).ConfigureAwait(false);

		Assert.AreEqual(original, restored, $"{providerName} async should restore the original string exactly");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public async Task Obfuscation_String_Async_Roundtrips(IObfuscationProvider obfuscator, string providerName)
	{
		const string original = "async string obfuscation round-trip";

		string obfuscated = await obfuscator.ObfuscateAsync(original, TestContext.CancellationToken).ConfigureAwait(false);
		string restored = await obfuscator.DeobfuscateAsync(obfuscated, TestContext.CancellationToken).ConfigureAwait(false);

		Assert.AreEqual(original, restored, $"{providerName} async should restore the original string exactly");
	}

	[TestMethod]
	[DynamicData(nameof(EncodingProviders))]
	public async Task Encoding_String_Async_Roundtrips(IEncodingProvider encoder, string providerName)
	{
		const string original = "async string encoding round-trip";

		string encoded = await encoder.EncodeAsync(original, TestContext.CancellationToken).ConfigureAwait(false);
		string restored = await encoder.DecodeAsync(encoded, TestContext.CancellationToken).ConfigureAwait(false);

		Assert.AreEqual(original, restored, $"{providerName} async should restore the original string exactly");
	}

	#endregion

	#region Overload equivalence

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Roundtrips_Through_Every_Overload(IObfuscationProvider obfuscator, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("every overload for " + providerName);

		// span in, byte[] out
		byte[] viaSpan = obfuscator.Obfuscate(original);
		CollectionAssert.AreEqual(original, obfuscator.Deobfuscate(viaSpan), $"{providerName} span overload should round-trip");

		// stream in, byte[] out
		using MemoryStream source = new(original);
		byte[] viaStream = obfuscator.Obfuscate(source);
		CollectionAssert.AreEqual(original, obfuscator.Deobfuscate(viaStream), $"{providerName} stream overload should round-trip");

		// every entry point must agree on the result
		CollectionAssert.AreEqual(viaSpan, viaStream, $"{providerName} span and stream overloads should agree");

		// stream in, stream out
		using MemoryStream streamIn = new(original);
		using MemoryStream obfuscatedOut = new();
		Assert.IsTrue(obfuscator.TryObfuscate(streamIn, obfuscatedOut), $"{providerName} should obfuscate stream to stream");
		obfuscatedOut.Position = 0;
		using MemoryStream restoredOut = new();
		Assert.IsTrue(obfuscator.TryDeobfuscate(obfuscatedOut, restoredOut), $"{providerName} should deobfuscate stream to stream");
		CollectionAssert.AreEqual(original, restoredOut.ToArray(), $"{providerName} stream-to-stream should round-trip");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Hash_Agrees_Across_Overloads(IHashProvider hasher, string providerName)
	{
		byte[] data = Encoding.UTF8.GetBytes("overload agreement for " + providerName);

		byte[] fromSpan = hasher.Hash(data);
		using MemoryStream stream = new(data);
		byte[] fromStream = hasher.Hash(stream);
		byte[] fromString = hasher.Hash("overload agreement for " + providerName);

		Assert.AreEqual(hasher.HashLengthBytes, fromSpan.Length, $"{providerName} should report its own hash length");
		CollectionAssert.AreEqual(fromSpan, fromStream, $"{providerName} span and stream overloads should agree");
		CollectionAssert.AreEqual(fromSpan, fromString, $"{providerName} span and string overloads should agree");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Hash_Is_Deterministic_And_Sensitive(IHashProvider hasher, string providerName)
	{
		byte[] first = hasher.Hash("stability check");
		byte[] second = hasher.Hash("stability check");
		byte[] different = hasher.Hash("stability checl");

		CollectionAssert.AreEqual(first, second, $"{providerName} should be deterministic");
		CollectionAssert.AreNotEqual(first, different, $"{providerName} should differ for different input");
	}

	#endregion

	#region Thread safety

	[TestMethod]
	public void Encryption_Provider_Is_Safe_To_Share_Across_Threads()
	{
		// Registered as a singleton, so a single instance is used concurrently by design.
		// The transform methods are default interface implementations, so access them through the interface.
		using AesEncryptionProvider instance = new();
		IEncryptionProvider encryptor = instance;

		(byte[] Key, byte[] Iv, string Text)[] cases = [.. Enumerable.Range(0, 64).Select(i =>
			(encryptor.GenerateKey(), encryptor.GenerateIV(), $"payload number {i}"))];

		ParallelLoopResult loop = Parallel.ForEach(cases, testCase =>
		{
			string encrypted = encryptor.Encrypt(testCase.Text, testCase.Key, testCase.Iv);
			string restored = encryptor.Decrypt(encrypted, testCase.Key, testCase.Iv);
			Assert.AreEqual(testCase.Text, restored, "Concurrent use must not mix key or IV state between callers");
		});

		Assert.IsTrue(loop.IsCompleted, "All concurrent encryption operations should complete");
	}

	[TestMethod]
	public void Encryption_Keys_And_IVs_Are_Not_Repeated()
	{
		using AesEncryptionProvider encryptor = new();

		HashSet<string> keys = [.. Enumerable.Range(0, 32).Select(_ => Convert.ToBase64String(encryptor.GenerateKey()))];
		HashSet<string> ivs = [.. Enumerable.Range(0, 32).Select(_ => Convert.ToBase64String(encryptor.GenerateIV()))];

		Assert.AreEqual(32, keys.Count, "Every generated key should be distinct");
		Assert.AreEqual(32, ivs.Count, "Every generated IV should be distinct");
	}

	/// <summary>
	/// Regression test for ciphertext whose final byte is zero.
	/// </summary>
	/// <remarks>
	/// The span-based decrypt path has to recover the ciphertext length from a buffer that may be larger
	/// than the ciphertext, and it does so by trimming trailing zeros. Trimming naively corrupts any
	/// ciphertext that legitimately ends in a zero byte — about one in every 256. Searching for such a
	/// case makes that failure deterministic rather than intermittent.
	/// </remarks>
	[TestMethod]
	public void Encryption_Span_Handles_Ciphertext_Ending_In_Zero()
	{
		// The transform methods are default interface implementations, so access them through the interface.
		using AesEncryptionProvider instance = new();
		IEncryptionProvider encryptor = instance;
		byte[] iv = encryptor.GenerateIV();

		for (int attempt = 0; attempt < 2000; attempt++)
		{
			byte[] key = encryptor.GenerateKey();
			byte[] plaintext = Encoding.UTF8.GetBytes($"ciphertext trailing zero probe {attempt}");
			byte[] ciphertext = encryptor.Encrypt(plaintext, key, iv);

			if (ciphertext[^1] != 0)
			{
				continue;
			}

			// Oversized buffer, so the trailing zero is ambiguous with buffer padding.
			byte[] destination = new byte[ciphertext.Length * 2];
			Assert.IsTrue(
				encryptor.TryDecrypt(ciphertext, key, iv, destination),
				"Ciphertext ending in a zero byte should still decrypt");
			CollectionAssert.AreEqual(
				plaintext,
				destination[..plaintext.Length],
				"Ciphertext ending in a zero byte should decrypt to the original plaintext");
			return;
		}

		Assert.Inconclusive("No ciphertext ending in a zero byte was produced in 2000 attempts.");
	}

	#endregion

	#region Registration

	[TestMethod]
	public void Registration_Is_Idempotent()
	{
		ServiceCollection services = new();
		services.AddCommon();
		int afterFirst = services.Count;

		services.AddCommon();

		Assert.AreEqual(afterFirst, services.Count, "Registering the same providers twice should not duplicate them");
	}

	[TestMethod]
	public void Registration_Exposes_Providers_By_Concrete_Type_And_Interface()
	{
		using ServiceProvider provider = BuildProvider();

		Assert.IsNotNull(
			provider.GetService<HashProviders.SHA256.SHA256HashProvider>(),
			"A specific provider should be resolvable by its concrete type");
		Assert.IsGreaterThan(
			1,
			provider.GetServices<IHashProvider>().Count(),
			"All registered hash providers should be resolvable as a set");
	}

	#endregion
}
