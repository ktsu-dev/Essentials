// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;

/// <summary>
/// The span-to-span decompression shared by the Brotli, Deflate, Gzip and ZLib providers.
/// </summary>
/// <remarks>
/// <para>
/// Linked into each provider project rather than placed in the interfaces package, following
/// <c>DeflateBound</c>. Each provider used to inflate the whole input into a growing
/// <see cref="MemoryStream"/> and only then compare the result against the destination, so the
/// destination never bounded the work: a 405-byte Brotli input of 256 MiB of zeros allocated 767 MiB
/// before returning <see langword="false"/>, and past 2 GiB the process ran out of memory. See issue #122.
/// </para>
/// <para>
/// This reads straight into the destination and stops as soon as it is full, so the memory a call costs
/// is the copy of the input plus the decompressor's own buffers, whatever the input expands to.
/// </para>
/// </remarks>
internal static class BoundedDecompression
{
	/// <summary>Whether <paramref name="output"/> is everything <paramref name="input"/> describes.</summary>
	/// <param name="output">Everything the decompressor produced.</param>
	/// <param name="input">The whole compressed input.</param>
	/// <returns><see langword="true"/> if the input was not cut off.</returns>
	internal delegate bool CompletenessCheck(ReadOnlySpan<byte> output, ReadOnlySpan<byte> input);

	/// <summary>
	/// Decompresses <paramref name="compressedData"/> into <paramref name="destination"/>, failing once the
	/// destination is full and more output remains.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <param name="openDecompressor">Wraps the compressed input in the provider's decompressing stream.</param>
	/// <param name="isComplete">
	/// Tells a stream that reached its own end from one whose input ran out, which the decompressing stream reports
	/// the same way (issue #80), or <see langword="null"/> for a format that cannot tell.
	/// </param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>, or zero on failure.</param>
	/// <returns><see langword="true"/> if the whole output fit in <paramref name="destination"/>; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// Errors in the compressed data surface as the decompressor throws them, for the caller's own
	/// <c>catch</c> blocks to turn into <see langword="false"/>. Input that ends early is reported as
	/// <paramref name="isComplete"/> decides, as the stream overloads report it.
	/// </remarks>
	internal static bool TryDecompress(ReadOnlySpan<byte> compressedData, Span<byte> destination, Func<Stream, Stream> openDecompressor, CompletenessCheck? isComplete, out int bytesWritten)
	{
		bytesWritten = 0;

		using MemoryStream input = new(compressedData.ToArray(), writable: false);
		using Stream decompressor = openDecompressor(input);

		int written = 0;
		while (written < destination.Length)
		{
			int read = decompressor.Read(destination[written..]);
			if (read == 0)
			{
				return Finish(compressedData, destination[..written], isComplete, out bytesWritten);
			}

			written += read;
		}

		// The destination is full. One more byte from the decompressor means the output does not fit, and
		// asking for a single byte keeps that answer from costing more than the decompressor's own buffer.
		Span<byte> probe = stackalloc byte[1];
		if (decompressor.Read(probe) > 0)
		{
			return false;
		}

		return Finish(compressedData, destination[..written], isComplete, out bytesWritten);
	}

	private static bool Finish(ReadOnlySpan<byte> compressedData, ReadOnlySpan<byte> output, CompletenessCheck? isComplete, out int bytesWritten)
	{
		if (isComplete is not null && !isComplete(output, compressedData))
		{
			bytesWritten = 0;
			return false;
		}

		bytesWritten = output.Length;
		return true;
	}
}
