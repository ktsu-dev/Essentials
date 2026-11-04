// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers;
using System.IO;

/// <summary>
/// Reads a stream a chunk at a time through a pooled buffer, for the providers whose stream paths transform or
/// hash each byte in place.
/// </summary>
/// <remarks>
/// Linked into the FNV hash and the Xor, Caesar and BitRotate obfuscation providers rather than placed in the
/// interfaces package. The base <see cref="Stream.ReadByte"/> and <see cref="Stream.WriteByte"/> allocate and
/// make a call per byte on any stream that does not override them, which cost eleven times as long and 32 bytes
/// of garbage per byte (issue #91).
/// </remarks>
internal static class StreamChunks
{
	/// <summary>How many bytes are read at a time.</summary>
	internal const int BufferSize = 81920;

	/// <summary>Receives one chunk, which it may change in place.</summary>
	/// <param name="chunk">The bytes read.</param>
	internal delegate void ChunkAction(Span<byte> chunk);

	/// <summary>
	/// Passes every chunk of <paramref name="source"/> to <paramref name="action"/>, then writes it to
	/// <paramref name="destination"/> when there is one.
	/// </summary>
	/// <param name="source">The stream to read to its end.</param>
	/// <param name="action">Receives each chunk in order.</param>
	/// <param name="destination">Where each chunk goes once <paramref name="action"/> has seen it, if anywhere.</param>
	internal static void ForEach(Stream source, ChunkAction action, Stream? destination = null)
	{
		byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
		try
		{
			int read;
			while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
			{
				action(buffer.AsSpan(0, read));
				destination?.Write(buffer, 0, read);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
		}
	}
}
