// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for obfuscation providers that can obfuscate and deobfuscate data using reversible transforms.
/// Obfuscation is NOT encryption — it provides no confidentiality guarantees and is intended only to make
/// data non-obvious at rest or in transit.
/// </summary>
public interface IObfuscationProvider
{
	/// <summary>
	/// Gets the largest number of bytes <see cref="TryObfuscate(ReadOnlySpan{byte}, Span{byte}, out int)"/>
	/// can produce for an input of the given length.
	/// </summary>
	/// <param name="sourceLength">The length of the data to obfuscate.</param>
	/// <returns>The buffer size required to guarantee the obfuscate succeeds.</returns>
	public int GetMaxObfuscatedLength(int sourceLength);

	/// <summary>
	/// Gets the largest number of bytes <see cref="TryDeobfuscate(ReadOnlySpan{byte}, Span{byte}, out int)"/>
	/// can produce for obfuscated data of the given length.
	/// </summary>
	/// <param name="obfuscatedLength">The length of the obfuscated data.</param>
	/// <returns>The buffer size required to guarantee the deobfuscate succeeds.</returns>
	public int GetMaxDeobfuscatedLength(int obfuscatedLength);

	/// <summary>
	/// Tries to obfuscate the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="destination">The destination to write the obfuscated data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the obfuscation was successful, false otherwise.</returns>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Tries to obfuscate the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="destination">The destination to write the obfuscated data to.</param>
	/// <returns>True if the obfuscation was successful, false otherwise.</returns>
	public bool TryObfuscate(Stream data, Stream destination);

	/// <summary>
	/// Tries to obfuscate the data from the span and write the result to the destination stream.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="destination">The destination stream to write the obfuscated data to.</param>
	/// <returns>True if the obfuscation was successful, false otherwise.</returns>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Stream destination)
		=> ProviderHelpers.SpanToStreamBridge(data, destination, TryObfuscate);

	/// <summary>
	/// Obfuscates the data from the span and returns the result.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <returns>The obfuscated data.</returns>
	public byte[] Obfuscate(ReadOnlySpan<byte> data)
		=> ProviderHelpers.ExecuteToExactArray(
			GetMaxObfuscatedLength(data.Length),
			data,
			TryObfuscate,
			"Obfuscation failed to produce output with the allocated buffer.");

	/// <summary>
	/// Obfuscates the data from the stream and returns the result.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <returns>The obfuscated data.</returns>
	public byte[] Obfuscate(Stream data)
		=> ProviderHelpers.ExecuteToByteArray(
			output => TryObfuscate(data, output),
			"Obfuscation failed to produce output with the allocated buffer.");

	/// <summary>
	/// Obfuscates a string and returns the obfuscated bytes as Base64 text.
	/// </summary>
	/// <remarks>
	/// The input is encoded as UTF8, obfuscated, and the obfuscated bytes are Base64-encoded so the
	/// result is safe to store or transmit as text. Use <see cref="Deobfuscate(string)"/> to reverse it.
	/// </remarks>
	/// <param name="data">The data to obfuscate.</param>
	/// <returns>The obfuscated data as a Base64 string.</returns>
	public string Obfuscate(string data)
		=> ProviderHelpers.Utf8ToBase64Transform(data, bytes => Obfuscate(bytes));

	/// <summary>
	/// Tries to obfuscate the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="destination">The destination stream to write the obfuscated data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the obfuscation was successful, false otherwise.</returns>
	public Task<bool> TryObfuscateAsync(ReadOnlyMemory<byte> data, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryObfuscate(data.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to obfuscate the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="destination">The destination stream to write the obfuscated data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the obfuscation was successful, false otherwise.</returns>
	public Task<bool> TryObfuscateAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryObfuscate(data, destination), cancellationToken);

	/// <summary>
	/// Obfuscates the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The obfuscated data.</returns>
	public Task<byte[]> ObfuscateAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Obfuscate(data.Span), cancellationToken);

	/// <summary>
	/// Obfuscates the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The obfuscated data.</returns>
	public Task<byte[]> ObfuscateAsync(Stream data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Obfuscate(data), cancellationToken);

