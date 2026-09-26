// Copyright (c) 2023-2026 ktsu-dev contributors

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
	/// <remarks>Each stage can expand its input, so the bound is the composition of every stage's bound.</remarks>
	public int GetMaxObfuscatedLength(int sourceLength)
	{
		int length = sourceLength;
		foreach (IObfuscationProvider stage in _stages)
		{
			length = stage.GetMaxObfuscatedLength(length);
		}

		return length;
	}

	/// <inheritdoc/>
	public int GetMaxDeobfuscatedLength(int obfuscatedLength)
	{
		int length = obfuscatedLength;
		for (int i = _stages.Count - 1; i >= 0; i--)
		{
			length = _stages[i].GetMaxDeobfuscatedLength(length);
		}

		return length;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (!TryRunForward(data.ToArray(), out byte[] current))
		{
			return false;
		}

		if (destination.Length < current.Length)
		{
			return false;
		}

		current.CopyTo(destination);
		bytesWritten = current.Length;
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
		if (!TryRunForward(buffer.ToArray(), out byte[] current))
		{
			return false;
		}

		destination.Write(current, 0, current.Length);
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (!TryRunReverse(obfuscatedData.ToArray(), out byte[] current))
		{
			return false;
		}

		if (destination.Length < current.Length)
		{
			return false;
		}

		current.CopyTo(destination);
		bytesWritten = current.Length;
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
		if (!TryRunReverse(buffer.ToArray(), out byte[] current))
		{
			return false;
		}

		destination.Write(current, 0, current.Length);
		return true;
	}

	/// <summary>
	/// Obfuscates <paramref name="data"/> through every stage in order, stopping at the first stage
	/// that fails. Each stage runs through its own <c>Try</c> method, so a failure is reported rather
	/// than thrown and the composite keeps the <c>Try</c> contract of its stages.
	/// </summary>
	private bool TryRunForward(byte[] data, out byte[] result)
	{
		result = data;
		foreach (IObfuscationProvider stage in _stages)
		{
			byte[] buffer = new byte[stage.GetMaxObfuscatedLength(result.Length)];
			if (!stage.TryObfuscate(result, buffer, out int written))
			{
				result = [];
				return false;
			}

			result = buffer.AsSpan(0, written).ToArray();
		}

		return true;
	}

	/// <summary>
	/// Deobfuscates <paramref name="data"/> through every stage in reverse order, stopping at the
	/// first stage that fails, for the same reason as <see cref="TryRunForward"/>.
	/// </summary>
	private bool TryRunReverse(byte[] data, out byte[] result)
	{
		result = data;
		for (int i = _stages.Count - 1; i >= 0; i--)
		{
			IObfuscationProvider stage = _stages[i];
			byte[] buffer = new byte[stage.GetMaxDeobfuscatedLength(result.Length)];
			if (!stage.TryDeobfuscate(result, buffer, out int written))
			{
				result = [];
				return false;
			}

			result = buffer.AsSpan(0, written).ToArray();
		}

		return true;
	}
}