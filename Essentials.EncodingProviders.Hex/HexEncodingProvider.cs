// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.EncodingProviders.Hex;

using ktsu.Essentials;
using System;
using System.IO;

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
}
