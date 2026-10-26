// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.BitRotate;

using ktsu.Essentials;
using System;
using System.Buffers;
using System.IO;

/// <summary>
/// An obfuscation provider that rotates the bits of each byte left when obfuscating and right when
/// deobfuscating. This is NOT encryption and provides no confidentiality.
/// </summary>
public class BitRotateObfuscationProvider : IObfuscationProvider
{
	/// <summary>How many bytes the stream paths transform at a time.</summary>
	private const int StreamBufferSize = 81920;

	private readonly int _bits;

	/// <summary>Initializes a new instance rotating by 3 bits.</summary>
	public BitRotateObfuscationProvider() : this(3) { }

	/// <summary>Initializes a new instance rotating by the specified number of bits (1–7).</summary>
	/// <param name="bits">The number of bits to rotate; must be between 1 and 7 inclusive.</param>
	public BitRotateObfuscationProvider(int bits)
	{
		if (bits is < 1 or > 7)
		{
			throw new ArgumentOutOfRangeException(nameof(bits), bits, "Rotation must be between 1 and 7 bits.");
		}

		_bits = bits;
	}

	private static byte RotateLeft(byte value, int bits) => (byte)((value << bits) | (value >> (8 - bits)));

	private static byte RotateRight(byte value, int bits) => (byte)((value >> bits) | (value << (8 - bits)));

	/// <inheritdoc/>
	public int GetMaxObfuscatedLength(int sourceLength) => sourceLength;

	/// <inheritdoc/>
	public int GetMaxDeobfuscatedLength(int obfuscatedLength) => obfuscatedLength;

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < data.Length)
		{
			return false;
		}

		// Copy first so a destination that overlaps the source is never read after it is written.
		data.CopyTo(destination);
		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = RotateLeft(destination[i], _bits);
		}

		bytesWritten = data.Length;
		return true;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		try
		{
			// A chunk at a time: the base Stream.ReadByte and WriteByte allocate and make a call per byte on any
			// stream that does not override them (issue #91).
			byte[] buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
			try
			{
				int read;
				while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
				{
					for (int i = 0; i < read; i++)
					{
						buffer[i] = RotateLeft(buffer[i], _bits);
					}

					destination.Write(buffer, 0, read);
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
			}

			return true;
		}
		catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
		{
			// A stream that cannot be read or written is a failure to report, as the encoder-backed
			// obfuscators already do (issue #86).
			return false;
		}
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < obfuscatedData.Length)
		{
			return false;
		}

		// Copy first so a destination that overlaps the source is never read after it is written.
		obfuscatedData.CopyTo(destination);
		for (int i = 0; i < obfuscatedData.Length; i++)
		{
			destination[i] = RotateRight(destination[i], _bits);
		}

		bytesWritten = obfuscatedData.Length;
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
	{
		if (obfuscatedData is null || destination is null)
		{
			return false;
		}

		try
		{
			// A chunk at a time: the base Stream.ReadByte and WriteByte allocate and make a call per byte on any
			// stream that does not override them (issue #91).
			byte[] buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
			try
			{
				int read;
				while ((read = obfuscatedData.Read(buffer, 0, buffer.Length)) > 0)
				{
					for (int i = 0; i < read; i++)
					{
						buffer[i] = RotateRight(buffer[i], _bits);
					}

					destination.Write(buffer, 0, read);
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
			}

			return true;
		}
		catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
		{
			// A stream that cannot be read or written is a failure to report, as the encoder-backed
			// obfuscators already do (issue #86).
			return false;
		}
	}
}