	/// <summary>
	/// Obfuscates the data from the string and returns the result asynchronously.
	/// </summary>
	/// <param name="data">The data to obfuscate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The obfuscated data.</returns>
	public Task<string> ObfuscateAsync(string data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Obfuscate(data), cancellationToken);

	/// <summary>
	/// Tries to deobfuscate the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <param name="destination">The destination to write the deobfuscated data to.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
	/// <returns>True if the deobfuscation was successful, false otherwise.</returns>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Tries to deobfuscate the data from the stream and write the result to the destination.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <param name="destination">The destination to write the deobfuscated data to.</param>
	/// <returns>True if the deobfuscation was successful, false otherwise.</returns>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination);

	/// <summary>
	/// Tries to deobfuscate the data from the span and write the result to the destination stream.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <param name="destination">The destination stream to write the deobfuscated data to.</param>
	/// <returns>True if the deobfuscation was successful, false otherwise.</returns>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Stream destination)
		=> ProviderHelpers.SpanToStreamBridge(obfuscatedData, destination, TryDeobfuscate);

	/// <summary>
	/// Deobfuscates the data from the span and returns the result.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <returns>The deobfuscated data.</returns>
	public byte[] Deobfuscate(ReadOnlySpan<byte> obfuscatedData)
		=> ProviderHelpers.ExecuteToExactArray(
			GetMaxDeobfuscatedLength(obfuscatedData.Length),
			obfuscatedData,
			TryDeobfuscate,
			"Deobfuscation failed to produce output with the allocated buffer.");

	/// <summary>
	/// Deobfuscates the data from the stream and returns the result.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <returns>The deobfuscated data.</returns>
	public byte[] Deobfuscate(Stream obfuscatedData)
		=> ProviderHelpers.ExecuteToByteArray(
			output => TryDeobfuscate(obfuscatedData, output),
			"Deobfuscation failed to produce output with the allocated buffer.");

	/// <summary>
	/// Deobfuscates Base64 text produced by <see cref="Obfuscate(string)"/> and returns the original string.
	/// </summary>
	/// <param name="obfuscatedData">The Base64-encoded obfuscated data.</param>
	/// <returns>The deobfuscated data as a UTF8 string.</returns>
	/// <exception cref="FormatException"><paramref name="obfuscatedData"/> is not valid Base64.</exception>
	public string Deobfuscate(string obfuscatedData)
		=> ProviderHelpers.Base64ToUtf8Transform(obfuscatedData, bytes => Deobfuscate(bytes));

	/// <summary>
	/// Tries to deobfuscate the data from the span and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <param name="destination">The destination stream to write the deobfuscated data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the deobfuscation was successful, false otherwise.</returns>
	public Task<bool> TryDeobfuscateAsync(ReadOnlyMemory<byte> obfuscatedData, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDeobfuscate(obfuscatedData.Span, destination), cancellationToken);

	/// <summary>
	/// Tries to deobfuscate the data from the stream and write the result to the destination stream asynchronously.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <param name="destination">The destination stream to write the deobfuscated data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the deobfuscation was successful, false otherwise.</returns>
	public Task<bool> TryDeobfuscateAsync(Stream obfuscatedData, Stream destination, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TryDeobfuscate(obfuscatedData, destination), cancellationToken);

	/// <summary>
	/// Deobfuscates the data from the span and returns the result asynchronously.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The deobfuscated data.</returns>
	public Task<byte[]> DeobfuscateAsync(ReadOnlyMemory<byte> obfuscatedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Deobfuscate(obfuscatedData.Span), cancellationToken);

	/// <summary>
	/// Deobfuscates the data from the stream and returns the result asynchronously.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated data to deobfuscate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The deobfuscated data.</returns>
	public Task<byte[]> DeobfuscateAsync(Stream obfuscatedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Deobfuscate(obfuscatedData), cancellationToken);

	/// <summary>
	/// Deobfuscates text produced by <see cref="Obfuscate(string)"/> asynchronously.
	/// </summary>
	/// <param name="obfuscatedData">The obfuscated text.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The deobfuscated data as a UTF8 string.</returns>
	public Task<string> DeobfuscateAsync(string obfuscatedData, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Deobfuscate(obfuscatedData), cancellationToken);
}
