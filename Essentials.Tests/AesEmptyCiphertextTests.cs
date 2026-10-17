// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using ktsu.Essentials.EncryptionProviders.Aes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// AES-CBC with PKCS7 padding always produces at least one block, so a zero-byte ciphertext is never
/// valid. The span <c>TryDecrypt</c> rejected it while the stream paths, and the span <c>Decrypt</c>
/// that routes through them, "decrypted" it to empty plaintext (issue #75).
/// </summary>
[TestClass]
public class AesEmptyCiphertextTests
{
	private static readonly IEncryptionProvider Provider = new AesEncryptionProvider();
	private static readonly byte[] Key = Provider.GenerateKey();
	private static readonly byte[] Iv = Provider.GenerateIV();
	private static readonly byte[] NoBytes = [];

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public void Decrypt_EmptyCiphertext_Throws() =>
		Assert.ThrowsExactly<InvalidOperationException>(() => Provider.Decrypt(NoBytes, Key, Iv));

	[TestMethod]
	public void TryDecrypt_EmptySpanToStream_ReturnsFalse()
	{
		using MemoryStream destination = new();
		Assert.IsFalse(Provider.TryDecrypt(NoBytes.AsSpan(), Key, Iv, destination));
	}

	[TestMethod]
	public void TryDecrypt_EmptyStream_ReturnsFalse()
	{
		using MemoryStream source = new();
		using MemoryStream destination = new();
		Assert.IsFalse(Provider.TryDecrypt(source, Key, Iv, destination));
	}

	[TestMethod]
	public async Task TryDecryptAsync_EmptyStream_ReturnsFalseAsync()
	{
		using MemoryStream source = new();
		using MemoryStream destination = new();
		Assert.IsFalse(await Provider.TryDecryptAsync(source, Key, Iv, destination, TestContext.CancellationToken).ConfigureAwait(false));
	}

	[TestMethod]
	public async Task EmptyPlaintext_StillRoundTripsThroughEveryPathAsync()
	{
		// The encryption of empty plaintext is one padding block, which must still decrypt to nothing.
		byte[] encrypted = Provider.Encrypt(NoBytes, Key, Iv);
		Assert.HasCount(16, encrypted);

		Assert.IsEmpty(Provider.Decrypt(encrypted, Key, Iv));

		using MemoryStream source = new(encrypted);
		using MemoryStream destination = new();
		Assert.IsTrue(Provider.TryDecrypt(source, Key, Iv, destination));
		Assert.AreEqual(0, destination.Length);

		using MemoryStream asyncSource = new(encrypted);
		using MemoryStream asyncDestination = new();
		Assert.IsTrue(await Provider.TryDecryptAsync(asyncSource, Key, Iv, asyncDestination, TestContext.CancellationToken).ConfigureAwait(false));
		Assert.AreEqual(0, asyncDestination.Length);
	}
}
