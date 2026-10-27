// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Caesar;

using System;
using System.IO;
using ktsu.Essentials;

/// <summary>
/// An obfuscation provider that adds a fixed shift to each byte (wrapping at 256). This is NOT
/// encryption and provides no confidentiality.
/// </summary>
public class CaesarObfuscationProvider(byte shift = 13) : IObfuscationProvider
{
	private readonly byte _shift = shift;

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
			destination[i] = (byte)(destination[i] + _shift);
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
			StreamChunks.ForEach(
				data,
				chunk =>
				{
					for (int i = 0; i < chunk.Length; i++)
					{
						chunk[i] = (byte)(chunk[i] + _shift);
					}
				},
				destination);

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
			destination[i] = (byte)(destination[i] - _shift);
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
			StreamChunks.ForEach(
				obfuscatedData,
				chunk =>
				{
					for (int i = 0; i < chunk.Length; i++)
					{
						chunk[i] = (byte)(chunk[i] - _shift);
					}
				},
				destination);

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
