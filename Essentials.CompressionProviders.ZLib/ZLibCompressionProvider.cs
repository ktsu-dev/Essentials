// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CompressionProviders.ZLib;

using ktsu.Essentials;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A compression provider that uses ZLib for data compression and decompression.
/// </summary>
public class ZLibCompressionProvider : ICompressionProvider
{
	/// <inheritdoc/>
	/// <remarks>
	/// This is zlib's <c>deflateBound</c> for the default parameters, plus the 6-byte zlib header and checksum. zlib closes a block each
	/// time its literal buffer fills, roughly every 16 KB, so incompressible input pays a stored-block
	/// header far more often than once per 64 KB. Computed in <see cref="long"/> so a large
	/// <paramref name="sourceLength"/> cannot overflow into a small bound.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The bound for <paramref name="sourceLength"/> exceeds <see cref="int.MaxValue"/>.</exception>
	public int GetMaxCompressedLength(int sourceLength)
	{
		long length = sourceLength;
		long bound = length + (length >> 12) + (length >> 14) + (length >> 25) + 13 + 6;
		return bound <= int.MaxValue
			? (int)bound
			: throw new ArgumentOutOfRangeException(nameof(sourceLength), sourceLength, "The compressed length bound exceeds the largest possible buffer.");
	}

	/// <summary>
	/// Tries to compress the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination to write the compressed data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public bool TryCompress(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		try
		{
			using MemoryStream inputStream = new();
			inputStream.Write(data);
			inputStream.Position = 0;
			using MemoryStream outputStream = new();

			if (!TryCompress(inputStream, outputStream))
			{
				return false;
			}

			byte[] compressedData = outputStream.ToArray();
			if (compressedData.Length > destination.Length)
			{
				return false;
			}

			compressedData.CopyTo(destination);
			bytesWritten = compressedData.Length;
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
		catch (InvalidDataException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to compress the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination to write the compressed data to.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public bool TryCompress(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		try
		{
			using ZLibStream zlibStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
			data.CopyTo(zlibStream);
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
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to compress the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: no thread is held for the duration. Disposal writes the trailer, so the
	/// compression stream is disposed with <c>await using</c> to keep that final write asynchronous too.
	/// If this throws or returns false, the destination may hold a partial result. Cancellation still
	/// flushes the trailer, so that partial result is structurally valid and cannot be distinguished
	/// from a complete one. Treat the destination as invalid unless the method returns true.
	/// </remarks>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination to write the compressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public async Task<bool> TryCompressAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			ZLibStream zlibStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
			await using (zlibStream.ConfigureAwait(false))
			{
				await data.CopyToAsync(zlibStream, 81920, cancellationToken).ConfigureAwait(false);
			}

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
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decompress the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public bool TryDecompress(ReadOnlySpan<byte> compressedData, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		try
		{
			using MemoryStream inputStream = new();
			inputStream.Write(compressedData);
			inputStream.Position = 0;
			using MemoryStream outputStream = new();

			if (!TryDecompress(inputStream, outputStream))
			{
				return false;
			}

			byte[] decompressedData = outputStream.ToArray();
			if (decompressedData.Length > destination.Length)
			{
				return false;
			}

			decompressedData.CopyTo(destination);
			bytesWritten = decompressedData.Length;
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
		catch (InvalidDataException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decompress the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public bool TryDecompress(Stream compressedData, Stream destination)
	{
		if (compressedData is null || destination is null)
		{
			return false;
		}

		try
		{
			using ZLibStream zlibStream = new(compressedData, CompressionMode.Decompress, leaveOpen: true);
			zlibStream.CopyTo(destination);
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
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decompress the data from the stream and write the result to the destination, asynchronously.
	/// </summary>
	/// <remarks>
	/// Genuinely asynchronous: no thread is held for the duration. The decompression stream wraps the
	/// source rather than the destination, so disposing it writes nothing to the destination.
	/// If this throws or returns false, the destination may hold a partial result. Decompressed bytes
	/// carry no structure of their own, so a truncated destination cannot be distinguished from a
	/// complete decompression by inspecting it. Treat the destination as invalid unless the method
	/// returns true.
	/// </remarks>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public async Task<bool> TryDecompressAsync(Stream compressedData, Stream destination, CancellationToken cancellationToken = default)
	{
		if (compressedData is null || destination is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			ZLibStream zlibStream = new(compressedData, CompressionMode.Decompress, leaveOpen: true);
			await using (zlibStream.ConfigureAwait(false))
			{
				await zlibStream.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
			}

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
		catch (InvalidDataException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
}
