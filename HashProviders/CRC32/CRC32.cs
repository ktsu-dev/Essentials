// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.HashProviders;

using System;
using System.IO;
using System.IO.Hashing;
using ktsu.Abstractions;

/// <summary>
/// A hash provider that uses CRC-32 for hashing data.
/// </summary>
public class CRC32 : IHashProvider
{
	/// <summary>
	/// The length of the CRC-32 hash in bytes (4 bytes / 32 bits).
	/// </summary>
	public int HashLengthBytes => 4;

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer using CRC-32.
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
			return Crc32.TryHash(data, destination, out int bytesWritten)
				&& bytesWritten == HashLengthBytes;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to hash the specified data from a stream into the provided hash buffer using CRC-32.
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
			Crc32 hasher = new();
			hasher.Append(data);
			return hasher.TryGetHashAndReset(destination, out int bytesWritten)
				&& bytesWritten == HashLengthBytes;
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
