// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for keyed hash providers, which compute a message authentication code over data using
/// a secret key.
/// </summary>
/// <remarks>
/// A keyed hash answers "was this produced by someone holding the key, and is it unmodified", which
/// an unkeyed <see cref="IHashProvider"/> cannot. <see cref="IEncryptionProvider"/> provides
/// confidentiality but not integrity, so a caller who needs tamper detection over ciphertext
/// authenticates it with one of these.
/// <para>
/// The key is passed per call rather than bound at construction, which matches
/// <see cref="IEncryptionProvider"/> and keeps providers stateless singletons. A provider holding
/// key or algorithm state in a field is the defect recorded in the remarks on the SHA-256 provider,
/// where concurrent callers corrupted each other's in-progress hash.
/// </para>
/// <para>
/// Generate the key with a cryptographically secure random number generator, such as
/// <see cref="RandomNumberGenerator"/>, and make it at least <see cref="HashLengthBytes"/> long. An
/// empty or predictable key still produces a valid-looking tag, so nothing about the output signals
/// a weak key. Never reuse an <see cref="IEncryptionProvider"/> encryption key for authentication.
/// </para>
/// </remarks>
public interface IKeyedHashProvider
{
	/// <summary>
	/// The length of the authentication tag in bytes.
	/// </summary>
	public int HashLengthBytes { get; }

	/// <summary>
	/// Tries to compute the authentication tag for the specified data.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the tag was written, false if the buffer was too small or the key rejected.</returns>
	public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Tries to compute the authentication tag for the data in the specified stream.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate. Read to its end from its current position.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the tag was written, false if the stream was null, the buffer too small, or the key rejected.</returns>
	public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Creates a keyed incremental hash that accepts data in successive chunks.
	/// </summary>
	/// <remarks>
	/// The default implementation accumulates every appended byte in memory and computes the tag in
	/// one pass when it is requested. That is correct but it buffers the entire input, so implementers
	/// should override this with a genuinely incremental implementation. Doing so also lets
	/// <see cref="TryHashAsync(ReadOnlyMemory{byte}, Stream, Memory{byte}, CancellationToken)"/>
	/// stream properly, because that method is built on this one.
	/// </remarks>
	/// <param name="key">The secret key.</param>
	/// <returns>A new keyed incremental hash. The caller owns it and should dispose it, which zeroes the key copy.</returns>
	public IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key) => new BufferingKeyedIncrementalHash(this, key);

	/// <summary>
	/// Asynchronously computes the authentication tag over a stream, reading it in one pass.
	/// </summary>
	/// <remarks>
	/// The key is <see cref="ReadOnlyMemory{T}"/> rather than <see cref="ReadOnlySpan{T}"/> because a
	/// span cannot cross an await boundary. The result is not reported through an <c>out</c> parameter
	/// for the same reason; a return value of true guarantees exactly <see cref="HashLengthBytes"/>
	/// bytes were written.
	/// <para>
	/// The read buffer is scrubbed on its way back to the pool. <see cref="ArrayPool{T}"/>.Shared is
	/// process-wide, so without that the tail of the authenticated message stays readable to whatever
	/// rents next.
	/// </para>
	/// </remarks>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the tag was written, false if the stream was null or the buffer too small.</returns>
	public async Task<bool> TryHashAsync(ReadOnlyMemory<byte> key, Stream data, Memory<byte> destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination.Length < HashLengthBytes)
		{
			return false;
		}

		using IIncrementalHash hash = CreateIncremental(key.Span);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
		try
		{
			int read;
			while ((read = await data.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
				hash.Append(buffer.AsSpan(0, read));
			}

			return hash.TryGetHashAndReset(destination.Span, out int bytesWritten)
				&& bytesWritten == HashLengthBytes;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
		}
	}

	/// <summary>
	/// Asynchronously computes the authentication tag over a stream, reading it in one pass.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The authentication tag.</returns>
	/// <exception cref="InvalidOperationException">The tag could not be produced.</exception>
	public async Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, Stream data, CancellationToken cancellationToken = default)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !await TryHashAsync(key, data, hash, cancellationToken).ConfigureAwait(false)
			? throw new InvalidOperationException($"Keyed hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

	/// <summary>
	/// Asynchronously computes the authentication tag for the specified data.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The authentication tag.</returns>
	public Task<byte[]> HashAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Hash(key.Span, data.Span), cancellationToken);

	/// <summary>
	/// Computes the authentication tag for the specified data.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <returns>The authentication tag.</returns>
	/// <exception cref="InvalidOperationException">The tag could not be produced.</exception>
	public byte[] Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryHash(key, data, hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Keyed hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

	/// <summary>
	/// Computes the authentication tag for the UTF-8 encoding of the specified text.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The text to authenticate.</param>
	/// <returns>The authentication tag.</returns>
	public byte[] Hash(ReadOnlySpan<byte> key, string data)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Hash(key, bytes);
	}

	/// <summary>
	/// Computes the authentication tag over the data in the specified stream.
	/// </summary>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <returns>The authentication tag.</returns>
	/// <exception cref="InvalidOperationException">The tag could not be produced.</exception>
	public byte[] Hash(ReadOnlySpan<byte> key, Stream data)
	{
		byte[] hash = new byte[HashLengthBytes];
		return !TryHash(key, data, hash, out int bytesWritten) || bytesWritten != HashLengthBytes
			? throw new InvalidOperationException($"Keyed hashing failed to produce {HashLengthBytes} bytes of output.")
			: hash;
	}

	/// <summary>
	/// Determines whether the supplied tag is the correct authentication tag for the data.
	/// </summary>
	/// <remarks>
	/// Prefer this to computing a tag and comparing it yourself. The comparison runs in a time that
	/// does not depend on the tag's contents, so it does not leak how much of a forged tag was
	/// correct. A tag of the wrong length is rejected without comparing.
	/// </remarks>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data the tag is claimed to authenticate.</param>
	/// <param name="expected">The tag to check.</param>
	/// <returns>True if the tag is correct for this key and data, false otherwise.</returns>
	public bool Verify(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected)
	{
		if (expected.Length != HashLengthBytes)
		{
			return false;
		}

		byte[] actual = new byte[HashLengthBytes];
		try
		{
			return TryHash(key, data, actual, out int bytesWritten)
				&& bytesWritten == HashLengthBytes
				&& FixedTimeComparison.FixedTimeEquals(actual, expected);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(actual);
		}
	}
}
