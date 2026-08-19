// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO.Hashing;

/// <summary>
/// An <see cref="IIncrementalHash"/> backed by a <see cref="NonCryptographicHashAlgorithm"/>.
/// </summary>
/// <remarks>
/// This file is linked into each non-cryptographic hash provider project rather than living in
/// ktsu.Essentials, because that package is interfaces-only and must not take a dependency on
/// System.IO.Hashing. Every algorithm it serves — Crc32, Crc64, XxHash32, XxHash64, XxHash3 and
/// XxHash128 — derives from NonCryptographicHashAlgorithm, so one adapter covers all six.
/// Because each of the six provider projects declares its own <c>InternalsVisibleTo</c> for
/// ktsu.Essentials.Tests, the test assembly sees six distinct internal types all named
/// <c>ktsu.Essentials.NonCryptoIncrementalHash</c>. That is harmless until a test references the
/// type by name, at which point the compiler's type-ambiguity error will look unrelated to this file.
/// </remarks>
/// <param name="inner">The underlying algorithm instance.</param>
/// <param name="hashLengthBytes">The length of the hash in bytes.</param>
internal sealed class NonCryptoIncrementalHash(NonCryptographicHashAlgorithm inner, int hashLengthBytes) : IIncrementalHash
{
	/// <inheritdoc/>
	public int HashLengthBytes => hashLengthBytes;

	/// <inheritdoc/>
	public void Append(ReadOnlySpan<byte> data) => inner.Append(data);

	/// <inheritdoc/>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		return destination.Length >= hashLengthBytes
			&& inner.TryGetHashAndReset(destination, out bytesWritten);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// NonCryptographicHashAlgorithm holds no unmanaged state and is not IDisposable.
	}
}
