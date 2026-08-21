// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests.Infrastructure;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A stream that supports only its asynchronous members. Every synchronous read or write throws.
/// </summary>
/// <remarks>
/// Both the array-based and memory-based asynchronous overloads are overridden. The base class routes
/// one to the other and, for an unoverridden stream, ultimately to the synchronous member — so
/// overriding only one would let a synchronous implementation slip through the very check this exists
/// to make. <see cref="Flush"/> is deliberately a no-op rather than a throw: it moves no data, and
/// disposal calls it.
/// </remarks>
internal sealed class AsyncOnlyStream : Stream
{
	private readonly MemoryStream inner;

	/// <summary>Creates a readable stream over the given contents.</summary>
	public AsyncOnlyStream(byte[] contents) => inner = new MemoryStream(contents, writable: false);

	/// <summary>Creates an empty writable stream.</summary>
	public AsyncOnlyStream() => inner = new MemoryStream();

	/// <summary>The bytes written to this stream.</summary>
	public byte[] ToArray() => inner.ToArray();

	/// <inheritdoc/>
	public override bool CanRead => inner.CanRead;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => inner.CanWrite;

	/// <inheritdoc/>
	public override long Length => inner.Length;

	/// <inheritdoc/>
	public override long Position
	{
		get => inner.Position;
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		// Moves no data, and disposal calls it. Throwing here would fail every test for the wrong reason.
	}

	/// <inheritdoc/>
	public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) =>
		throw new NotSupportedException("This stream is asynchronous only; Read was called.");

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) =>
		throw new NotSupportedException("This stream is asynchronous only; Write was called.");

	/// <inheritdoc/>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		inner.ReadAsync(buffer, offset, count, cancellationToken);

	/// <inheritdoc/>
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
		inner.ReadAsync(buffer, cancellationToken);

	/// <inheritdoc/>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		inner.WriteAsync(buffer, offset, count, cancellationToken);

	/// <inheritdoc/>
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
		inner.WriteAsync(buffer, cancellationToken);

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			inner.Dispose();
		}

		base.Dispose(disposing);
	}
}
