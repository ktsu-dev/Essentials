// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using ktsu.Essentials;
using ktsu.Essentials.All;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class IncrementalHashTests
{
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
		byte[] payload = new byte[292];
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
	}
}
