// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.HashProviders.SHA1;

using ktsu.Essentials;
using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

/// <summary>
/// A hash provider that uses SHA-1 for hashing data.
/// </summary>
/// <remarks>
/// This type is stateless and safe to share across threads. Earlier versions held a single
/// <see cref="HashAlgorithm"/> in a field and reused it for every call; because the provider is
/// registered as a singleton, concurrent callers corrupted each other's in-progress hash state.
/// </remarks>
[SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "This provider exists specifically to implement SHA-1, which callers select deliberately for compatibility")]
public class SHA1HashProvider : IHashProvider
{
	/// <summary>
	/// The length of the SHA-1 hash in bytes (20 bytes / 160 bits).
	/// </summary>
	public int HashLengthBytes => 20;

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
			return System.Security.Cryptography.SHA1.TryHashData(data, destination, out bytesWritten);
#else
			using System.Security.Cryptography.SHA1 algorithm = System.Security.Cryptography.SHA1.Create();
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
			bytesWritten = System.Security.Cryptography.SHA1.HashData(data, destination);
			return bytesWritten == HashLengthBytes;
#else
			using System.Security.Cryptography.SHA1 algorithm = System.Security.Cryptography.SHA1.Create();
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

	/// <inheritdoc/>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership of the IncrementalHash transfers to the returned IncrementalHashAdapter, which disposes it.")]
	public IIncrementalHash CreateIncremental()
		=> new IncrementalHashAdapter(
			IncrementalHash.CreateHash(HashAlgorithmName.SHA1),
			HashLengthBytes);
}
