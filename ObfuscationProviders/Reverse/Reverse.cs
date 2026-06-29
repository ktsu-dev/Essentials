// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that reverses the byte order. Self-inverse. This is NOT encryption and
/// provides no confidentiality.
/// </summary>
public class Reverse : IObfuscationProvider
{
	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < data.Length)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = data[data.Length - 1 - i];
		}

		destination[data.Length..].Clear();
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
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
		=> TryObfuscate(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
		=> TryObfuscate(obfuscatedData, destination);
}
