// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.All;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CacheProviderTests
{
	public TestContext TestContext { get; set; } = null!;

	private static ICacheProvider<string, int> CreateCache()
	{
		ServiceCollection services = new();
		services.AddCacheProviders();
		using ServiceProvider provider = services.BuildServiceProvider();
		return provider.GetRequiredService<ICacheProvider<string, int>>();
	}

	private static ICacheProvider<string, string?> CreateNullableCache()
	{
		ServiceCollection services = new();
		services.AddCacheProviders();
		using ServiceProvider provider = services.BuildServiceProvider();
		return provider.GetRequiredService<ICacheProvider<string, string?>>();
	}

	[TestMethod]
	public void Cache_Set_And_TryGet()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key1", 42);
		bool found = cache.TryGet("key1", out int value);

		Assert.IsTrue(found, "Should find cached value");
		Assert.AreEqual(42, value, "Should return correct value");
	}

	[TestMethod]
	public void Cache_TryGet_Missing_Key_Returns_False()
	{
		ICacheProvider<string, int> cache = CreateCache();

		bool found = cache.TryGet("nonexistent", out int value);

		Assert.IsFalse(found, "Should not find missing key");
		Assert.AreEqual(0, value, "Should return default for missing key");
	}

	[TestMethod]
	public void Cache_Remove_Existing_Key()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key1", 42);
		bool removed = cache.Remove("key1");

		Assert.IsTrue(removed, "Should remove existing key");

		bool found = cache.TryGet("key1", out _);
		Assert.IsFalse(found, "Should not find removed key");
	}

	[TestMethod]
	public void Cache_Remove_Missing_Key_Returns_False()
	{
		ICacheProvider<string, int> cache = CreateCache();

		bool removed = cache.Remove("nonexistent");
		Assert.IsFalse(removed, "Should return false for missing key");
	}

	[TestMethod]
	public void Cache_Clear_Removes_All_Entries()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("a", 1);
		cache.Set("b", 2);
		cache.Clear();

		Assert.IsFalse(cache.TryGet("a", out _), "Should not find cleared key a");
		Assert.IsFalse(cache.TryGet("b", out _), "Should not find cleared key b");
	}

	[TestMethod]
	public void Cache_Get_Throws_For_Missing_Key()
	{
		ICacheProvider<string, int> cache = CreateCache();

		Assert.ThrowsExactly<KeyNotFoundException>(() => cache.Get("nonexistent"));
	}

	[TestMethod]
	public void Cache_GetOrAdd_Returns_Existing_Value()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key", 10);
		int result = cache.GetOrAdd("key", _ => 99);

		Assert.AreEqual(10, result, "Should return existing cached value");
	}

	[TestMethod]
	public void Cache_GetOrAdd_Creates_New_Value()
	{
		ICacheProvider<string, int> cache = CreateCache();

		int result = cache.GetOrAdd("key", _ => 99);

		Assert.AreEqual(99, result, "Should create value from factory");

		bool found = cache.TryGet("key", out int cached);
		Assert.IsTrue(found, "Should cache the new value");
		Assert.AreEqual(99, cached);
	}

	[TestMethod]
	public void Cache_Get_Returns_Cached_Null()
	{
		ICacheProvider<string, string?> cache = CreateNullableCache();

		cache.Set("key", null);

		Assert.IsNull(cache.Get("key"), "Should return the cached null rather than throwing");
	}

	[TestMethod]
	public void Cache_GetOrAdd_Returns_Cached_Null_Without_Invoking_Factory()
	{
		ICacheProvider<string, string?> cache = CreateNullableCache();
		int factoryCalls = 0;

		cache.Set("key", null);
		string? result = cache.GetOrAdd("key", _ =>
		{
			factoryCalls++;
			return "replacement";
		});

		Assert.IsNull(result, "Should return the cached null");
		Assert.AreEqual(0, factoryCalls, "Should not re-invoke the factory for a cached null");
	}

	[TestMethod]
	public async Task Cache_GetAsync_Returns_Cached_Null()
	{
		ICacheProvider<string, string?> cache = CreateNullableCache();

		await cache.SetAsync("key", null).ConfigureAwait(false);

		Assert.IsNull(await cache.GetAsync("key").ConfigureAwait(false), "Should return the cached null rather than throwing");
	}

	[TestMethod]
	public async Task Cache_GetOrAddAsync_Returns_Cached_Null_Without_Invoking_Factory()
	{
		ICacheProvider<string, string?> cache = CreateNullableCache();
		int factoryCalls = 0;

		await cache.SetAsync("key", null).ConfigureAwait(false);
		string? result = await cache.GetOrAddAsync("key", _ =>
		{
			Interlocked.Increment(ref factoryCalls);
			return "replacement";
		}).ConfigureAwait(false);

		Assert.IsNull(result, "Should return the cached null");
		Assert.AreEqual(0, factoryCalls, "Should not re-invoke the factory for a cached null");
	}

	[TestMethod]
	public void Cache_Expiration_Removes_Entry()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key", 42, TimeSpan.FromMilliseconds(50));
		Thread.Sleep(100);

		bool found = cache.TryGet("key", out _);
		Assert.IsFalse(found, "Should not find expired entry");
	}

	[TestMethod]
	public void Cache_No_Expiration_Persists()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key", 42);
		Thread.Sleep(50);

		bool found = cache.TryGet("key", out int value);
		Assert.IsTrue(found, "Should find entry without expiration");
		Assert.AreEqual(42, value);
	}
}
