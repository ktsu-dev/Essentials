// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.EncodingProviders.Base64;

using ktsu.Essentials;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

	/// <inheritdoc/>
	/// <remarks>
	/// Genuinely asynchronous: the source is read and the result written with <c>ReadAsync</c> and
	/// <c>WriteAsync</c>, so no thread is held for the duration of the I/O. Declaring this member and
	/// its decoding counterpart replaces the interface's <c>Task.Run</c> defaults and converts every
	/// stream path derived from them.
	/// <para>
	/// The whole input is buffered before the transform, which is what the synchronous path does too:
	/// Base64 encodes three bytes to four, so a chunked transform would have to carry a partial group
	/// across every boundary. The transform itself is CPU work on a buffer already in memory and is
	/// deliberately not offloaded — that is the caller's decision to make, not this provider's.
	/// </para>
	/// </remarks>
	public async Task<bool> TryEncodeAsync(Stream data, Stream destination, CancellationToken cancellationToken = default)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		try
		{
			byte[] source = await ReadAllAsync(data, cancellationToken).ConfigureAwait(false);
			byte[] encoded = new byte[GetMaxEncodedLength(source.Length)];

			if (!TryEncode(source, encoded, out int bytesWritten))
			{
				return false;
			}

			await destination.WriteAsync(encoded.AsMemory(0, bytesWritten), cancellationToken).ConfigureAwait(false);
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
	/// <remarks>
	/// Genuinely asynchronous, on the same terms as <see cref="TryEncodeAsync(Stream, Stream, CancellationToken)"/>.
	/// </remarks>
	public async Task<bool> TryDecodeAsync(Stream encodedData, Stream destination, CancellationToken cancellationToken = default)
	{
		if (encodedData is null || destination is null)
		{
			return false;
		}

		try
		{
			byte[] source = await ReadAllAsync(encodedData, cancellationToken).ConfigureAwait(false);
			byte[] decoded = new byte[GetMaxDecodedLength(source.Length)];

			if (!TryDecode(source, decoded, out int bytesWritten))
			{
				return false;
			}

			await destination.WriteAsync(decoded.AsMemory(0, bytesWritten), cancellationToken).ConfigureAwait(false);
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

	/// <summary>
	/// Reads a stream to its end without holding a thread.
	/// </summary>
	/// <param name="source">The stream to read.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>Everything the stream had left.</returns>
	private static async Task<byte[]> ReadAllAsync(Stream source, CancellationToken cancellationToken)
	{
		using MemoryStream buffer = new();
		await source.CopyToAsync(buffer, CopyBufferSize, cancellationToken).ConfigureAwait(false);
		return buffer.ToArray();
	}

	/// <summary>
	/// The chunk size used when reading a source stream, matching the framework's own default.
	/// </summary>
	private const int CopyBufferSize = 81920;
}
