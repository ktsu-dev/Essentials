// Copyright (c) 2023-2026 ktsu-dev contributors

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
	/// Gets the largest number of bytes
	/// <see cref="TryEncrypt(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte}, out int)"/>
	/// can produce for an input of the given length.
	/// </summary>
	/// <param name="sourceLength">The length of the data to encrypt.</param>
	/// <returns>The buffer size required to guarantee the encryption succeeds.</returns>
	public int GetMaxEncryptedLength(int sourceLength);

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
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the encryption was successful, false otherwise.</returns>
	public bool TryEncrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten);

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
	public async Task<bool> TryEncryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		using MemoryStream source = new();
		await source.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		source.Position = 0;
		return await TryEncryptAsync(source, key, iv, destination, cancellationToken).ConfigureAwait(false);
	}

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
	/// Encrypts a string and returns the ciphertext as Base64 text.
	/// </summary>
	/// <remarks>
	/// The input is encoded as UTF8, encrypted, and the ciphertext is Base64-encoded so the result is
	/// safe to store or transmit as text. Use <see cref="Decrypt(string, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
	/// to reverse it.
	/// </remarks>
	/// <param name="data">The data to encrypt.</param>
	/// <param name="key">The key to use for encryption.</param>
	/// <param name="iv">The initialization vector to use for encryption.</param>
	/// <returns>The ciphertext as a Base64 string.</returns>
	public string Encrypt(string data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Convert.ToBase64String(Encrypt(bytes, key, iv));
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
	public async Task<byte[]> EncryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
	{
		using MemoryStream destination = new();
		return !await TryEncryptAsync(data, key, iv, destination, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException("Encryption failed to produce output with the allocated buffer.")
			: destination.ToArray();
	}

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
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the decryption was successful, false otherwise.</returns>
	public bool TryDecrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten);

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
	public async Task<bool> TryDecryptAsync(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, Stream destination, CancellationToken cancellationToken = default)
	{
		using MemoryStream source = new();
		await source.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		source.Position = 0;
		return await TryDecryptAsync(source, key, iv, destination, cancellationToken).ConfigureAwait(false);
	}

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
	/// Decrypts Base64 ciphertext produced by <see cref="Encrypt(string, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
	/// and returns the original string.
	/// </summary>
	/// <param name="data">The Base64-encoded ciphertext.</param>
	/// <param name="key">The key to use for decryption.</param>
	/// <param name="iv">The initialization vector to use for decryption.</param>
	/// <returns>The plaintext as a UTF8 string.</returns>
	/// <exception cref="FormatException"><paramref name="data"/> is not valid Base64.</exception>
	public string Decrypt(string data, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
	{
		byte[] bytes = Convert.FromBase64String(data);
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
	public async Task<byte[]> DecryptAsync(Stream data, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> iv, CancellationToken cancellationToken = default)
	{
		using MemoryStream destination = new();
		return !await TryDecryptAsync(data, key, iv, destination, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException("Decryption failed to produce output with the allocated buffer.")
			: destination.ToArray();
	}

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
