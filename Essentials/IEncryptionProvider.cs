// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials;

using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for encryption providers that can encrypt and decrypt data.
/// </summary>
public interface IEncryptionProvider
{
	/// <summary>
	/// Generates a new encryption key.
	/// </summary>
	/// <returns>A new encryption key.</returns>
	public byte[] GenerateKey();

	/// <summary>
	/// Generates a new initialization vector.
	/// </summary>
	/// <returns>A new initialization vector.</returns>
	public byte[] GenerateIV();

	/// <summary>
	/// Tries to encrypt the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public bool TryEncrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination);

	/// <summary>
	/// Tries to encrypt the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public bool TryEncrypt(Stream data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Stream destination);

	/// <summary>
	/// Tries to encrypt the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public bool TryEncrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Stream destination)
	{
		using MemoryStream inputStream = new(data.ToArray());
		return TryEncrypt(inputStream, key, iv, destination);
	}

	/// <summary>
	/// Tries to encrypt the data from the span and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public Task<bool> TryEncryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Memory<byte> destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryEncrypt(data.Span, key.Span, iv.Span, destination.Span), cancellationToken);

	/// <summary>
	/// Tries to encrypt the data from the stream and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public Task<bool> TryEncryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryEncrypt(data, key.Span, iv.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to encrypt the data from the span and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="destination">The destination to write the encrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public Task<bool> TryEncryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryEncrypt(data.Span, key.Span, iv.Span, destination), cancellationToken);

	/// <summary>
	/// Encrypts the data from the span and returns the result.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	public byte[] Encrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		using MemoryStream outputStream = new();
		if (!TryEncrypt(data, key, iv, outputStream))
		{
			throw new InvalidOperationException("Encryption failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Encrypts the data from the stream and returns the result.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	public byte[] Encrypt(Stream data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		using MemoryStream outputStream = new();
		if (!TryEncrypt(data, key, iv, outputStream))
		{
			throw new InvalidOperationException("Encryption failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Encrypts the data from the string and returns the result.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	public string Encrypt(string data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Encoding.UTF8.GetString(Encrypt(bytes, key, iv));
	}

	/// <summary>
	/// Encrypts the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encrypted data.</returns>
	public Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Encrypt(data.Span, key.Span, iv.Span), cancellationToken);

	/// <summary>
	/// Encrypts the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encrypted data.</returns>
	public Task<byte[]> EncryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Encrypt(data, key.Span, iv.Span), cancellationToken);

	/// <summary>
	/// Encrypts the data from the string and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encrypted data.</returns>
	public Task<string> EncryptAsync(string data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Encrypt(data, key.Span, iv.Span), cancellationToken);

	/// <summary>
	/// Tries to decrypt the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	public bool TryDecrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination);

	/// <summary>
	/// Tries to decrypt the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	public bool TryDecrypt(Stream data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Stream destination);

	/// <summary>
	/// Tries to decrypt the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	public bool TryDecrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Stream destination)
	{
		using MemoryStream inputStream = new(data.ToArray());
		return TryDecrypt(inputStream, key, iv, destination);
	}

	/// <summary>
	/// Tries to decrypt the data from the span and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public Task<bool> TryDecryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Memory<byte> destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDecrypt(data.Span, key.Span, iv.Span, destination.Span), cancellationToken);

	/// <summary>
	/// Tries to decrypt the data from the stream and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public Task<bool> TryDecryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDecrypt(data, key.Span, iv.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to decrypt the data from the span and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="destination">The destination to write the decrypted data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public Task<bool> TryDecryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDecrypt(data.Span, key.Span, iv.Span, destination), cancellationToken);

	/// <summary>
	/// Decrypts the data from the span and returns the result.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		using MemoryStream outputStream = new();
		if (!TryDecrypt(data, key, iv, outputStream))
		{
			throw new InvalidOperationException("Decryption failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Decrypts the data from the stream and returns the result.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	public byte[] Decrypt(Stream data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		using MemoryStream outputStream = new();
		if (!TryDecrypt(data, key, iv, outputStream))
		{
			throw new InvalidOperationException("Decryption failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Decrypts the data from the string and returns the result.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	public string Decrypt(string data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Encoding.UTF8.GetString(Decrypt(bytes, key, iv));
	}

	/// <summary>
	/// Decrypts the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decrypted data.</returns>
	public Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decrypt(data.Span, key.Span, iv.Span), cancellationToken);

	/// <summary>
	/// Decrypts the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decrypted data.</returns>
	public Task<byte[]> DecryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decrypt(data, key.Span, iv.Span), cancellationToken);

	/// <summary>
	/// Decrypts the data from the string and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to decrypt.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decrypted data.</returns>
	public Task<string> DecryptAsync(string data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decrypt(data, key.Span, iv.Span), cancellationToken);
}
