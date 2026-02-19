// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.HashProviders;

using ktsu.Essentials;
using System;
using System.IO;
using SysXxHash128 = System.IO.Hashing.XxHash128;

/// <summary>
/// A hash provider that uses xxHash128 for hashing data.
/// </summary>
public class XxHash128 : IHashProvider
{
	/// <summary>
	/// The length of the xxHash128 hash in bytes (16 bytes / 128 bits).
	/// </summary>
	public int HashLengthBytes => 16;

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer using xxHash128.
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
			return SysXxHash128.TryHash(data, destination, out int bytesWritten)
				&& bytesWritten == HashLengthBytes;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to hash the specified data from a stream into the provided hash buffer using xxHash128.
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
			SysXxHash128 hasher = new();
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
