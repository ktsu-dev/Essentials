// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CompressionProviders.Brotli;

using ktsu.Essentials;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A compression provider that uses Brotli for data compression and decompression.
/// </summary>
public class BrotliCompressionProvider : ICompressionProvider
{
	/// <inheritdoc/>
	/// <remarks>Delegates to Brotli's own worst-case bound rather than approximating it.</remarks>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceLength"/> is negative or larger than Brotli can bound.</exception>
	public int GetMaxCompressedLength(int sourceLength)
		=> BrotliEncoder.GetMaxCompressedLength(sourceLength);

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
			using BrotliStream brotliStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
			data.CopyTo(brotliStream);
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
		catch (NotSupportedException)
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
			BrotliStream brotliStream = new(destination, CompressionLevel.Optimal, leaveOpen: true);
			await using (brotliStream.ConfigureAwait(false))
			{
				await data.CopyToAsync(brotliStream, 81920, cancellationToken).ConfigureAwait(false);
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
		catch (NotSupportedException)
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
			// BrotliDecoder writes straight into the destination, so the destination bounds the work (issue #122),
			// and it reports whether the final block arrived, which BrotliStream does not: input cut off part way
			// read as a clean end and decompressed to a shorter output (issue #80).
			if (!BrotliDecoder.TryDecompress(compressedData, destination, out bytesWritten))
			{
				bytesWritten = 0;
				return false;
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
			BrotliDecoder decoder = new();
			try
			{
				byte[] input = new byte[BufferSize];
				byte[] output = new byte[BufferSize];
				int offset = 0;
				int available = 0;
				bool ended = false;
				while (true)
				{
					if (offset == available && !ended)
					{
						available = compressedData.Read(input, 0, input.Length);
						offset = 0;
						ended = available == 0;
					}

					OperationStatus status = decoder.Decompress(input.AsSpan(offset, available - offset), output, out int consumed, out int written);
					offset += consumed;
					destination.Write(output, 0, written);
					if (IsFinished(status, ended, out bool complete))
					{
						return complete;
					}
				}
			}
			finally
			{
				decoder.Dispose();
			}
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
		catch (NotSupportedException)
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
			BrotliDecoder decoder = new();
			try
			{
				byte[] input = new byte[BufferSize];
				byte[] output = new byte[BufferSize];
				int offset = 0;
				int available = 0;
				bool ended = false;
				while (true)
				{
					if (offset == available && !ended)
					{
						available = await compressedData.ReadAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
						offset = 0;
						ended = available == 0;
					}

					OperationStatus status = decoder.Decompress(input.AsSpan(offset, available - offset), output, out int consumed, out int written);
					offset += consumed;
					await destination.WriteAsync(output.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
					if (IsFinished(status, ended, out bool complete))
					{
						return complete;
					}
				}
			}
			finally
			{
				decoder.Dispose();
			}
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
		catch (NotSupportedException)
		{
			return false;
		}
	}

	/// <summary>How many bytes of input and of output the stream paths work through at a time.</summary>
	private const int BufferSize = 81920;

	/// <summary>
	/// Reads one step of a <see cref="BrotliDecoder"/> stream loop. Decoding is driven by the decoder rather than
	/// <see cref="BrotliStream"/> because only the decoder says whether the final block arrived: input that ends
	/// before it is truncated, which <see cref="BrotliStream"/> reports as a clean end (issue #80).
	/// </summary>
	/// <param name="status">What the decoder reported.</param>
	/// <param name="ended">Whether the input has run out.</param>
	/// <param name="complete">When finished, whether the whole stream was decoded.</param>
	/// <returns><see langword="true"/> when the loop is done, with <paramref name="complete"/> as its answer.</returns>
	private static bool IsFinished(OperationStatus status, bool ended, out bool complete)
	{
		switch (status)
		{
			case OperationStatus.Done:
				complete = true;
				return true;
			case OperationStatus.NeedMoreData when ended:
			case OperationStatus.InvalidData:
				complete = false;
				return true;
			default:
				// More output to collect, or more input to read.
				complete = false;
				return false;
		}
	}
}
