// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.EncodingProviders.Hex;

using ktsu.Essentials;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An encoding provider that uses hexadecimal encoding for data encoding and decoding.
/// </summary>
/// <remarks>
/// The span paths convert byte by byte, so they neither allocate an intermediate string nor copy
/// through a temporary array. Encoding emits uppercase; decoding accepts either case.
/// </remarks>
public class HexEncodingProvider : IEncodingProvider
{
	private const string HexDigits = "0123456789ABCDEF";

	/// <inheritdoc/>
	public int GetMaxEncodedLength(int sourceLength) => sourceLength * 2;

	/// <inheritdoc/>
	public int GetMaxDecodedLength(int encodedLength) => encodedLength / 2;

	/// <inheritdoc/>
	public bool TryEncode(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < data.Length * 2)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i * 2] = (byte)HexDigits[data[i] >> 4];
			destination[(i * 2) + 1] = (byte)HexDigits[data[i] & 0x0F];
		}

		bytesWritten = data.Length * 2;
		return true;
	}

	/// <inheritdoc/>
	public bool TryEncode(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		try
		{
			int b;
			while ((b = data.ReadByte()) >= 0)
			{
				destination.WriteByte((byte)HexDigits[b >> 4]);
				destination.WriteByte((byte)HexDigits[b & 0x0F]);
			}

			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public bool TryDecode(ReadOnlySpan<byte> encodedData, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (encodedData.Length % 2 != 0 || destination.Length < encodedData.Length / 2)
		{
			return false;
		}

		for (int i = 0; i < encodedData.Length; i += 2)
		{
			if (!TryParseNibble(encodedData[i], out int high) || !TryParseNibble(encodedData[i + 1], out int low))
			{
				bytesWritten = 0;
				return false;
			}

			destination[i / 2] = (byte)((high << 4) | low);
		}

		bytesWritten = encodedData.Length / 2;
		return true;
	}

	/// <inheritdoc/>
	public bool TryDecode(Stream encodedData, Stream destination)
	{
		if (encodedData is null || destination is null)
		{
			return false;
		}

		try
		{
			while (true)
			{
				int first = encodedData.ReadByte();
				if (first < 0)
				{
					return true;
				}

				int second = encodedData.ReadByte();
				if (second < 0
					|| !TryParseNibble((byte)first, out int high)
					|| !TryParseNibble((byte)second, out int low))
				{
					return false;
				}

				destination.WriteByte((byte)((high << 4) | low));
			}
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	private static bool TryParseNibble(byte character, out int value)
	{
		value = character switch
		{
			>= (byte)'0' and <= (byte)'9' => character - '0',
			>= (byte)'A' and <= (byte)'F' => character - 'A' + 10,
			>= (byte)'a' and <= (byte)'f' => character - 'a' + 10,
			_ => -1,
		};

		return value >= 0;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Genuinely asynchronous: the source is read and the result written with <c>ReadAsync</c> and
	/// <c>WriteAsync</c>, so no thread is held for the duration of the I/O. Declaring this member and
	/// its decoding counterpart replaces the interface's <c>Task.Run</c> defaults and converts every
	/// stream path derived from them.
	/// <para>
	/// It transforms a chunk at a time rather than buffering the whole stream, which keeps the
	/// synchronous path's constant memory use. Hex encodes one byte to two, so a chunk boundary never
	/// splits a group and no state has to be carried across one.
	/// </para>
	/// </remarks>
	public async Task<bool> TryEncodeAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		byte[] source = new byte[ChunkSize];
		byte[] encoded = new byte[ChunkSize * 2];

		try
		{
			int read;
			while ((read = await data.ReadAsync(source.AsMemory(0, ChunkSize), cancellationToken).ConfigureAwait(false)) > 0)
			{
				for (int i = 0; i < read; i++)
				{
					encoded[i * 2] = (byte)HexDigits[source[i] >> 4];
					encoded[(i * 2) + 1] = (byte)HexDigits[source[i] & 0x0F];
				}

				await destination.WriteAsync(encoded.AsMemory(0, read * 2), cancellationToken).ConfigureAwait(false);
			}

			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Genuinely asynchronous, and chunked, on the same terms as
	/// <see cref="TryEncodeAsync(Stream, Stream, CancellationToken)"/>.
	/// <para>
	/// Decoding reads two characters per byte, so a chunk can end mid-pair. A read is therefore
	/// topped up until it holds an even number of characters, or the stream ends — in which case a
	/// leftover character means the input was truncated, and this reports failure exactly as the
	/// synchronous path does.
	/// </para>
	/// </remarks>
	public async Task<bool> TryDecodeAsync(Stream encodedData, Stream destination, CancellationToken cancellationToken = default)
	{
		if (encodedData is null || destination is null)
		{
			return false;
		}

		byte[] source = new byte[ChunkSize];
		byte[] decoded = new byte[ChunkSize / 2];

		try
		{
			while (true)
			{
				int filled = await ReadPairsAsync(encodedData, source, cancellationToken).ConfigureAwait(false);
				if (filled == 0)
				{
					return true;
				}

				if (filled < 0)
				{
					// An odd number of characters: the last byte has no partner, so the input is truncated.
					return false;
				}

				for (int i = 0; i < filled; i += 2)
				{
					if (!TryParseNibble(source[i], out int high) || !TryParseNibble(source[i + 1], out int low))
					{
						return false;
					}

					decoded[i / 2] = (byte)((high << 4) | low);
				}

				await destination.WriteAsync(decoded.AsMemory(0, filled / 2), cancellationToken).ConfigureAwait(false);
			}
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Fills a buffer with an even number of characters, so no character pair straddles a chunk.
	/// </summary>
	/// <param name="source">The stream to read.</param>
	/// <param name="buffer">The buffer to fill.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// How many characters were read, zero at a clean end of stream, or -1 if the stream ended on an
	/// unpaired character.
	/// </returns>
	/// <remarks>
	/// A single <c>ReadAsync</c> may return fewer bytes than asked for at any time — that is the
	/// stream contract, not an end-of-stream signal — so this keeps reading until the buffer is full
	/// or the stream really has ended.
	/// </remarks>
	private static async Task<int> ReadPairsAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
	{
		int filled = 0;
		while (filled < buffer.Length)
		{
			int read = await source.ReadAsync(buffer.AsMemory(filled, buffer.Length - filled), cancellationToken).ConfigureAwait(false);
			if (read == 0)
			{
				break;
			}

			filled += read;
		}

		return filled % 2 == 0 ? filled : -1;
	}

	/// <summary>
	/// The number of characters transformed per chunk. Even, so a pair is never split.
	/// </summary>
	private const int ChunkSize = 8192;
}
