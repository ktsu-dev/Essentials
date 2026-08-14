// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CompressionProviders.Deflate;

using ktsu.Essentials;
using System;
using System.IO;
using System.IO.Compression;

/// <summary>
/// A compression provider that uses Deflate for data compression and decompression.
/// </summary>
public class DeflateCompressionProvider : ICompressionProvider
{
	/// <inheritdoc/>
	/// <remarks>
	/// Incompressible input can grow slightly: the deflate family emits stored blocks of up to 65535
	/// bytes with a 5-byte header each, plus a fixed container header and trailer. The margin below
	/// covers that for every algorithm here.
	/// </remarks>
	public int GetMaxCompressedLength(int sourceLength)
		=> sourceLength + (((sourceLength / 65535) + 1) * 5) + 64;

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
			using DeflateStream deflateStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
			data.CopyTo(deflateStream);
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
			using DeflateStream deflateStream = new(compressedData, CompressionMode.Decompress, leaveOpen: true);
			deflateStream.CopyTo(destination);
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
