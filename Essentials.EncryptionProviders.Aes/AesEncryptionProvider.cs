// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.EncryptionProviders.Aes;

using ktsu.Essentials;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An encryption provider that uses AES for data encryption and decryption.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads — every operation creates its own
/// <see cref="System.Security.Cryptography.Aes"/> instance from the caller-supplied key and IV.
/// It is therefore safe to register as a singleton.
/// <para>
/// This provider is AES in CBC mode with PKCS7 padding, which is what <c>Aes.Create()</c> defaults to.
/// CBC ciphertext is malleable: an attacker who can modify it can make predictable changes to the
/// decrypted plaintext without knowing the key. Decryption reports padding failures, so a caller who
/// decrypts attacker-supplied input and reveals whether it parsed becomes a padding oracle.
/// </para>
/// <para>
/// Authenticate the initialization vector and the ciphertext together before decrypting them. CBC
/// recovers the first plaintext block as the initialization vector XORed with the decryption of the
/// first ciphertext block, so a tag covering only the ciphertext still leaves that block rewritable.
/// See the remarks on <see cref="IEncryptionProvider"/>.
/// </para>
/// </remarks>
public class AesEncryptionProvider : IEncryptionProvider
{
	private const int KeySize = 32; // 256 bits
	private const int IVSize = 16; // 128 bits
	private const int BlockSizeBytes = 16; // AES block size is always 128 bits

	/// <inheritdoc/>
	/// <remarks>CBC with PKCS7 always pads to the next whole block, adding a full block when already aligned.</remarks>
	public int GetMaxEncryptedLength(int sourceLength) => ((sourceLength / BlockSizeBytes) + 1) * BlockSizeBytes;

	/// <summary>
	/// Generates a new encryption key.
	/// </summary>
	/// <returns>A new encryption key.</returns>
	public byte[] GenerateKey()
	{
		byte[] key = new byte[KeySize];
		using RandomNumberGenerator rng = RandomNumberGenerator.Create();
		rng.GetBytes(key);
		return key;
	}

	/// <summary>
	/// Generates a new initialization vector.
	/// </summary>
	/// <returns>A new initialization vector.</returns>
	public byte[] GenerateIV()
	{
		byte[] iv = new byte[IVSize];
		using RandomNumberGenerator rng = RandomNumberGenerator.Create();
		rng.GetBytes(iv);
		return iv;
	}

	/// <summary>
	/// Tries to encrypt the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public bool TryEncrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		try
		{
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform encryptor = aes.CreateEncryptor(key.ToArray(), iv.ToArray());
			byte[] encryptedData = encryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);

			if (encryptedData.Length > destination.Length)
			{
				return false;
			}

			encryptedData.CopyTo(destination);
			bytesWritten = encryptedData.Length;
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to encrypt the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public bool TryEncrypt(Stream data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Stream destination)
	{
		if (data is null || destination is null || key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		try
		{
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform encryptor = aes.CreateEncryptor(key.ToArray(), iv.ToArray());
			using CryptoStream cryptoStream = new(destination, encryptor, CryptoStreamMode.Write, leaveOpen: true);
			data.CopyTo(cryptoStream);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to encrypt the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: no thread is held for the duration. The crypto stream is disposed with
	/// <c>await using</c> because disposal writes the final block, and a synchronous dispose would make
	/// that write synchronous.
	/// If this throws or returns false, the destination may hold a partial result. Cancellation still
	/// flushes the final block, so that partial result is structurally valid ciphertext and cannot be
	/// distinguished from a complete one. Treat the destination as invalid unless the method returns true.
	/// </remarks>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public async Task<bool> TryEncryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null || key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform encryptor = aes.CreateEncryptor(key.ToArray(), iv.ToArray());
			CryptoStream cryptoStream = new(destination, encryptor, CryptoStreamMode.Write, leaveOpen: true);
			await using (cryptoStream.ConfigureAwait(false))
			{
				await data.CopyToAsync(cryptoStream, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decrypt the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public bool TryDecrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (key.Length != KeySize || iv.Length != IVSize || data.IsEmpty)
		{
			return false;
		}

		try
		{
			// The ciphertext is exactly the span the caller passed. Earlier versions had to guess its
			// length by trimming trailing zeros, because the API could not report how many bytes the
			// matching encrypt call wrote; that corrupted any ciphertext ending in a zero byte.
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform decryptor = aes.CreateDecryptor(key.ToArray(), iv.ToArray());
			byte[] decryptedData = decryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);

			if (decryptedData.Length > destination.Length)
			{
				return false;
			}

			decryptedData.CopyTo(destination);
			bytesWritten = decryptedData.Length;
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decrypt the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public bool TryDecrypt(Stream data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Stream destination)
	{
		if (data is null || destination is null || key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		try
		{
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform decryptor = aes.CreateDecryptor(key.ToArray(), iv.ToArray());
			using CryptoStream cryptoStream = new(data, decryptor, CryptoStreamMode.Read, leaveOpen: true);
			cryptoStream.CopyTo(destination);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decrypt the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: no thread is held for the duration. The decrypting stream wraps the
	/// source rather than the destination, so disposing it writes nothing to the destination.
	/// If this throws or returns false, the destination may hold a partial result. Recovered plaintext
	/// carries no structure of its own, so a truncated destination cannot be distinguished from a
	/// complete decryption by inspecting it. Treat the destination as invalid unless the method
	/// returns true.
	/// </remarks>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public async Task<bool> TryDecryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null || key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform decryptor = aes.CreateDecryptor(key.ToArray(), iv.ToArray());
			CryptoStream cryptoStream = new(data, decryptor, CryptoStreamMode.Read, leaveOpen: true);
			await using (cryptoStream.ConfigureAwait(false))
			{
				await cryptoStream.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (CryptographicException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
}
