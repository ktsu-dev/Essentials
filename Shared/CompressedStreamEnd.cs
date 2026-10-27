// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Checks that a Gzip or ZLib stream reached its own end rather than stopping where its input ran out.
/// </summary>
/// <remarks>
/// <para>
/// Linked into the Gzip and ZLib providers rather than placed in the interfaces package, following
/// <c>BoundedDecompression</c>, so the interfaces take no dependency for it. <c>GZipStream</c> and <c>ZLibStream</c>
/// report the end of their input as a clean end of stream, so a payload cut off part way decompressed to a
/// shorter one and was reported as a success (issue #80). Both formats end in a checksum of everything before it,
/// which the decompressor verifies only if it gets that far: comparing the last bytes of the input against a
/// checksum of the output is what tells a complete stream from a truncated one.
/// </para>
/// <para>
/// The input's last bytes are its trailer only when the compressed data is all the input there is, so data
/// followed by anything else is reported as a failure. A Gzip file is allowed several members, read back to back
/// as gzip itself reads them; its trailer then describes only the last member, so a trailer whose size is less
/// than the whole output is taken for one, the members before it having been checked by the decompressor as it
/// crossed their trailers.
/// </para>
/// </remarks>
internal abstract class CompressedStreamEnd
{
	/// <summary>How many bytes of trailer the longest format here ends with.</summary>
	internal const int MaxTrailerLength = 8;

	private const int CopyBufferSize = 81920;

	/// <summary>Adds decompressed output to the running checksum.</summary>
	/// <param name="output">The next run of output.</param>
	internal abstract void Append(ReadOnlySpan<byte> output);

	/// <summary>Whether the input ended in the trailer the output so far calls for.</summary>
	/// <param name="inputTail">The last bytes of the input, up to <see cref="MaxTrailerLength"/> of them.</param>
	/// <returns><see langword="true"/> if the stream reached its own end.</returns>
	internal abstract bool EndsWith(ReadOnlySpan<byte> inputTail);

	/// <summary>
	/// Whether <paramref name="output"/> is everything <paramref name="input"/> describes, rather than what it
	/// held before it was cut off.
	/// </summary>
	/// <param name="output">Everything the decompressor produced.</param>
	/// <param name="input">The whole compressed input.</param>
	/// <returns><see langword="true"/> if the input ended in the trailer for that output.</returns>
	internal bool IsComplete(ReadOnlySpan<byte> output, ReadOnlySpan<byte> input)
	{
		// The providers compress empty data to no bytes at all, so no bytes read back as empty data.
		if (input.IsEmpty)
		{
			return output.IsEmpty;
		}

		Append(output);
		return EndsWith(input[Math.Max(0, input.Length - MaxTrailerLength)..]);
	}

	/// <summary>
	/// Decompresses <paramref name="compressedData"/> into <paramref name="destination"/>, then reports whether
	/// the stream reached its own end.
	/// </summary>
	/// <param name="compressedData">The compressed input.</param>
	/// <param name="destination">The stream to write the output to.</param>
	/// <param name="openDecompressor">Wraps the input in the provider's decompressing stream.</param>
	/// <returns><see langword="true"/> if the whole stream was decompressed.</returns>
	internal bool CopyTo(Stream compressedData, Stream destination, Func<Stream, Stream> openDecompressor)
	{
		using TailRecordingStream input = new(compressedData);
		using Stream decompressor = openDecompressor(input);

		byte[] buffer = new byte[CopyBufferSize];
		int read;
		while ((read = decompressor.Read(buffer, 0, buffer.Length)) > 0)
		{
			Append(buffer.AsSpan(0, read));
			destination.Write(buffer, 0, read);
		}

		// The providers compress empty data to no bytes at all, so no bytes read back as empty data.
		return input.Tail.IsEmpty || EndsWith(input.Tail);
	}

