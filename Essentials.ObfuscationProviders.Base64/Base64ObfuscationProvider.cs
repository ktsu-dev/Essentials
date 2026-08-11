// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Base64;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that composes a Base64 <see cref="IEncodingProvider"/>: obfuscation is
/// Base64 encoding, deobfuscation is Base64 decoding. This is NOT encryption.
/// </summary>
public class Base64ObfuscationProvider : IObfuscationProvider
{
	private readonly IEncodingProvider _encoder;

	/// <summary>Initializes a new instance using the default Base64 encoder.</summary>
	public Base64ObfuscationProvider() : this(new EncodingProviders.Base64.Base64EncodingProvider()) { }

	/// <summary>Initializes a new instance using the supplied encoder.</summary>
	/// <param name="encoder">The encoding provider used to perform the transform.</param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "Public constructor required for DI registration via factory lambda")]
	public Base64ObfuscationProvider(IEncodingProvider encoder) => _encoder = Ensure.NotNull(encoder);

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination) => _encoder.TryDecode(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination) => _encoder.TryDecode(obfuscatedData, destination);
}
