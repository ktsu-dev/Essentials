// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.EncryptionProviders.Aes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The provider zeroes its copies of the key and the plaintext once each operation is done with them (issue #98).
/// A round trip cannot show that the zeroing came too early, since encrypting and decrypting under the same
/// zeroed key would still agree, so every path is checked against ciphertext from an independent
/// <see cref="Aes"/> instead, and the caller's key and plaintext are checked to be untouched.
/// </summary>
[TestClass]
public sealed class AesKnownAnswerTests
{
	private static readonly byte[] Key = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];
	private static readonly byte[] Iv = [.. Enumerable.Range(100, 16).Select(i => (byte)i)];
	private static readonly byte[] Plaintext = System.Text.Encoding.UTF8.GetBytes("Seventeen bytes!! and then some more of them");

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public async Task Every_Path_Encrypts_And_Decrypts_Under_The_Callers_Key()
	{
		IEncryptionProvider provider = new AesEncryptionProvider();
		byte[] key = [.. Key];
		byte[] plaintext = [.. Plaintext];
		byte[] expected = Reference();

		byte[] destination = new byte[provider.GetMaxEncryptedLength(plaintext.Length)];
		Assert.IsTrue(provider.TryEncrypt(plaintext, key, Iv, destination, out int written));
		CollectionAssert.AreEqual(expected, destination[..written], "span encrypt");

		using (MemoryStream source = new(plaintext))
		using (MemoryStream sink = new())
		{
			Assert.IsTrue(provider.TryEncrypt(source, key, Iv, sink));
			CollectionAssert.AreEqual(expected, sink.ToArray(), "stream encrypt");
		}

		using (MemoryStream source = new(plaintext))
		using (MemoryStream sink = new())
		{
			Assert.IsTrue(await provider.TryEncryptAsync(source, key, Iv, sink, TestContext.CancellationToken).ConfigureAwait(false));
			CollectionAssert.AreEqual(expected, sink.ToArray(), "async stream encrypt");
		}

		byte[] decrypted = new byte[expected.Length];
		Assert.IsTrue(provider.TryDecrypt(expected, key, Iv, decrypted, out written));
		CollectionAssert.AreEqual(Plaintext, decrypted[..written], "span decrypt");

		using (MemoryStream source = new(expected))
		using (MemoryStream sink = new())
		{
			Assert.IsTrue(provider.TryDecrypt(source, key, Iv, sink));
			CollectionAssert.AreEqual(Plaintext, sink.ToArray(), "stream decrypt");
		}

		using (MemoryStream source = new(expected))
		using (MemoryStream sink = new())
		{
			Assert.IsTrue(await provider.TryDecryptAsync(source, key, Iv, sink, TestContext.CancellationToken).ConfigureAwait(false));
			CollectionAssert.AreEqual(Plaintext, sink.ToArray(), "async stream decrypt");
		}

		CollectionAssert.AreEqual(Key, key, "the caller's key was changed");
		CollectionAssert.AreEqual(Plaintext, plaintext, "the caller's plaintext was changed");
	}

	[TestMethod]
	public void An_Undersized_Destination_Still_Fails()
	{
		IEncryptionProvider provider = new AesEncryptionProvider();
		byte[] ciphertext = Reference();

		Assert.IsFalse(provider.TryDecrypt(ciphertext, Key, Iv, new byte[Plaintext.Length - 1], out int written));
		Assert.AreEqual(0, written);
		Assert.IsFalse(provider.TryEncrypt(Plaintext, Key, Iv, new byte[ciphertext.Length - 1], out written));
		Assert.AreEqual(0, written);
	}

	private static byte[] Reference()
	{
		using Aes aes = Aes.Create();
		aes.Key = Key;
		return aes.EncryptCbc(Plaintext, Iv, PaddingMode.PKCS7);
	}
}
