// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Common.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using ktsu.Abstractions;
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
