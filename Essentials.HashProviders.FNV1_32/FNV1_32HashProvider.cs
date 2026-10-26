// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.HashProviders.FNV1_32;

using ktsu.Essentials;
using System;
using System.Buffers;
using System.IO;

/// <summary>
/// A hash provider that uses FNV-1 32-bit for hashing data.
/// </summary>
public class FNV1_32HashProvider : IHashProvider
{
	private const uint FnvOffsetBasis32 = 0x811c9dc5;
	private const uint FnvPrime32 = 0x01000193;

	/// <summary>How many bytes the stream path reads at a time.</summary>
	private const int StreamBufferSize = 81920;

	/// <summary>
	/// The length of the FNV-1 32-bit hash in bytes (4 bytes / 32 bits).
	/// </summary>
	public int HashLengthBytes => 4;

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer using FNV-1 32-bit.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		try
		{
			uint hash = FnvOffsetBasis32;

			foreach (byte b in data)
			{
				hash *= FnvPrime32;
				hash ^= b;
			}

			// Write the hash as little-endian bytes
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);

			bytesWritten = HashLengthBytes;
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to hash the specified data from a stream into the provided hash buffer using FNV-1 32-bit.
	/// </summary>
	/// <param name="data">The stream containing data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(Stream data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

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
			// A chunk at a time: the base Stream.ReadByte allocates and makes a Read call per byte, which on a
			// stream that does not override it cost eleven times as long and 32 bytes of garbage per byte (issue #91).
			byte[] buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
			try
			{
				int read;
				while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
				{
					for (int i = 0; i < read; i++)
					{
						hash *= FnvPrime32;
						hash ^= buffer[i];
					}
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
			}

			// Write the hash as little-endian bytes
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);

			bytesWritten = HashLengthBytes;
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

	/// <inheritdoc/>
	public IIncrementalHash CreateIncremental() => new Incremental();

	/// <summary>
	/// The running FNV-1 32-bit state. Naturally incremental: the accumulator is the entire state.
	/// </summary>
	private sealed class Incremental : IIncrementalHash
	{
		private uint hash = FnvOffsetBasis32;

		public int HashLengthBytes => 4;

		public void Append(ReadOnlySpan<byte> data)
		{
			foreach (byte b in data)
			{
				hash *= FnvPrime32;
				hash ^= b;
			}
		}

		public bool TryGetHashAndReset(Span<byte> destination, out int bytesWritten)
		{
			bytesWritten = 0;

			if (destination.Length < HashLengthBytes)
			{
				return false;
			}

			// Little-endian, matching the one-shot implementation above.
			destination[0] = (byte)(hash & 0xFF);
			destination[1] = (byte)((hash >> 8) & 0xFF);
			destination[2] = (byte)((hash >> 16) & 0xFF);
			destination[3] = (byte)((hash >> 24) & 0xFF);

			bytesWritten = HashLengthBytes;
			hash = FnvOffsetBasis32;
			return true;
		}

		public void Dispose()
		{
			// No unmanaged state.
		}
	}
}
