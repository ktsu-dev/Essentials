// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.EncodingProviders.Base64;

using ktsu.Essentials;
using System;
using System.Buffers;
using System.IO;
using SysBase64 = System.Buffers.Text.Base64;

/// <summary>
/// An encoding provider that uses Base64 encoding for data encoding and decoding.
/// </summary>
/// <remarks>
/// The span paths use the UTF8 Base64 primitives directly, so they neither allocate an intermediate
/// string nor copy through a temporary array.
/// </remarks>
public class Base64EncodingProvider : IEncodingProvider
{
	/// <inheritdoc/>
	public int GetMaxEncodedLength(int sourceLength) => SysBase64.GetMaxEncodedToUtf8Length(sourceLength);

	/// <inheritdoc/>
	public int GetMaxDecodedLength(int encodedLength) => SysBase64.GetMaxDecodedFromUtf8Length(encodedLength);

	/// <inheritdoc/>
	public bool TryEncode(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		OperationStatus status = SysBase64.EncodeToUtf8(data, destination, out int consumed, out bytesWritten);

		if (status == OperationStatus.Done && consumed == data.Length)
		{
			return true;
		}

		bytesWritten = 0;
		return false;
	}

	/// <inheritdoc/>
	public bool TryEncode(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		try
		{
			using MemoryStream inputBuffer = new();
			data.CopyTo(inputBuffer);

			byte[] source = inputBuffer.ToArray();
			byte[] encoded = new byte[GetMaxEncodedLength(source.Length)];

			if (!TryEncode(source, encoded, out int bytesWritten))
			{
				return false;
			}

			destination.Write(encoded, 0, bytesWritten);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public bool TryDecode(ReadOnlySpan<byte> encodedData, Span<byte> destination, out int bytesWritten)
	{
		OperationStatus status = SysBase64.DecodeFromUtf8(encodedData, destination, out int consumed, out bytesWritten);

		if (status == OperationStatus.Done && consumed == encodedData.Length)
		{
			return true;
		}

		bytesWritten = 0;
		return false;
	}

	/// <inheritdoc/>
	public bool TryDecode(Stream encodedData, Stream destination)
	{
		if (encodedData is null || destination is null)
		{
			return false;
		}

		try
		{
			using MemoryStream inputBuffer = new();
			encodedData.CopyTo(inputBuffer);

			byte[] source = inputBuffer.ToArray();
			byte[] decoded = new byte[GetMaxDecodedLength(source.Length)];

			if (!TryDecode(source, decoded, out int bytesWritten))
			{
				return false;
			}

			destination.Write(decoded, 0, bytesWritten);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
}
