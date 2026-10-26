// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.All;
using ktsu.Essentials.CacheProviders.InMemory;
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
	public void Cache_Remove_Expired_Key_Returns_False_And_Removes_It()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key", 42, TimeSpan.MinValue);
		bool removed = cache.Remove("key");

		Assert.IsFalse(removed, "An expired entry is not found by TryGet, so Remove should not report finding it either");
		Assert.IsFalse(cache.Remove("key"), "The expired entry should have been removed");
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

		await cache.SetAsync("key", null, cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

		Assert.IsNull(await cache.GetAsync("key", TestContext.CancellationToken).ConfigureAwait(false), "Should return the cached null rather than throwing");
	}

	[TestMethod]
	public async Task Cache_GetOrAddAsync_Returns_Cached_Null_Without_Invoking_Factory()
	{
		ICacheProvider<string, string?> cache = CreateNullableCache();
		int factoryCalls = 0;

		await cache.SetAsync("key", null, cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);
		string? result = await cache.GetOrAddAsync("key", _ =>
		{
			Interlocked.Increment(ref factoryCalls);
			return "replacement";
		}, cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);

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

	[TestMethod]
	public void Cache_Expired_TryGet_Does_Not_Remove_A_Value_Set_Concurrently()
	{
		InMemoryCacheProvider<InterleavingKey, int> cache = new();
		InterleavingKey key = new();

		cache.Set(key, -1, TimeSpan.FromTicks(-1));

		// TryGet hashes the key once to read the expired entry and again to remove it. Setting a fresh
		// value between the two reproduces another thread's Set landing inside that window.
		key.OnSecondHash(() => cache.Set(key, 2));
		bool foundExpired = cache.TryGet(key, out _);

		Assert.IsFalse(foundExpired, "The entry TryGet read had expired");
		Assert.IsTrue(key.Interleaved, "The concurrent Set should have run between the read and the remove");
		Assert.IsTrue(cache.TryGet(key, out int value), "The value set concurrently should survive the expired entry's removal");
		Assert.AreEqual(2, value);
	}

	[TestMethod]
	public void Cache_Set_With_MaxValue_Expiration_Never_Expires()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key", 42, TimeSpan.MaxValue);

		Assert.IsTrue(cache.TryGet("key", out int value), "An entry with the largest time-to-live should be found");
		Assert.AreEqual(42, value);
	}

	[TestMethod]
	public void Cache_Set_With_MinValue_Expiration_Has_Already_Expired()
	{
		ICacheProvider<string, int> cache = CreateCache();

		cache.Set("key", 42, TimeSpan.MinValue);

		Assert.IsFalse(cache.TryGet("key", out _), "An entry with the most negative time-to-live has already expired");
	}

	[TestMethod]
	public void Cache_Expiry_Follows_Elapsed_Time_Not_The_Wall_Clock()
	{
		// The time-to-live was turned into a DateTime and checked against DateTime.UtcNow, so a clock step
		// stretched or cut every entry's lifetime (issue #162). Only the clock the cache is given moves here.
		long now = 0;
		InMemoryCacheProvider<string, int> cache = new(() => now);

		cache.Set("token", 1, TimeSpan.FromSeconds(30));
		now += Stopwatch.Frequency * 29;
		Assert.IsTrue(cache.TryGet("token", out _), "an entry is live until its time-to-live has elapsed");

		now += Stopwatch.Frequency * 2;
		Assert.IsFalse(cache.TryGet("token", out _), "an entry expires once its time-to-live has elapsed");
	}

	[TestMethod]
	public void Cache_Sweeps_Expired_Entries_That_Are_Never_Read_Again()
	{
		// An expired entry was removed only when its own key was read, so short-lived keys that were never
		// read again stayed for the life of the process (issue #79).
		long now = 0;
		InMemoryCacheProvider<int, int> cache = new(() => now);

		for (int i = 0; i < 10_000; i++)
		{
			cache.Set(i, i, TimeSpan.FromMilliseconds(1));
		}

		cache.Set(-1, -1);
		now += Stopwatch.Frequency * 2;
		cache.Set(-2, -2);

		Assert.AreEqual(2, cache.Count, "only the two entries that do not expire should be left");
		Assert.IsTrue(cache.TryGet(-1, out _));
	}

	[TestMethod]
	public void Cache_Sweep_Keeps_Live_Entries()
	{
		long now = 0;
		InMemoryCacheProvider<int, int> cache = new(() => now);

		for (int i = 0; i < 5_000; i++)
		{
			cache.Set(i, i, TimeSpan.FromHours(1));
		}

		now += Stopwatch.Frequency * 2;
		cache.Set(-1, -1);

		Assert.AreEqual(5_001, cache.Count);
	}

	[TestMethod]
	public void Cache_GetOrAdd_Keeps_A_Value_Set_While_The_Factory_Ran()
	{
		// GetOrAdd checked, ran the factory and then set unconditionally, so a value another writer set in
		// between was overwritten (issue #85).
		ICacheProvider<string, string> cache = new InMemoryCacheProvider<string, string>();

		string result = cache.GetOrAdd("k", key =>
		{
			cache.Set(key, "fresh");
			return "factory";
		});

		Assert.AreEqual("fresh", result);
		Assert.IsTrue(cache.TryGet("k", out string? stored));
		Assert.AreEqual("fresh", stored);
	}

	[TestMethod]
	public async Task Cache_GetOrAdd_Gives_Racing_Callers_The_Cached_Instance()
	{
		ICacheProvider<string, object> cache = new InMemoryCacheProvider<string, object>();
		using Barrier barrier = new(2);

		Task<object>[] callers = [.. Enumerable.Range(0, 2).Select(_ => Task.Run(() => cache.GetOrAdd("k", _ =>
		{
			barrier.SignalAndWait(TestContext.CancellationToken);
			return new object();
		}), TestContext.CancellationToken))];
		object[] results = await Task.WhenAll(callers).ConfigureAwait(false);

		(bool found, object? cached) = await cache.TryGetAsync("k", TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsTrue(found);
		Assert.AreSame(cached, results[0], "the first caller holds an instance that is not cached");
		Assert.AreSame(cached, results[1], "the second caller holds an instance that is not cached");
	}

	[TestMethod]
	public void Cache_GetOrAdd_Replaces_An_Expired_Entry()
	{
		long now = 0;
		InMemoryCacheProvider<string, int> cache = new(() => now);

		cache.Set("k", 1, TimeSpan.FromSeconds(1));
		now += Stopwatch.Frequency * 2;

		Assert.AreEqual(2, cache.GetOrAdd("k", _ => 2));
		Assert.IsTrue(cache.TryGet("k", out int value));
		Assert.AreEqual(2, value);
	}

	/// <summary>
	/// A key that runs an action when it is hashed for the second time after being armed, which lets a
	/// test interleave work between the lookups a single cache call makes.
	/// </summary>
	private sealed class InterleavingKey
	{
		private Action? pending;
		private int hashesUntilAction;

		public bool Interleaved { get; private set; }

		public void OnSecondHash(Action action)
		{
			pending = action;
			hashesUntilAction = 2;
		}

		public override int GetHashCode()
		{
			if (pending is not null && --hashesUntilAction == 0)
			{
				Action action = pending;
				pending = null;
				action();
				Interleaved = true;
			}

			return 17;
		}

		public override bool Equals(object? obj) => ReferenceEquals(this, obj);
	}
}
