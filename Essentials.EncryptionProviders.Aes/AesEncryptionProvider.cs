// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.EncryptionProviders.Aes;

using ktsu.Essentials;
using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// An encryption provider that uses AES for data encryption and decryption.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads — every operation creates its own
/// <see cref="System.Security.Cryptography.Aes"/> instance from the caller-supplied key and IV.
/// It is therefore safe to register as a singleton.
/// </remarks>
public class AesEncryptionProvider : IEncryptionProvider, IDisposable
{
	private const int KeySize = 32; // 256 bits
	private const int IVSize = 16; // 128 bits
	private const int BlockSizeBytes = 16; // AES block size is always 128 bits

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
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public bool TryEncrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination)
	{
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
			// Clear the rest of the destination buffer to ensure only encrypted data is present
			destination[encryptedData.Length..].Clear();
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
	/// Tries to decrypt the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public bool TryDecrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination)
	{
		if (key.Length != KeySize || iv.Length != IVSize)
		{
			return false;
		}

		try
		{
			ReadOnlySpan<byte> actualData = TrimBufferPadding(data);
			if (actualData.IsEmpty)
			{
				return false; // All zeros is not valid encrypted data
			}

			using System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create();
			using ICryptoTransform decryptor = aes.CreateDecryptor(key.ToArray(), iv.ToArray());
			byte[] decryptedData = decryptor.TransformFinalBlock(actualData.ToArray(), 0, actualData.Length);

			if (decryptedData.Length > destination.Length)
			{
				return false;
			}

			decryptedData.CopyTo(destination);
			// Clear the rest of the destination buffer
			destination[decryptedData.Length..].Clear();
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
	/// Recovers the ciphertext length from a buffer that may be larger than the ciphertext itself.
	/// </summary>
	/// <remarks>
	/// <see cref="TryEncrypt(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte})"/> zero-fills
	/// the unused tail of the caller's buffer, and the span-based API has no way to report how many bytes it wrote.
	/// Trailing zeros are therefore stripped, then the length is rounded back up to the next whole AES block —
	/// ciphertext is always a whole number of blocks, so this preserves ciphertext that legitimately ends in zero
	/// bytes (roughly 1 in 256 of all ciphertexts), which naive zero-stripping would corrupt.
	/// </remarks>
	/// <param name="data">The buffer holding ciphertext followed by zero or more padding zeros.</param>
	/// <returns>The ciphertext, or an empty span if the buffer is entirely zeros.</returns>
	private static ReadOnlySpan<byte> TrimBufferPadding(ReadOnlySpan<byte> data)
	{
		int lastNonZero = data.Length - 1;
		while (lastNonZero >= 0 && data[lastNonZero] == 0)
		{
			lastNonZero--;
		}

		if (lastNonZero < 0)
		{
			return default;
		}

		// Round up to the next whole block, without exceeding the buffer we were given.
		int blocks = (lastNonZero / BlockSizeBytes) + 1;
		int length = Math.Min(blocks * BlockSizeBytes, data.Length);
		return data[..length];
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
	/// Releases the resources used by this provider.
	/// </summary>
	/// <remarks>
	/// This provider holds no disposable state; the method exists only so that existing callers using
	/// <c>using</c> or DI-managed disposal continue to compile. It will be removed in the next major version.
	/// </remarks>
	/// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
	protected virtual void Dispose(bool disposing)
	{
		// Nothing to dispose — all cryptographic state is created and released per call.
	}

	/// <summary>
	/// Releases the resources used by this provider.
	/// </summary>
	/// <remarks>
	/// This provider holds no disposable state; calling this method is not required. It exists only for
	/// source compatibility with earlier versions and will be removed in the next major version.
	/// </remarks>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
