// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for compression providers that can compress and decompress data.
/// </summary>
public interface ICompressionProvider
{
	/// <summary>
	/// Gets the largest number of bytes <see cref="TryCompress(ReadOnlySpan{byte}, Span{byte}, out int)"/>
	/// can produce for an input of the given length.
	/// </summary>
	/// <param name="sourceLength">The length of the data to compress.</param>
	/// <returns>The buffer size required to guarantee the compress succeeds.</returns>
	public int GetMaxCompressedLength(int sourceLength);

	/// <summary>
	/// Tries to compress the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination to write the compressed data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public bool TryCompress(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Tries to compress the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination to write the compressed data to.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public bool TryCompress(Stream data, Stream destination);

	/// <summary>
	/// Tries to compress the data from the span and write the result to the destination stream.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination stream to write the compressed data to.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public bool TryCompress(ReadOnlySpan<byte> data, Stream destination)
		=> ProviderHelpers.SpanToStreamBridge(data, destination, TryCompress);

	/// <summary>
	/// Compresses the data from the span and returns the result.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <returns>The compressed data.</returns>
	public byte[] Compress(ReadOnlySpan<byte> data)
		=> ProviderHelpers.ExecuteToExactArray(
			GetMaxCompressedLength(data.Length),
			data,
			TryCompress,
			"Compression failed to produce output with the allocated buffer.");

	/// <summary>
	/// Compresses the data from the stream and returns the result.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <returns>The compressed data.</returns>
	public byte[] Compress(Stream data)
		=> ProviderHelpers.ExecuteToByteArray(
			output => TryCompress(data, output),
			"Compression failed to produce output with the allocated buffer.");

	/// <summary>
	/// Compresses the data from the string and returns the result.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <returns>The compressed data.</returns>
	public string Compress(string data)
		=> ProviderHelpers.Utf8ToBase64Transform(data, bytes => Compress(bytes));

	/// <summary>
	/// Tries to compress the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination stream to write the compressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public Task<bool> TryCompressAsync(ReadOnlyMemory<byte> data, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryCompress(data.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to compress the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="destination">The destination stream to write the compressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the compression was successful, false otherwise.</returns>
	public Task<bool> TryCompressAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryCompress(data, destination), cancellationToken);

	/// <summary>
	/// Compresses the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The compressed data.</returns>
	public Task<byte[]> CompressAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Compress(data.Span), cancellationToken);

	/// <summary>
	/// Compresses the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The compressed data.</returns>
	public Task<byte[]> CompressAsync(Stream data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Compress(data), cancellationToken);

	/// <summary>
	/// Compresses the data from the string and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The compressed data.</returns>
	public Task<string> CompressAsync(string data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Compress(data), cancellationToken);

	/// <summary>
	/// Tries to decompress the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public bool TryDecompress(ReadOnlySpan<byte> compressedData, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Tries to decompress the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination to write the decompressed data to.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public bool TryDecompress(Stream compressedData, Stream destination);

	/// <summary>
	/// Tries to decompress the data from the span and write the result to the destination stream.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination stream to write the decompressed data to.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public bool TryDecompress(ReadOnlySpan<byte> compressedData, Stream destination)
		=> ProviderHelpers.SpanToStreamBridge(compressedData, destination, TryDecompress);

	/// <summary>
	/// Decompresses the data from the span and returns the result.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <returns>The decompressed data.</returns>
	/// <remarks>
	/// Unlike the other categories this cannot size a buffer up front — the decompressed length is not
	/// derivable from compressed input — so it grows a stream instead. Callers who already know the
	/// original size should use <see cref="TryDecompress(ReadOnlySpan{byte}, Span{byte}, out int)"/>.
	/// </remarks>
	public byte[] Decompress(ReadOnlySpan<byte> compressedData)
	{
		// A span cannot be captured by a lambda, so this calls the span-to-stream overload directly
		// rather than going through ProviderHelpers.ExecuteToByteArray.
		using MemoryStream outputStream = new();
		if (!TryDecompress(compressedData, outputStream))
		{
			throw new InvalidOperationException("Decompression failed to produce output with the allocated buffer.");
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// Decompresses the data from the stream and returns the result.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <returns>The decompressed data.</returns>
	public byte[] Decompress(Stream compressedData)
		=> ProviderHelpers.ExecuteToByteArray(
			output => TryDecompress(compressedData, output),
			"Decompression failed to produce output with the allocated buffer.");

	/// <summary>
	/// Decompresses text produced by <see cref="Compress(string)"/> and returns the original string.
	/// </summary>
	/// <param name="compressedData">The compressed text.</param>
	/// <returns>The decompressed data as a UTF8 string.</returns>
	public string Decompress(string compressedData)
		=> ProviderHelpers.Base64ToUtf8Transform(compressedData, bytes => Decompress(bytes));

	/// <summary>
	/// Tries to decompress the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination stream to write the decompressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public Task<bool> TryDecompressAsync(ReadOnlyMemory<byte> compressedData, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDecompress(compressedData.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to decompress the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="destination">The destination stream to write the decompressed data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the decompression was successful, false otherwise.</returns>
	public Task<bool> TryDecompressAsync(Stream compressedData, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDecompress(compressedData, destination), cancellationToken);

	/// <summary>
	/// Decompresses the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decompressed data.</returns>
	public Task<byte[]> DecompressAsync(ReadOnlyMemory<byte> compressedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decompress(compressedData.Span), cancellationToken);

	/// <summary>
	/// Decompresses the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="compressedData">The compressed data to decompress.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decompressed data.</returns>
	public Task<byte[]> DecompressAsync(Stream compressedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decompress(compressedData), cancellationToken);

	/// <summary>
	/// Decompresses text produced by <see cref="Compress(string)"/> asynchronously.
	/// </summary>
	/// <param name="compressedData">The compressed text.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The decompressed data as a UTF8 string.</returns>
	public Task<string> DecompressAsync(string compressedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Decompress(compressedData), cancellationToken);
}
