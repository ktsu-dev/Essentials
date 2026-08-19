// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers;
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
	/// Creates an incremental hash that accepts data in successive chunks.
	/// </summary>
	/// <remarks>
	/// The default implementation accumulates every appended byte in memory and hashes it in one pass
	/// when the digest is requested. That is correct but it buffers the entire input, so implementers
	/// should override this with a genuinely incremental implementation. Doing so also lets
	/// <see cref="TryHashAsync(Stream, Memory{byte}, CancellationToken)"/> stream properly, because
	/// that method is built on this one.
	/// </remarks>
	/// <returns>A new incremental hash. The caller owns it and should dispose it.</returns>
	public IIncrementalHash CreateIncremental() => new BufferingIncrementalHash(this);

	/// <summary>
	/// Asynchronously hashes a stream into the provided buffer, reading it in one pass.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: the stream is read with <see cref="Stream.ReadAsync(Memory{byte}, CancellationToken)"/>
	/// and no thread is held for the duration. The result is not reported through an <c>out</c> parameter
	/// because one cannot cross an await boundary; a return value of true guarantees exactly
	/// <see cref="HashLengthBytes"/> bytes were written.
	/// </remarks>
	/// <param name="data">The stream to hash.</param>
	/// <param name="destination">The buffer to write the hash to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the hash was written, false if the stream was null or the buffer too small.</returns>
	public async Task<bool> TryHashAsync(Stream data, Memory<byte> destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination.Length < HashLengthBytes)
		{
			return false;
		}

		using IIncrementalHash hash = CreateIncremental();
		byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
		try
		{
			int read;
			while ((read = await data.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
			{
				hash.Append(buffer.AsSpan(0, read));
			}

			return hash.TryGetHashAndReset(destination.Span, out int bytesWritten)
				&& bytesWritten == HashLengthBytes;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	/// <summary>
	/// Asynchronously hashes a stream, reading it in one pass.
	/// </summary>
	/// <param name="data">The stream to hash.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A byte array containing the hash of the stream.</returns>
	/// <exception cref="InvalidOperationException">The hash could not be produced.</exception>
	public async Task<byte[]> HashAsync(Stream data, CancellationToken cancellationToken = default)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !await TryHashAsync(data, hash, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException($"Hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

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
