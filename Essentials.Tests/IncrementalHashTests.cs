// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.All;
using ktsu.Essentials.HashProviders.SHA256;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class IncrementalHashTests
{
	public TestContext TestContext { get; set; } = null!;

	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddHashProviders();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> HashProviders => BuildProvider().EnumerateProviders<IHashProvider>();

	/// <summary>
	/// Chunk boundaries are deliberately uneven and not aligned to any algorithm's block size,
	/// so a provider that mishandles partial blocks or fails to carry state across appends fails here.
	/// </summary>
	private static readonly int[] ChunkSizes = [1, 7, 64, 3, 200, 17];

	private static byte[] BuildPayload()
	{
		// 2500 bytes: large enough that XxHash3 and XxHash128, which accumulate in 1024-byte
		// internal blocks, exercise their multi-block path rather than completing in one block.
		byte[] payload = new byte[2500];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i * 31 % 251);
		}

		return payload;
	}

	/// <summary>
	/// A payload comfortably larger than the 80 KB buffer <see cref="IHashProvider.TryHashAsync"/> rents,
	/// so hashing it forces the async read loop to run for multiple iterations.
	/// </summary>
	private static byte[] BuildLargePayload()
	{
		byte[] payload = new byte[250_000];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i * 31 % 251);
		}

		return payload;
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_In_Chunks_Equals_OneShot(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using IIncrementalHash incremental = provider.CreateIncremental();
		int offset = 0;
		int chunkIndex = 0;
		while (offset < payload.Length)
		{
			int size = Math.Min(ChunkSizes[chunkIndex++ % ChunkSizes.Length], payload.Length - offset);
			incremental.Append(payload.AsSpan(offset, size));
			offset += size;
		}

		byte[] incrementalHash = incremental.GetHashAndReset();
		byte[] oneShotHash = provider.Hash(payload);

		Assert.AreEqual(
			Convert.ToHexString(oneShotHash),
			Convert.ToHexString(incrementalHash),
			$"{providerName} incremental hash should equal its one-shot hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_Reports_Provider_HashLength(IHashProvider provider, string providerName)
	{
		using IIncrementalHash incremental = provider.CreateIncremental();
		Assert.AreEqual(provider.HashLengthBytes, incremental.HashLengthBytes, $"{providerName} length mismatch");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_Empty_Input_Equals_OneShot(IHashProvider provider, string providerName)
	{
		using IIncrementalHash incremental = provider.CreateIncremental();
		byte[] incrementalHash = incremental.GetHashAndReset();

		Assert.AreEqual(
			Convert.ToHexString(provider.Hash([])),
			Convert.ToHexString(incrementalHash),
			$"{providerName} incremental empty hash should equal its one-shot empty hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_Resets_Between_Uses(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using IIncrementalHash incremental = provider.CreateIncremental();
		incremental.Append(payload);
		byte[] first = incremental.GetHashAndReset();

		incremental.Append(payload);
		byte[] second = incremental.GetHashAndReset();

		Assert.AreEqual(
			Convert.ToHexString(first),
			Convert.ToHexString(second),
			$"{providerName} should produce the same hash after a reset");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public void Incremental_TryGetHashAndReset_Rejects_Small_Buffer(IHashProvider provider, string providerName)
	{
		using IIncrementalHash incremental = provider.CreateIncremental();
		incremental.Append("data"u8);

		byte[] tooSmall = new byte[Math.Max(1, provider.HashLengthBytes - 1)];
		bool ok = incremental.TryGetHashAndReset(tooSmall, out int bytesWritten);

		Assert.IsFalse(ok, $"{providerName} should reject an undersized destination");
		Assert.AreEqual(0, bytesWritten, $"{providerName} should report no bytes written on failure");

		// The failed call must leave the running state intact: a retry with a correctly sized
		// buffer should succeed and produce the same hash as if the small-buffer call had never
		// happened.
		byte[] destination = new byte[provider.HashLengthBytes];
		bool retryOk = incremental.TryGetHashAndReset(destination, out int retryWritten);

		Assert.IsTrue(retryOk, $"{providerName} should succeed on retry with a correctly sized buffer");
		Assert.AreEqual(provider.HashLengthBytes, retryWritten, $"{providerName} retry should report the full hash length");
		Assert.AreEqual(
			Convert.ToHexString(provider.Hash("data"u8)),
			Convert.ToHexString(destination),
			$"{providerName} retry should produce the hash of the data appended before the failed call");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task HashAsync_Stream_Large_Payload_Equals_OneShot(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildLargePayload();

		using MemoryStream stream = new(payload);
		byte[] asyncHash = await provider.HashAsync(stream, TestContext.CancellationTokenSource.Token).ConfigureAwait(false);

		Assert.AreEqual(
			Convert.ToHexString(provider.Hash(payload)),
			Convert.ToHexString(asyncHash),
			$"{providerName} async hash of a large payload should equal its one-shot hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task HashAsync_Stream_Short_Reads_Equals_OneShot(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildLargePayload();

		using ShortReadStream stream = new(payload);
		byte[] asyncHash = await provider.HashAsync(stream, TestContext.CancellationTokenSource.Token).ConfigureAwait(false);

		Assert.AreEqual(
			Convert.ToHexString(provider.Hash(payload)),
			Convert.ToHexString(asyncHash),
			$"{providerName} async hash over a stream that only ever returns short reads should equal its one-shot hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task HashAsync_Stream_Equals_OneShot(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using MemoryStream stream = new(payload);
		byte[] asyncHash = await provider.HashAsync(stream, TestContext.CancellationTokenSource.Token).ConfigureAwait(false);

		Assert.AreEqual(
			Convert.ToHexString(provider.Hash(payload)),
			Convert.ToHexString(asyncHash),
			$"{providerName} async stream hash should equal its one-shot hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Reports_Success_And_Fills_Destination(IHashProvider provider, string providerName)
	{
		byte[] payload = BuildPayload();

		using MemoryStream stream = new(payload);
		byte[] destination = new byte[provider.HashLengthBytes];
		bool ok = await provider.TryHashAsync(stream, destination, TestContext.CancellationTokenSource.Token).ConfigureAwait(false);

		Assert.IsTrue(ok, $"{providerName} should hash the stream successfully");
		Assert.AreEqual(
			Convert.ToHexString(provider.Hash(payload)),
			Convert.ToHexString(destination),
			$"{providerName} async destination should hold the correct hash");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Rejects_Small_Destination(IHashProvider provider, string providerName)
	{
		using MemoryStream stream = new(BuildPayload());
		byte[] tooSmall = new byte[Math.Max(1, provider.HashLengthBytes - 1)];

		bool ok = await provider.TryHashAsync(stream, tooSmall, TestContext.CancellationTokenSource.Token).ConfigureAwait(false);

		Assert.IsFalse(ok, $"{providerName} should reject an undersized destination");
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Honours_Cancellation(IHashProvider provider, string providerName)
	{
		using CancellationTokenSource cts = new();
		await cts.CancelAsync().ConfigureAwait(false);

		using MemoryStream stream = new(BuildPayload());
		byte[] destination = new byte[provider.HashLengthBytes];

		// Caught by base type on purpose: the framework may surface either
		// OperationCanceledException or its TaskCanceledException subclass, and which one
		// depends on the stream implementation. An exact-type assertion would be brittle here.
		bool cancelled = false;
		try
		{
			await provider.TryHashAsync(stream, destination, cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			cancelled = true;
		}

		Assert.IsTrue(cancelled, $"{providerName} should surface cancellation");
	}

	#region BufferingIncrementalHash coverage

	// These tests are deliberately not part of the DynamicData sweep above: every registered
	// provider overrides CreateIncremental() with a genuinely incremental implementation, so none
	// of them ever exercises BufferingIncrementalHash, the default body every external implementer
	// inherits. BufferingOnlyHashProvider implements only the two required TryHash methods so its
	// CreateIncremental() resolves to that default.

	[TestMethod]
	public void BufferingIncrementalHash_Chunked_Equals_OneShot()
	{
		IHashProvider provider = new BufferingOnlyHashProvider();
		byte[] payload = BuildPayload();

		using IIncrementalHash incremental = provider.CreateIncremental();
		AssertIsBufferingIncrementalHash(incremental);

		int offset = 0;
		int chunkIndex = 0;
		while (offset < payload.Length)
		{
			int size = Math.Min(ChunkSizes[chunkIndex++ % ChunkSizes.Length], payload.Length - offset);
			incremental.Append(payload.AsSpan(offset, size));
			offset += size;
		}

		byte[] incrementalHash = incremental.GetHashAndReset();
		byte[] oneShotHash = provider.Hash(payload);

		Assert.AreEqual(
			Convert.ToHexString(oneShotHash),
			Convert.ToHexString(incrementalHash),
			"BufferingIncrementalHash chunked hash should equal its one-shot hash");
	}

	[TestMethod]
	public void BufferingIncrementalHash_Empty_Input_Equals_OneShot()
	{
		IHashProvider provider = new BufferingOnlyHashProvider();

		using IIncrementalHash incremental = provider.CreateIncremental();
		AssertIsBufferingIncrementalHash(incremental);

		byte[] incrementalHash = incremental.GetHashAndReset();

		Assert.AreEqual(
			Convert.ToHexString(provider.Hash([])),
			Convert.ToHexString(incrementalHash),
			"BufferingIncrementalHash empty hash should equal its one-shot empty hash");
	}

	[TestMethod]
	public void BufferingIncrementalHash_Resets_Between_Uses()
	{
		IHashProvider provider = new BufferingOnlyHashProvider();
		byte[] payload = BuildPayload();

		using IIncrementalHash incremental = provider.CreateIncremental();
		AssertIsBufferingIncrementalHash(incremental);

		incremental.Append(payload);
		byte[] first = incremental.GetHashAndReset();

		incremental.Append(payload);
		byte[] second = incremental.GetHashAndReset();

		Assert.AreEqual(
			Convert.ToHexString(first),
			Convert.ToHexString(second),
			"BufferingIncrementalHash should produce the same hash after a reset");
	}

	[TestMethod]
	public void BufferingIncrementalHash_TryGetHashAndReset_Rejects_Small_Buffer()
	{
		IHashProvider provider = new BufferingOnlyHashProvider();

		using IIncrementalHash incremental = provider.CreateIncremental();
		AssertIsBufferingIncrementalHash(incremental);

		incremental.Append("data"u8);

		byte[] tooSmall = new byte[Math.Max(1, provider.HashLengthBytes - 1)];
		bool ok = incremental.TryGetHashAndReset(tooSmall, out int bytesWritten);

		Assert.IsFalse(ok, "BufferingIncrementalHash should reject an undersized destination");
		Assert.AreEqual(0, bytesWritten, "BufferingIncrementalHash should report no bytes written on failure");
	}

	/// <summary>
	/// Confirms, by type name rather than a direct reference (<c>BufferingIncrementalHash</c> is
	/// internal to <c>ktsu.Essentials</c> and not visible here), that <paramref name="incremental"/>
	/// really is the default buffering fallback and not something else the test accidentally
	/// resolved to.
	/// </summary>
	/// <param name="incremental">The instance to check.</param>
	private static void AssertIsBufferingIncrementalHash(IIncrementalHash incremental)
		=> Assert.AreEqual(
			"BufferingIncrementalHash",
			incremental.GetType().Name,
			"the test provider should inherit IHashProvider.CreateIncremental()'s default body");

	/// <summary>
	/// Implements only the two required <see cref="IHashProvider"/> members and does not override
	/// <see cref="IHashProvider.CreateIncremental"/>, so it resolves to the default buffering body.
	/// Both members delegate to a real SHA-256 provider so expected values are trivially available.
	/// </summary>
	private sealed class BufferingOnlyHashProvider : IHashProvider
	{
		private readonly SHA256HashProvider inner = new();

		public int HashLengthBytes => inner.HashLengthBytes;

		public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
			=> inner.TryHash(data, destination, out bytesWritten);

		public bool TryHash(Stream data, Span<byte> destination, out int bytesWritten)
			=> inner.TryHash(data, destination, out bytesWritten);
	}

	#endregion

	/// <summary>
	/// A read-only, non-seekable stream over an in-memory buffer that caps every read at
	/// <see cref="MaxReadSize"/> bytes regardless of how large a buffer the caller passes. Used to
	/// force <see cref="IHashProvider.TryHashAsync"/>'s read loop through hundreds of iterations,
	/// which is the realistic behaviour of network streams and is otherwise never exercised because
	/// <see cref="MemoryStream"/> always satisfies a read in full.
	/// </summary>
	/// <param name="data">The bytes to serve.</param>
	private sealed class ShortReadStream(byte[] data) : Stream
	{
		private const int MaxReadSize = 1000;

		private int position;

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => data.Length;

		public override long Position
		{
			get => position;
			set => throw new NotSupportedException();
		}

		public override void Flush()
		{
			// No-op: nothing is buffered.
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int available = Math.Min(Math.Min(count, MaxReadSize), data.Length - position);
			if (available <= 0)
			{
				return 0;
			}

			Array.Copy(data, position, buffer, offset, available);
			position += available;
			return available;
		}

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			// Overridden explicitly: Stream's default ReadAsync(Memory<byte>, ...) implementation
			// does route through the synchronous Read(byte[], int, int) override above, but
			// IHashProvider.TryHashAsync calls this exact overload, so it is spelled out here to
			// leave no doubt that the cap applies to the path under test.
			int available = Math.Min(Math.Min(buffer.Length, MaxReadSize), data.Length - position);
			if (available <= 0)
			{
				return ValueTask.FromResult(0);
			}

			data.AsSpan(position, available).CopyTo(buffer.Span);
			position += available;
			return ValueTask.FromResult(available);
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
