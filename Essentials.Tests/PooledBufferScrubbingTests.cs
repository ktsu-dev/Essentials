// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using ktsu.Essentials.EncodingProviders.Hex;
using ktsu.Essentials.HashProviders.SHA256;
using ktsu.Essentials.KeyedHashProviders.HmacSha256;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Buffers rented from <see cref="ArrayPool{T}"/>.Shared are process-wide: whatever a provider leaves
/// in one stays there until some later renter overwrites it, and the next renter is arbitrary other
/// code in the same process. These tests pin that the four pooled call sites hand their buffers back
/// scrubbed rather than carrying hashed or transformed data out with them.
/// </summary>
/// <remarks>
/// Each test seeds the pool with a buffer of its own, filled with a sentinel, so the provider is
/// guaranteed to rent that exact array — Shared hands a returned buffer straight back to the next
/// same-size rent on the same thread. Keeping the reference across the provider call is not something
/// a real caller may do, but it is the only way to observe what the pool was handed back. The sentinel
/// also rules out a vacuous pass: a buffer the provider never touched still reads as the sentinel, not
/// as zeros, so these tests cannot pass by failing to observe the right array.
/// </remarks>
[TestClass]
public class PooledBufferScrubbingTests
{
	/// <summary>
	/// A value the providers never write, so its survival identifies untouched bytes.
	/// </summary>
	private const byte Sentinel = 0xCC;

	/// <summary>
	/// The buffer size each of the four pooled read loops rents: <see cref="IHashProvider.TryHashAsync"/>,
	/// <see cref="IKeyedHashProvider.TryHashAsync"/>, and the sync and stream HMAC paths in
	/// <c>HmacKeyedHashCore</c>.
	/// </summary>
	private const int HashReadBufferLength = 81920;

	private static byte[] BuildPayload(int length)
	{
		byte[] payload = new byte[length];
		for (int i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i * 31 % 251);
		}

		return payload;
	}

	private static byte[] SeedPoolWithSentinelBuffer(int length)
	{
		byte[] scratch = ArrayPool<byte>.Shared.Rent(length);
		scratch.AsSpan().Fill(Sentinel);
		ArrayPool<byte>.Shared.Return(scratch);
		return scratch;
	}

	private static void AssertScrubbed(byte[] scratch)
	{
		int residue = scratch.AsSpan().IndexOfAnyExcept((byte)0);
		Assert.AreEqual(
			-1,
			residue,
			residue < 0
				? null
				: $"The pooled buffer was returned holding 0x{scratch[residue]:X2} at offset {residue}, so its contents survive into the next renter.");
	}

	[TestMethod]
	[DoNotParallelize]
	public async Task TryHashAsyncReturnsItsReadBufferScrubbedAsync()
	{
		IHashProvider provider = new SHA256HashProvider();
		byte[] scratch = SeedPoolWithSentinelBuffer(HashReadBufferLength);
		using MemoryStream data = new(BuildPayload(4096));

		_ = await provider.HashAsync(data).ConfigureAwait(false);

		AssertScrubbed(scratch);
	}

	[TestMethod]
	[DoNotParallelize]
	public void ExecuteToExactArrayReturnsItsScratchBufferScrubbed()
	{
		IEncodingProvider provider = new HexEncodingProvider();
		byte[] payload = BuildPayload(500);
		byte[] scratch = SeedPoolWithSentinelBuffer(provider.GetMaxEncodedLength(payload.Length));

		_ = provider.Encode(payload);

		AssertScrubbed(scratch);
	}

	[TestMethod]
	[DoNotParallelize]
	public async Task KeyedHashTryHashAsyncReturnsItsReadBufferScrubbedAsync()
	{
		IKeyedHashProvider provider = new HmacSha256KeyedHashProvider();
		byte[] scratch = SeedPoolWithSentinelBuffer(HashReadBufferLength);
		byte[] key = BuildPayload(32);
		using MemoryStream data = new(BuildPayload(4096));
		byte[] destination = new byte[provider.HashLengthBytes];

		_ = await provider.TryHashAsync(key, data, destination).ConfigureAwait(false);

		AssertScrubbed(scratch);
	}

	[TestMethod]
	[DoNotParallelize]
	public void KeyedHashStreamTryHashReturnsItsReadBufferScrubbed()
	{
		HmacSha256KeyedHashProvider provider = new();
		byte[] scratch = SeedPoolWithSentinelBuffer(HashReadBufferLength);
		byte[] key = BuildPayload(32);
		using MemoryStream data = new(BuildPayload(4096));
		byte[] destination = new byte[provider.HashLengthBytes];

		_ = provider.TryHash(key, data, destination, out _);

		AssertScrubbed(scratch);
	}
}
