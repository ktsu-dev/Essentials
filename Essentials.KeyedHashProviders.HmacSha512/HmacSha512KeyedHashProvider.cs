// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.KeyedHashProviders.HmacSha512;

using System;
using System.IO;
using System.Security.Cryptography;
using ktsu.Essentials;

/// <summary>
/// A keyed hash provider that uses HMAC-SHA-512 to authenticate data.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads, because the key is supplied per call
/// rather than held in a field. Every operation delegates to the shared HMAC core, which owns key
/// copying and zeroing.
/// </remarks>
public class HmacSha512KeyedHashProvider : IKeyedHashProvider
{
	/// <summary>
	/// The length of the HMAC-SHA-512 tag in bytes (64 bytes / 512 bits).
	/// </summary>
	public int HashLengthBytes => 64;

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA512, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)
		=> HmacKeyedHashCore.TryHash(HashAlgorithmName.SHA512, HashLengthBytes, key, data, destination, out bytesWritten);

	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental(ReadOnlySpan<byte> key)
		=> HmacKeyedHashCore.CreateIncremental(HashAlgorithmName.SHA512, HashLengthBytes, key);
}
