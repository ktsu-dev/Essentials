// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < data.Length)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = (byte)(data[i] + _shift);
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

		int b;
		while ((b = data.ReadByte()) >= 0)
		{
			destination.WriteByte((byte)(b + _shift));
		}

		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
	{
		if (destination.Length < obfuscatedData.Length)
		{
			return false;
		}

		for (int i = 0; i < obfuscatedData.Length; i++)
		{
			destination[i] = (byte)(obfuscatedData[i] - _shift);
		}

		destination[obfuscatedData.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
	{
		if (obfuscatedData is null || destination is null)
		{
			return false;
		}

		int b;
		while ((b = obfuscatedData.ReadByte()) >= 0)
		{
			destination.WriteByte((byte)(b - _shift));
		}

		return true;
	}
}
