// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that composes a Base64 <see cref="IEncodingProvider"/>: obfuscation is
/// Base64 encoding, deobfuscation is Base64 decoding. This is NOT encryption.
/// </summary>
public class Base64 : IObfuscationProvider
{
	private readonly IEncodingProvider _encoder;

	/// <summary>Initializes a new instance using the default Base64 encoder.</summary>
	public Base64() : this(new EncodingProviders.Base64()) { }

	/// <summary>Initializes a new instance using the supplied encoder.</summary>
	/// <param name="encoder">The encoding provider used to perform the transform.</param>
	private Base64(IEncodingProvider encoder) => _encoder = Ensure.NotNull(encoder);

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination) => _encoder.TryDecode(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination) => _encoder.TryDecode(obfuscatedData, destination);
}
