// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Internal utility methods used by default interface implementations
/// to avoid duplicating common patterns across provider interfaces.
/// </summary>
internal static class ProviderHelpers
{
	/// <summary>
	/// Wraps a synchronous function in Task.Run with cancellation support.
	/// Used by all async default interface implementations.
	/// </summary>
	/// <typeparam name="T">The return type of the async operation.</typeparam>
	/// <param name="action">The synchronous action to run.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	internal static Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<T>(cancellationToken)
			: Task.Run(action, cancellationToken);

	/// <summary>
	/// Wraps a synchronous void action in Task.Run with cancellation support.
	/// Used by async default interface implementations that return Task (not Task&lt;T&gt;).
	/// </summary>
	/// <param name="action">The synchronous action to run.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	internal static Task RunAsync(Action action, CancellationToken cancellationToken)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled(cancellationToken)
			: Task.Run(action, cancellationToken);

	/// <summary>
	/// Calls a try-operation with a MemoryStream destination and returns the result as a byte array.
	/// Used by convenience methods that auto-allocate output buffers.
	/// </summary>
	/// <param name="tryOperation">A function that writes to a Stream and returns success/failure.</param>
	/// <param name="failureMessage">The message for the exception if the operation fails.</param>
	/// <returns>The output as a byte array.</returns>
	internal static byte[] ExecuteToByteArray(Func<Stream, bool> tryOperation, string failureMessage)
	{
		using MemoryStream outputStream = new();
		if (!tryOperation(outputStream))
		{
			throw new InvalidOperationException(failureMessage);
		}

		return outputStream.ToArray();
	}

	/// <summary>
	/// A span-to-span transform that reports how many bytes it wrote.
	/// </summary>
	/// <param name="source">The input data.</param>
	/// <param name="destination">The buffer to write to.</param>
	/// <param name="bytesWritten">The number of bytes written.</param>
	/// <returns>True if the operation succeeded, false otherwise.</returns>
	internal delegate bool SpanTransform(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten);

	/// <summary>
	/// Runs a span transform into a pooled buffer and returns an array trimmed to the bytes actually written.
	/// </summary>
	/// <remarks>
	/// Used by the self-allocating convenience methods where the output size is known in advance. The
	/// scratch buffer is rented rather than allocated, so the only lasting allocation is the returned
	/// array. Providers whose output size cannot be predicted — decompression, for one — still use the
	/// stream path in <see cref="ExecuteToByteArray"/>.
	/// <para>
	/// The buffer is scrubbed on its way back to the pool. <see cref="ArrayPool{T}"/>.Shared is
	/// process-wide, so anything left behind is readable by whatever rents next, and this path carries
	/// the output of every encoding, compression and obfuscation provider. The resulting
	/// <c>Array.Clear</c> per call is a deliberate cost, not an oversight.
	/// </para>
	/// </remarks>
	/// <param name="maxLength">The largest output the operation can produce for this input.</param>
	/// <param name="source">The input data.</param>
	/// <param name="transform">The operation to run.</param>
	/// <param name="failureMessage">The message for the exception if the operation fails.</param>
	/// <returns>The output, trimmed to the bytes actually written.</returns>
	internal static byte[] ExecuteToExactArray(int maxLength, ReadOnlySpan<byte> source, SpanTransform transform, string failureMessage)
	{
		byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
		try
		{
			if (!transform(source, buffer.AsSpan(0, maxLength), out int bytesWritten))
			{
				throw new InvalidOperationException(failureMessage);
			}

			byte[] result = new byte[bytesWritten];
			buffer.AsSpan(0, bytesWritten).CopyTo(result);
			return result;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
		}
	}

	/// <summary>
	/// Bridges a ReadOnlySpan input to a Stream-based operation by wrapping it in a MemoryStream.
	/// </summary>
	/// <param name="data">The span data to bridge.</param>
	/// <param name="destination">The destination stream.</param>
	/// <param name="streamOperation">The stream-to-stream operation to execute.</param>
	/// <returns>True if the operation succeeded, false otherwise.</returns>
	internal static bool SpanToStreamBridge(ReadOnlySpan<byte> data, Stream destination, Func<Stream, Stream, bool> streamOperation)
	{
		using MemoryStream inputStream = new(data.ToArray());
		return streamOperation(inputStream, destination);
	}

	/// <summary>
	/// Applies a byte-level operation to a UTF8 string, returning the result as a string.
	/// Encodes the input string to UTF8 bytes, applies the operation, and decodes the result.
	/// </summary>
	/// <remarks>
	/// Only valid when the operation is guaranteed to produce well-formed UTF8 output — for example
	/// format encoders such as Base64 or Hex, whose output is ASCII. Operations that produce arbitrary
	/// binary (compression, encryption, obfuscation) must use <see cref="Utf8ToBase64Transform"/> instead,
	/// because decoding arbitrary bytes as UTF8 silently replaces invalid sequences with U+FFFD.
	/// </remarks>
	/// <param name="data">The input string.</param>
	/// <param name="operation">The byte-level operation to apply. Must produce well-formed UTF8.</param>
	/// <returns>The result as a UTF8-decoded string.</returns>
	internal static string Utf8Transform(string data, Func<byte[], byte[]> operation)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Encoding.UTF8.GetString(operation(bytes));
	}

	/// <summary>
	/// Applies a byte-level operation to a UTF8 string and returns the binary result as Base64 text.
	/// </summary>
	/// <remarks>
	/// Use for operations that produce arbitrary binary output, so the result survives as text.
	/// <see cref="Base64ToUtf8Transform"/> is the exact inverse.
	/// </remarks>
	/// <param name="data">The input string.</param>
	/// <param name="operation">The byte-level operation to apply.</param>
	/// <returns>The result encoded as a Base64 string.</returns>
	internal static string Utf8ToBase64Transform(string data, Func<byte[], byte[]> operation)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Convert.ToBase64String(operation(bytes));
	}

	/// <summary>
	/// Decodes Base64 text, applies a byte-level operation, and returns the result as a UTF8 string.
	/// </summary>
	/// <remarks>The exact inverse of <see cref="Utf8ToBase64Transform"/>.</remarks>
	/// <param name="data">The Base64-encoded input string.</param>
	/// <param name="operation">The byte-level operation to apply.</param>
	/// <returns>The result as a UTF8-decoded string.</returns>
	/// <exception cref="FormatException"><paramref name="data"/> is not valid Base64.</exception>
	internal static string Base64ToUtf8Transform(string data, Func<byte[], byte[]> operation)
	{
		byte[] bytes = Convert.FromBase64String(data);
		return Encoding.UTF8.GetString(operation(bytes));
	}
}
