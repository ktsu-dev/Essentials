// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for encoding providers that apply a reversible format or transport encoding, such as
/// Base64 or hexadecimal. These are not text character encodings.
/// </summary>
public interface IEncodingProvider
{
	/// <summary>
	/// Gets the largest number of bytes <see cref="TryEncode(ReadOnlySpan{byte}, Span{byte}, out int)"/>
	/// can produce for an input of the given length.
	/// </summary>
	/// <param name="sourceLength">The length of the data to encode.</param>
	/// <returns>The buffer size required to guarantee the encode succeeds.</returns>
	public int GetMaxEncodedLength(int sourceLength);

	/// <summary>
	/// Gets the largest number of bytes <see cref="TryDecode(ReadOnlySpan{byte}, Span{byte}, out int)"/>
	/// can produce for encoded data of the given length.
	/// </summary>
	/// <param name="encodedLength">The length of the encoded data.</param>
	/// <returns>The buffer size required to guarantee the decode succeeds.</returns>
	public int GetMaxDecodedLength(int encodedLength);

	/// <summary>
	/// Tries to encode the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination to write the encoded data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public bool TryEncode(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);

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
		=> ProviderHelpers.SpanToStreamBridge(data, destination, TryEncode);

	/// <summary>
	/// Encodes the data from the span and returns the result.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <returns>The encoded data.</returns>
	public byte[] Encode(ReadOnlySpan<byte> data)
		=> ProviderHelpers.ExecuteToExactArray(
			GetMaxEncodedLength(data.Length),
			data,
			TryEncode,
			"Encoding failed to produce output with the allocated buffer.");

	/// <summary>
	/// Encodes the data from the stream and returns the result.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <returns>The encoded data.</returns>
	public byte[] Encode(Stream data)
		=> ProviderHelpers.ExecuteToByteArray(
			output => TryEncode(data, output),
			"Encoding failed to produce output with the allocated buffer.");

	/// <summary>
	/// Encodes the data from the string and returns the result.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <returns>The encoded data.</returns>
	public string Encode(string data)
		=> ProviderHelpers.Utf8Transform(data, bytes => Encode(bytes));

	/// <summary>
	/// Tries to encode the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination stream to write the encoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public Task<bool> TryEncodeAsync(ReadOnlyMemory<byte> data, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryEncode(data.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to encode the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination stream to write the encoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public Task<bool> TryEncodeAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryEncode(data, destination), cancellationToken);

	/// <summary>
	/// Encodes the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encoded data.</returns>
	public Task<byte[]> EncodeAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Encode(data.Span), cancellationToken);

	/// <summary>
	/// Encodes the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encoded data.</returns>
	public Task<byte[]> EncodeAsync(Stream data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Encode(data), cancellationToken);

	/// <summary>
	/// Encodes the data from the string and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The encoded data.</returns>
	public Task<string> EncodeAsync(string data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Encode(data), cancellationToken);

	/// <summary>
	/// Tries to decode the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination to write the decoded data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public bool TryDecode(ReadOnlySpan<byte> encodedData, Span<byte> destination, out int bytesWritten);

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
		=> ProviderHelpers.SpanToStreamBridge(encodedData, destination, TryDecode);

	/// <summary>
	/// Decodes the data from the span and returns the result.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <returns>The decoded data.</returns>
	public byte[] Decode(ReadOnlySpan<byte> encodedData)
		=> ProviderHelpers.ExecuteToExactArray(
			GetMaxDecodedLength(encodedData.Length),
			encodedData,
			TryDecode,
			"Decoding failed to produce output with the allocated buffer.");

	/// <summary>
	/// Decodes the data from the stream and returns the result.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <returns>The decoded data.</returns>
	public byte[] Decode(Stream encodedData)
		=> ProviderHelpers.ExecuteToByteArray(
			output => TryDecode(encodedData, output),
			"Decoding failed to produce output with the allocated buffer.");

	/// <summary>
	/// Decodes text produced by <see cref="Encode(string)"/> and returns the original string.
	/// </summary>
	/// <param name="encodedData">The encoded text.</param>
	/// <returns>The decoded data as a UTF8 string.</returns>
	public string Decode(string encodedData)
		=> ProviderHelpers.Utf8Transform(encodedData, bytes => Decode(bytes));

	/// <summary>
	/// Tries to decode the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination stream to write the decoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public Task<bool> TryDecodeAsync(ReadOnlyMemory<byte> encodedData, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDecode(encodedData.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to decode the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination stream to write the decoded data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public Task<bool> TryDecodeAsync(Stream encodedData, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDecode(encodedData, destination), cancellationToken);

	/// <summary>
	/// Decodes the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decoded data.</returns>
	public Task<byte[]> DecodeAsync(ReadOnlyMemory<byte> encodedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decode(encodedData.Span), cancellationToken);

	/// <summary>
	/// Decodes the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decoded data.</returns>
	public Task<byte[]> DecodeAsync(Stream encodedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decode(encodedData), cancellationToken);

	/// <summary>
	/// Decodes text produced by <see cref="Encode(string)"/> asynchronously.
	/// </summary>
	/// <param name="encodedData">The encoded text.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decoded data as a UTF8 string.</returns>
	public Task<string> DecodeAsync(string encodedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decode(encodedData), cancellationToken);
}
