// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.HashProviders.SHA512;

using ktsu.Essentials;
using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// A hash provider that uses SHA-512 for hashing data.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads. Earlier versions held a single
/// <see cref="HashAlgorithm"/> in a field and reused it for every call; because the provider is
/// registered as a singleton, concurrent callers corrupted each other's in-progress hash state.
/// </remarks>
public class SHA512HashProvider : IHashProvider
{
	/// <summary>
	/// The length of the SHA-512 hash in bytes (64 bytes / 512 bits).
	/// </summary>
	public int HashLengthBytes => 64;

	/// <inheritdoc/>
	public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		try
		{
#if NET6_0_OR_GREATER
			return System.Security.Cryptography.SHA512.TryHashData(data, destination, out bytesWritten);
#else
			using System.Security.Cryptography.SHA512 algorithm = System.Security.Cryptography.SHA512.Create();
			return algorithm.TryComputeHash(data, destination, out bytesWritten);
#endif
		}
		catch (ArgumentException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (CryptographicException)
		{
			bytesWritten = 0;
			return false;
		}
	}

	/// <inheritdoc/>
	public bool TryHash(Stream data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (data is null || destination.Length < HashLengthBytes)
		{
			return false;
		}

		try
		{
#if NET6_0_OR_GREATER
			bytesWritten = System.Security.Cryptography.SHA512.HashData(data, destination);
			return bytesWritten == HashLengthBytes;
#else
			using System.Security.Cryptography.SHA512 algorithm = System.Security.Cryptography.SHA512.Create();
			byte[] hash = algorithm.ComputeHash(data);
			if (hash.Length != HashLengthBytes)
			{
				return false;
			}

			hash.CopyTo(destination);
			bytesWritten = hash.Length;
			return true;
#endif
		}
		catch (ArgumentException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (CryptographicException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (IOException)
		{
			bytesWritten = 0;
			return false;
		}
	}
}
