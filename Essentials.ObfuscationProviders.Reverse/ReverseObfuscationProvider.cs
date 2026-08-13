// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Reverse;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that reverses the byte order. Self-inverse. This is NOT encryption and
/// provides no confidentiality.
/// </summary>
public class ReverseObfuscationProvider : IObfuscationProvider
{
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

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = data[data.Length - 1 - i];
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

		using MemoryStream buffer = new();
		data.CopyTo(buffer);
		byte[] bytes = buffer.ToArray();
		Array.Reverse(bytes);
		destination.Write(bytes, 0, bytes.Length);
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination, out int bytesWritten)
		=> TryObfuscate(obfuscatedData, destination, out bytesWritten);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
		=> TryObfuscate(obfuscatedData, destination);
}
