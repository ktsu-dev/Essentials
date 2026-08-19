// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// A hash computation that accepts data in successive chunks, so a caller can digest bytes it is
/// already moving for another reason instead of handing over a stream to be read.
/// </summary>
/// <remarks>
/// Obtained from <see cref="IHashProvider.CreateIncremental"/>. Instances are stateful and are not
/// safe to share across threads. Dispose when finished.
/// </remarks>
public interface IIncrementalHash : IDisposable
{
	/// <summary>
	/// The length of the hash in bytes.
	/// </summary>
	public int HashLengthBytes { get; }

	/// <summary>
	/// Appends data to the running hash.
	/// </summary>
	/// <param name="data">The data to append.</param>
	public void Append(ReadOnlySpan<byte> data);

	/// <summary>
	/// Tries to write the hash of everything appended so far into <paramref name="destination"/>,
	/// then resets so the instance can be reused.
	/// </summary>
	/// <remarks>
	/// When the destination is too small the call fails without consuming the appended data: the
	/// running state is left intact so the caller can retry with a correctly sized buffer.
	/// </remarks>
	/// <param name="destination">The buffer to write the hash to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the hash was written, false if <paramref name="destination"/> was too small.</returns>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Returns the hash of everything appended so far, then resets so the instance can be reused.
	/// </summary>
	/// <returns>A byte array containing the hash.</returns>
	/// <exception cref="InvalidOperationException">The hash could not be produced.</exception>
	public byte[] GetHashAndReset()
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryGetHashAndReset(hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}
}
