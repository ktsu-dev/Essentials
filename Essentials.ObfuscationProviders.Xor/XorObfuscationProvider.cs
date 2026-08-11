// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Xor;

using System;
using System.IO;
using ktsu.Essentials;

/// <summary>
/// An obfuscation provider that XORs each byte with a repeating key. Self-inverse: obfuscation and
/// deobfuscation are the same transform. This is NOT encryption and provides no confidentiality.
/// </summary>
public class XorObfuscationProvider : IObfuscationProvider
{
	private readonly byte[] _key;

	/// <summary>Initializes a new instance with the default single-byte key.</summary>
	public XorObfuscationProvider() : this([0x5A]) { }

	/// <summary>Initializes a new instance with the specified repeating key.</summary>
	/// <param name="key">The non-empty key bytes to XOR against.</param>
	public XorObfuscationProvider(byte[] key)
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