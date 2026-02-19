// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.HashProviders;

using System;
using System.IO;
using ktsu.Abstractions;

/// <summary>
/// A hash provider that uses FNV-1a 32-bit for hashing data.
/// </summary>
public class FNV1a_32 : IHashProvider
{
	private const uint FnvOffsetBasis32 = 0x811c9dc5;
	private const uint FnvPrime32 = 0x01000193;

	/// <summary>
	/// The length of the FNV-1a 32-bit hash in bytes (4 bytes / 32 bits).
	/// </summary>
	public int HashLengthBytes => 4;

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer using FNV-1a 32-bit.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		try
		{
			uint hash = FnvOffsetBasis32;

			foreach (byte b in data)
			{
				hash ^= b;
				hash *= FnvPrime32;
			}

			// Write the hash as little-endian bytes
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to hash the specified data from a stream into the provided hash buffer using FNV-1a 32-bit.
	/// </summary>
	/// <param name="data">The stream containing data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(Stream data, Span<byte> destination)
	{
		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		if (data is null)
		{
			return false;
		}

		try
		{
			uint hash = FnvOffsetBasis32;
			int b;

			while ((b = data.ReadByte()) != -1)
			{
				hash ^= (byte)b;
				hash *= FnvPrime32;
			}

			// Write the hash as little-endian bytes
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
		catch (NotSupportedException)
		{
			return false;
		}
	}
}
