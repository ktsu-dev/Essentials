// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for encoding providers that can encode and decode data using format encodings such as Base64, Hex, or URL encoding.
/// This is NOT for text character encodings (System.Text.Encoding) — it is for format/transport encodings.
/// </summary>
public interface IEncodingProvider
{
	/// <summary>
	/// Tries to encode the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination to write the encoded data to.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public bool TryEncode(ReadOnlySpan<byte> data, Span<byte> destination);

	/// <summary>
	/// Tries to encode the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination to write the encoded data to.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public bool TryEncode(Stream data, Stream destination);

	/// <summary>
	/// Tries to encode the data from the span and write the result to the destination stream.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination stream to write the encoded data to.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public bool TryEncode(ReadOnlySpan<byte> data, Stream destination)
	{
		using MemoryStream inputStream = new(data.ToArray());
		return TryEncode(inputStream, destination);
	}

	/// <summary>
	/// Encodes the data from the span and returns the result.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <returns>The encoded data.</returns>
	public byte[] Encode(ReadOnlySpan<byte> data)
	{
		using MemoryStream outputStream = new();
		if (!TryEncode(data, outputStream))
		{
			throw new InvalidOperationException("Encoding failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Encodes the data from the stream and returns the result.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <returns>The encoded data.</returns>
	public byte[] Encode(Stream data)
	{
		using MemoryStream outputStream = new();
		if (!TryEncode(data, outputStream))
		{
			throw new InvalidOperationException("Encoding failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Encodes the data from the string and returns the result.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <returns>The encoded data.</returns>
	public string Encode(string data)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Encoding.UTF8.GetString(Encode(bytes));
	}

	/// <summary>
	/// Tries to encode the data from the span and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination to write the encoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public Task<bool> TryEncodeAsync(ReadOnlyMemory<byte> data, Memory<byte> destination, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => TryEncode(data.Span, destination.Span), cancellationToken);

	/// <summary>
	/// Tries to encode the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination stream to write the encoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public Task<bool> TryEncodeAsync(ReadOnlyMemory<byte> data, Stream destination, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => TryEncode(data.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to encode the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination stream to write the encoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public Task<bool> TryEncodeAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => TryEncode(data, destination), cancellationToken);

	/// <summary>
	/// Encodes the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encoded data.</returns>
	public Task<byte[]> EncodeAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<byte[]>(cancellationToken)
			: Task.Run(() => Encode(data.Span), cancellationToken);

	/// <summary>
	/// Encodes the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encoded data.</returns>
	public Task<byte[]> EncodeAsync(Stream data, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<byte[]>(cancellationToken)
			: Task.Run(() => Encode(data), cancellationToken);

	/// <summary>
	/// Encodes the data from the string and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encoded data.</returns>
	public Task<string> EncodeAsync(string data, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<string>(cancellationToken)
			: Task.Run(() => Encode(data), cancellationToken);

	/// <summary>
	/// Tries to decode the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination to write the decoded data to.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public bool TryDecode(ReadOnlySpan<byte> encodedData, Span<byte> destination);

	/// <summary>
	/// Tries to decode the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination to write the decoded data to.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public bool TryDecode(Stream encodedData, Stream destination);

	/// <summary>
	/// Tries to decode the data from the span and write the result to the destination stream.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination stream to write the decoded data to.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public bool TryDecode(ReadOnlySpan<byte> encodedData, Stream destination)
	{
		using MemoryStream inputStream = new(encodedData.ToArray());
		return TryDecode(inputStream, destination);
	}

	/// <summary>
	/// Decodes the data from the span and returns the result.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <returns>The decoded data.</returns>
	public byte[] Decode(ReadOnlySpan<byte> encodedData)
	{
		using MemoryStream outputStream = new();
		if (!TryDecode(encodedData, outputStream))
		{
			throw new InvalidOperationException("Decoding failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Decodes the data from the stream and returns the result.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <returns>The decoded data.</returns>
	public byte[] Decode(Stream encodedData)
	{
		using MemoryStream outputStream = new();
		if (!TryDecode(encodedData, outputStream))
		{
			throw new InvalidOperationException("Decoding failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Tries to decode the data from the span and write the result to the destination asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination to write the decoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public Task<bool> TryDecodeAsync(ReadOnlyMemory<byte> encodedData, Memory<byte> destination, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => TryDecode(encodedData.Span, destination.Span), cancellationToken);

	/// <summary>
	/// Tries to decode the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination stream to write the decoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public Task<bool> TryDecodeAsync(ReadOnlyMemory<byte> encodedData, Stream destination, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => TryDecode(encodedData.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to decode the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination stream to write the decoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public Task<bool> TryDecodeAsync(Stream encodedData, Stream destination, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => TryDecode(encodedData, destination), cancellationToken);

	/// <summary>
	/// Decodes the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decoded data.</returns>
	public Task<byte[]> DecodeAsync(ReadOnlyMemory<byte> encodedData, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<byte[]>(cancellationToken)
			: Task.Run(() => Decode(encodedData.Span), cancellationToken);

	/// <summary>
	/// Decodes the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decoded data.</returns>
	public Task<byte[]> DecodeAsync(Stream encodedData, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<byte[]>(cancellationToken)
			: Task.Run(() => Decode(encodedData), cancellationToken);
}
