// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders.Composite;

using System;
using System.Collections.Generic;
using System.IO;
using ktsu.Essentials;

/// <summary>
/// An obfuscation provider that pipelines an ordered list of obfuscators. Obfuscation applies the
/// stages in order; deobfuscation applies them in reverse order. This is NOT encryption.
/// </summary>
public class CompositeObfuscationProvider : IObfuscationProvider
{
	private readonly IReadOnlyList<IObfuscationProvider> _stages;

	/// <summary>Initializes a new instance with the ordered obfuscation stages.</summary>
	/// <param name="stages">The non-empty ordered list of obfuscators to pipeline.</param>
	public CompositeObfuscationProvider(IReadOnlyList<IObfuscationProvider> stages)
	{
		Ensure.NotNull(stages);
		if (stages.Count == 0)
		{
			throw new ArgumentException("At least one stage is required.", nameof(stages));
		}

		_stages = stages;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		byte[] current = data.ToArray();
		foreach (IObfuscationProvider stage in _stages)
		{
			current = stage.Obfuscate(current);
		}

		if (destination.Length < current.Length)
		{
			return false;
		}

		current.CopyTo(destination);
		destination[current.Length..].Clear();
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
		byte[] current = buffer.ToArray();
		foreach (IObfuscationProvider stage in _stages)
		{
			current = stage.Obfuscate(current);
		}

		destination.Write(current, 0, current.Length);
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
	{
		byte[] current = obfuscatedData.ToArray();
		for (int i = _stages.Count - 1; i >= 0; i--)
		{
			current = _stages[i].Deobfuscate(current);
		}

		if (destination.Length < current.Length)
		{
			return false;
		}

		current.CopyTo(destination);
		destination[current.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
	{
		if (obfuscatedData is null || destination is null)
		{
			return false;
		}

		using MemoryStream buffer = new();
		obfuscatedData.CopyTo(buffer);
		byte[] current = buffer.ToArray();
		for (int i = _stages.Count - 1; i >= 0; i--)
		{
			current = _stages[i].Deobfuscate(current);
		}

		destination.Write(current, 0, current.Length);
		return true;
	}
}