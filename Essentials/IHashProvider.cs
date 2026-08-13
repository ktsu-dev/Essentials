// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for hash providers that can hash data.
/// </summary>
public interface IHashProvider
{
	/// <summary>
	/// The length of the hash in bytes.
	/// </summary>
	public int HashLengthBytes { get; }

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(Stream data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Asynchronously hashes the specified data.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A byte array containing the hash of the data.</returns>
	public Task<byte[]> HashAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Hash(data.Span), cancellationToken);

	/// <summary>
	/// Hashes the specified data.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <returns>A byte array containing the hash of the data.</returns>
	public byte[] Hash(ReadOnlySpan<byte> data)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryHash(data, hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

	/// <summary>
	/// Hashes the specified data.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <returns>A byte array containing the hash of the data.</returns>
	public byte[] Hash(string data)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Hash(bytes);
	}

	/// <summary>
	/// Hashes the specified data.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <returns>A byte array containing the hash of the data.</returns>
	public byte[] Hash(Stream data)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryHash(data, hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}
}
