// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Security.Cryptography;

/// <summary>
/// An <see cref="IIncrementalHash"/> backed by <see cref="IncrementalHash"/>, for providers built on
/// <see cref="System.Security.Cryptography"/>.
/// </summary>
/// <remarks>
/// Public because the cryptographic hash providers each ship as their own package and all need it.
/// The hash length is supplied by the caller rather than read from
/// <see cref="IncrementalHash"/>, whose <c>HashLengthInBytes</c> property does not exist on
/// netstandard2.1.
/// </remarks>
public sealed class IncrementalHashAdapter : IIncrementalHash
{
	private readonly IncrementalHash inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="IncrementalHashAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying incremental hash. This instance takes ownership and disposes it.</param>
	/// <param name="hashLengthBytes">The length of the hash in bytes.</param>
	/// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="hashLengthBytes"/> is not positive.</exception>
	public IncrementalHashAdapter(IncrementalHash inner, int hashLengthBytes)
	{
		Ensure.NotNull(inner);

		if (hashLengthBytes <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(hashLengthBytes), hashLengthBytes, "Hash length must be positive.");
		}

		this.inner = inner;
		HashLengthBytes = hashLengthBytes;
	}

	/// <inheritdoc/>
	public int HashLengthBytes { get; }

	/// <inheritdoc/>
	public void Append(ReadOnlySpan<byte> data) => inner.AppendData(data);

	/// <inheritdoc/>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		return destination.Length >= HashLengthBytes
			&& inner.TryGetHashAndReset(destination, out bytesWritten);
	}

	/// <inheritdoc/>
	public void Dispose() => inner.Dispose();
}