	/// <summary>The asynchronous counterpart of <see cref="CopyTo"/>, touching only the streams' asynchronous members.</summary>
	/// <param name="compressedData">The compressed input.</param>
	/// <param name="destination">The stream to write the output to.</param>
	/// <param name="openDecompressor">Wraps the input in the provider's decompressing stream.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the whole stream was decompressed.</returns>
	internal async Task<bool> CopyToAsync(Stream compressedData, Stream destination, Func<Stream, Stream> openDecompressor, CancellationToken cancellationToken)
	{
		using TailRecordingStream input = new(compressedData);
		Stream decompressor = openDecompressor(input);
		await using (decompressor.ConfigureAwait(false))
		{
			byte[] buffer = new byte[CopyBufferSize];
			int read;
			while ((read = await decompressor.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
			{
				Append(buffer.AsSpan(0, read));
				await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
			}
		}

		// The providers compress empty data to no bytes at all, so no bytes read back as empty data.
		return input.Tail.IsEmpty || EndsWith(input.Tail);
	}

	/// <summary>Gzip's trailer: the CRC-32 of the member's output, then its length modulo 2^32, both little-endian.</summary>
	internal sealed class Gzip : CompressedStreamEnd
	{
		private uint crc = 0xFFFFFFFFu;
		private uint length;

		internal override void Append(ReadOnlySpan<byte> output)
		{
			crc = Crc32.Update(crc, output);
			length = unchecked(length + (uint)output.Length);
		}

		internal override bool EndsWith(ReadOnlySpan<byte> inputTail)
		{
			if (inputTail.Length < 8)
			{
				return false;
			}

			uint trailerCrc = ReadUInt32LittleEndian(inputTail[^8..]);
			uint trailerLength = ReadUInt32LittleEndian(inputTail[^4..]);
			return (trailerCrc == ~crc && trailerLength == length) || trailerLength < length;
		}
	}

	/// <summary>ZLib's trailer: the Adler-32 of the output, big-endian.</summary>
	internal sealed class ZLib : CompressedStreamEnd
	{
		private const uint Modulus = 65521;

		// The largest run whose sums cannot overflow 32 bits before they are reduced.
		private const int MaxRun = 5552;

		private uint a = 1;
		private uint b;

		internal override void Append(ReadOnlySpan<byte> output)
		{
			while (!output.IsEmpty)
			{
				int run = Math.Min(output.Length, MaxRun);
				foreach (byte value in output[..run])
				{
					a += value;
					b += a;
				}

				a %= Modulus;
				b %= Modulus;
				output = output[run..];
			}
		}

		internal override bool EndsWith(ReadOnlySpan<byte> inputTail)
		{
			if (inputTail.Length < 4)
			{
				return false;
			}

			ReadOnlySpan<byte> trailer = inputTail[^4..];
			uint expected = ((uint)trailer[0] << 24) | ((uint)trailer[1] << 16) | ((uint)trailer[2] << 8) | trailer[3];
			return expected == ((b << 16) | a);
		}
	}

	private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> bytes)
		=> bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16) | ((uint)bytes[3] << 24);

	/// <summary>The CRC-32 Gzip uses (the reflected IEEE 802.3 polynomial), table driven.</summary>
	private static class Crc32
	{
		private static readonly uint[] Table = CreateTable();

		internal static uint Update(uint crc, ReadOnlySpan<byte> data)
		{
			foreach (byte value in data)
			{
				crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
			}

			return crc;
		}

		private static uint[] CreateTable()
		{
			uint[] table = new uint[256];
			for (uint n = 0; n < table.Length; n++)
			{
				uint c = n;
				for (int k = 0; k < 8; k++)
				{
					c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
				}

				table[n] = c;
			}

			return table;
		}
	}

	/// <summary>
	/// Passes reads through to the compressed input, keeping the last <see cref="MaxTrailerLength"/> bytes read.
	/// Disposing it leaves the input open, as the providers' decompressors are told to.
	/// </summary>
	private sealed class TailRecordingStream(Stream inner) : Stream
	{
		private readonly byte[] tail = new byte[MaxTrailerLength];
		private int tailLength;

		/// <summary>Gets the last bytes read, oldest first.</summary>
		internal ReadOnlySpan<byte> Tail => tail.AsSpan(MaxTrailerLength - tailLength);

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int read = inner.Read(buffer, offset, count);
			Record(buffer.AsSpan(offset, read));
			return read;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			int read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
			Record(buffer.AsSpan(offset, read));
			return read;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			int read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
			Record(buffer.Span[..read]);
			return read;
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		private void Record(ReadOnlySpan<byte> read)
		{
			if (read.Length >= MaxTrailerLength)
			{
				read[^MaxTrailerLength..].CopyTo(tail);
				tailLength = MaxTrailerLength;
				return;
			}

			// Shift the kept bytes down to make room, keeping them in the order they were read.
			tail.AsSpan(read.Length).CopyTo(tail);
			read.CopyTo(tail.AsSpan(MaxTrailerLength - read.Length));
			tailLength = Math.Min(MaxTrailerLength, tailLength + read.Length);
		}
	}
}
