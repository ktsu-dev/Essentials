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
/// <param name="inner">The underlying incremental hash. This instance takes ownership and disposes it.</param>
/// <param name="hashLengthBytes">The length of the hash in bytes.</param>
public sealed class IncrementalHashAdapter(IncrementalHash inner, int hashLengthBytes) : IIncrementalHash
{
	/// <inheritdoc/>
	public int HashLengthBytes => hashLengthBytes;

	/// <inheritdoc/>
	public void Append(ReadOnlySpan<byte> data) => inner.AppendData(data);

	/// <inheritdoc/>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		return destination.Length >= hashLengthBytes
			&& inner.TryGetHashAndReset(destination, out bytesWritten);
	}

	/// <inheritdoc/>
	public void Dispose() => inner.Dispose();
}
