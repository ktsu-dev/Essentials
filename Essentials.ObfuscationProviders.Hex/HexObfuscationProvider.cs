// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Hex;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that composes a Hex <see cref="IEncodingProvider"/>: obfuscation is hex
/// encoding, deobfuscation is hex decoding. This is NOT encryption.
/// </summary>
public class HexObfuscationProvider : IObfuscationProvider
{
	private readonly IEncodingProvider _encoder;

	/// <summary>Initializes a new instance using the default Hex encoder.</summary>
	public HexObfuscationProvider() : this(new EncodingProviders.Hex.HexEncodingProvider()) { }

	/// <summary>Initializes a new instance using the supplied encoder.</summary>
	/// <param name="encoder">The encoding provider used to perform the transform.</param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "Public constructor required for DI registration via factory lambda")]
	public HexObfuscationProvider(IEncodingProvider encoder) => _encoder = Ensure.NotNull(encoder);

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination) => _encoder.TryDecode(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination) => _encoder.TryDecode(obfuscatedData, destination);
}
