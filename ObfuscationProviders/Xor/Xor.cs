// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ktsu.Essentials;

/// <summary>
/// An obfuscation provider that XORs each byte with a repeating key. Self-inverse: obfuscation and
/// deobfuscation are the same transform. This is NOT encryption and provides no confidentiality.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Xor is the appropriate name for this obfuscation provider")]
public class Xor : IObfuscationProvider
{
	private readonly byte[] _key;

	/// <summary>Initializes a new instance with the default single-byte key.</summary>
	public Xor() : this([0x5A]) { }

	/// <summary>Initializes a new instance with the specified repeating key.</summary>
	/// <param name="key">The non-empty key bytes to XOR against.</param>
	public Xor(byte[] key)
	{
		Ensure.NotNull(key);
		if (key.Length == 0)
		{
			throw new ArgumentException("Key must contain at least one byte.", nameof(key));
		}

		_key = key;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < data.Length)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = (byte)(data[i] ^ _key[i % _key.Length]);
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
		long i = 0;
		while ((b = data.ReadByte()) >= 0)
		{
			destination.WriteByte((byte)(b ^ _key[(int)(i % _key.Length)]));
			i++;
		}

		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
		=> TryObfuscate(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
		=> TryObfuscate(obfuscatedData, destination);
}