// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.HashProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// A hash provider that uses FNV-1 64-bit for hashing data.
/// </summary>
public class FNV1_64 : IHashProvider
{
	private const ulong FnvOffsetBasis64 = 0xcbf29ce484222325;
	private const ulong FnvPrime64 = 0x00000100000001b3;

	/// <summary>
	/// The length of the FNV-1 64-bit hash in bytes (8 bytes / 64 bits).
	/// </summary>
	public int HashLengthBytes => 8;

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer using FNV-1 64-bit.
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
			ulong hash = FnvOffsetBasis64;

			foreach (byte b in data)
			{
				hash *= FnvPrime64;
				hash ^= b;
			}

			// Write the hash as little-endian bytes
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);
			destination[4] = (byte)((hash >> 32) & 0xFF);
			destination[5] = (byte)((hash >> 40) & 0xFF);
			destination[6] = (byte)((hash >> 48) & 0xFF);
			destination[7] = (byte)((hash >> 56) & 0xFF);

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to hash the specified data from a stream into the provided hash buffer using FNV-1 64-bit.
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
			ulong hash = FnvOffsetBasis64;
			int b;

			while ((b = data.ReadByte()) != -1)
			{
				hash *= FnvPrime64;
				hash ^= (byte)b;
			}

			// Write the hash as little-endian bytes
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);
			destination[4] = (byte)((hash >> 32) & 0xFF);
			destination[5] = (byte)((hash >> 40) & 0xFF);
			destination[6] = (byte)((hash >> 48) & 0xFF);
			destination[7] = (byte)((hash >> 56) & 0xFF);

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
