// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// An <see cref="IIncrementalHash"/> that accumulates every appended byte and hashes the result in
/// one pass, for providers that do not supply a genuinely incremental implementation.
/// </summary>
/// <remarks>
/// Correct for any provider, but it holds the whole input in memory, which is the cost this feature
/// exists to avoid. It backs the default body of <see cref="IHashProvider.CreateIncremental"/> so that
/// adding the member breaks no existing implementer; providers are expected to override it.
/// </remarks>
/// <param name="provider">The provider whose one-shot stream hashing is used to produce the digest.</param>
internal sealed class BufferingIncrementalHash(IHashProvider provider) : IIncrementalHash
{
	private readonly MemoryStream buffer = new();

	/// <inheritdoc/>
	public int HashLengthBytes => provider.HashLengthBytes;

	/// <inheritdoc/>
	public void Append(ReadOnlySpan<byte> data) => buffer.Write(data);

	/// <inheritdoc/>
	public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		buffer.Position = 0;
		bool hashed = provider.TryHash(buffer, destination, out bytesWritten);
		buffer.SetLength(0);
		return hashed;
	}

	/// <inheritdoc/>
	public void Dispose() => buffer.Dispose();
}
