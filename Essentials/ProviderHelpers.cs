// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials;

using System;
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
	/// <param name="data">The input string.</param>
	/// <param name="operation">The byte-level operation to apply.</param>
	/// <returns>The result as a UTF8-decoded string.</returns>
	internal static string Utf8Transform(string data, Func<byte[], byte[]> operation)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Encoding.UTF8.GetString(operation(bytes));
	}
}
