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

	[TestMethod]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Deliberately exercises the synchronous overloads alongside the asynchronous ones.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1835:Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'", Justification = "Deliberately exercises the array-based ReadAsync override.")]
	public async Task CountingReadStream_CountsEveryReadOverloadAsync()
	{
		using MemoryStream inner = new([1, 2, 3, 4, 5, 6, 7, 8]);
		using AesEncryptionProvider.CountingReadStream counting = new(inner);
		byte[] buffer = new byte[2];

		Assert.AreEqual(2, counting.Read(buffer, 0, 2));
		Assert.AreEqual(2, counting.Read(buffer.AsSpan()));
		Assert.AreEqual(2, await counting.ReadAsync(buffer, 0, 2, TestContext.CancellationToken).ConfigureAwait(false));
		Assert.AreEqual(2, await counting.ReadAsync(buffer.AsMemory(), TestContext.CancellationToken).ConfigureAwait(false));
		Assert.AreEqual(0, counting.Read(buffer, 0, 2));

		Assert.AreEqual(8, counting.BytesRead);
	}

	[TestMethod]
	public void CountingReadStream_IsReadOnlyAndForwardOnly()
	{
		using MemoryStream inner = new();
		using AesEncryptionProvider.CountingReadStream counting = new(inner);

		Assert.IsTrue(counting.CanRead);
		Assert.IsFalse(counting.CanSeek);
		Assert.IsFalse(counting.CanWrite);
		counting.Flush();

		Assert.ThrowsExactly<NotSupportedException>(() => _ = counting.Length);
		Assert.ThrowsExactly<NotSupportedException>(() => _ = counting.Position);
		Assert.ThrowsExactly<NotSupportedException>(() => counting.Position = 0);
		Assert.ThrowsExactly<NotSupportedException>(() => counting.Seek(0, SeekOrigin.Begin));
		Assert.ThrowsExactly<NotSupportedException>(() => counting.SetLength(0));
		Assert.ThrowsExactly<NotSupportedException>(() => counting.Write([1], 0, 1));
	}
}
