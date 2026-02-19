// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EncryptionProviders;

using System;
using System.IO;
using System.Security.Cryptography;
using ktsu.Abstractions;

/// <summary>
/// An encryption provider that uses AES for data encryption and decryption.
/// </summary>
public class Aes : IEncryptionProvider, IDisposable
{
	private const int KeySize = 32; // 256 bits
	private const int IVSize = 16; // 128 bits
	private bool disposedValue;

	private readonly Lazy<System.Security.Cryptography.Aes> _aes;
	private readonly Lazy<System.Security.Cryptography.Aes> _generator;

	/// <summary>
	/// Creates a new instance of the <see cref="Aes"/> class.
	/// </summary>
	public Aes()
	{
		_aes = new(System.Security.Cryptography.Aes.Create);
		_generator = new(System.Security.Cryptography.Aes.Create);
	}

	/// <summary>
	/// Generates a new encryption key.
	/// </summary>
	/// <returns>A new encryption key.</returns>
	public byte[] GenerateKey()
	{
		_generator.Value.GenerateKey();
		return _generator.Value.Key;
	}

	/// <summary>
	/// Generates a new initialization vector.
	/// </summary>
	/// <returns>A new initialization vector.</returns>
	public byte[] GenerateIV()
	{
		_generator.Value.GenerateIV();
		return _generator.Value.IV;
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
			_aes.Value.Key = key.ToArray();
			_aes.Value.IV = iv.ToArray();

			using ICryptoTransform encryptor = _aes.Value.CreateEncryptor();
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
			_aes.Value.Key = key.ToArray();
			_aes.Value.IV = iv.ToArray();

			using ICryptoTransform encryptor = _aes.Value.CreateEncryptor();
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
			_aes.Value.Key = key.ToArray();
			_aes.Value.IV = iv.ToArray();

			// Find the actual length of encrypted data (excluding trailing zeros)
			ReadOnlySpan<byte> actualData = data;
			int lastNonZero = data.Length - 1;
			while (lastNonZero >= 0 && data[lastNonZero] == 0)
			{
				lastNonZero--;
			}

			if (lastNonZero >= 0)
			{
				actualData = data[..(lastNonZero + 1)];
			}
			else
			{
				return false; // All zeros is not valid encrypted data
			}

			using ICryptoTransform decryptor = _aes.Value.CreateDecryptor();
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
			_aes.Value.Key = key.ToArray();
			_aes.Value.IV = iv.ToArray();

			using ICryptoTransform decryptor = _aes.Value.CreateDecryptor();
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
	/// Disposes the resources used by the Aes instance.
	/// </summary>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				if (_aes.IsValueCreated)
				{
					_aes.Value.Dispose();
				}

				if (_generator.IsValueCreated)
				{
					_generator.Value.Dispose();
				}
			}

			disposedValue = true;
		}
	}

	/// <summary>
	/// Disposes the Aes instance and releases all resources.
	/// </summary>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
