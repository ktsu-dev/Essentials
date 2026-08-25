// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// An <see cref="IIncrementalHash"/> that accumulates every appended byte and hashes the result in
/// one pass, for keyed hash providers that do not supply a genuinely incremental implementation.
/// </summary>
/// <remarks>
/// Correct for any provider, but it holds the whole input in memory, which is the cost incremental
/// hashing exists to avoid. It backs the default body of
/// <see cref="IKeyedHashProvider.CreateIncremental"/> so that implementers need only write the two
/// required primitives; providers are expected to override it. The key is copied on construction and
/// zeroed on disposal, so the instance must be disposed.
/// </remarks>
internal sealed class BufferingKeyedIncrementalHash : IIncrementalHash
{
	private readonly IKeyedHashProvider provider;
	private readonly byte[] keyCopy;
	private readonly MemoryStream buffer = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="BufferingKeyedIncrementalHash"/> class.
	/// </summary>
	/// <param name="keyedHashProvider">The provider whose one-shot stream hashing produces the digest.</param>
	/// <param name="key">The key, copied into this instance and zeroed on disposal.</param>
	internal BufferingKeyedIncrementalHash(IKeyedHashProvider keyedHashProvider, ReadOnlySpan<byte> key)
	{
		provider = keyedHashProvider;
		keyCopy = key.ToArray();
	}

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
		bool hashed = provider.TryHash(keyCopy, buffer, destination, out bytesWritten);
		buffer.SetLength(0);
		return hashed;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		CryptographicOperations.ZeroMemory(keyCopy);
		buffer.Dispose();
	}
}
