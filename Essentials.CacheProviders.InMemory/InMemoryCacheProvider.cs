// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CacheProviders.InMemory;

using ktsu.Essentials;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>
/// An in-memory cache provider that stores key-value pairs with optional expiration support.
/// </summary>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cached value.</typeparam>
public class InMemoryCacheProvider<TKey, TValue> : ICacheProvider<TKey, TValue> where TKey : notnull
{
	private readonly ConcurrentDictionary<TKey, CacheEntry> cache = new();

	/// <summary>
	/// Tries to get a cached value by key.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">When this method returns, contains the cached value if found, or the default value if not found.</param>
	/// <returns>True if the value was found in the cache and has not expired, false otherwise.</returns>
	public bool TryGet(TKey key, out TValue? value)
	{
		if (cache.TryGetValue(key, out CacheEntry? entry))
		{
			if (entry.Expiration is null || entry.Expiration > DateTime.UtcNow)
			{
				value = entry.Value;
				return true;
			}

			// Entry has expired. Remove only the entry that was observed, so a fresh value another thread
			// has just set under the same key is left in place.
			RemoveEntry(key, entry);
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Sets a value in the cache with an optional expiration time.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">The value to cache.</param>
	/// <param name="expiration">The optional time-to-live for the cached entry. If null, the entry does not expire.</param>
	public void Set(TKey key, TValue value, TimeSpan? expiration = null) =>
		cache[key] = new CacheEntry(value, ComputeExpiration(expiration));

	/// <summary>
	/// Removes a cached value by key.
	/// </summary>
	/// <param name="key">The cache key to remove.</param>
	/// <returns>True if the value was found and removed, false if it didn't exist.</returns>
	public bool Remove(TKey key) => cache.TryRemove(key, out _);

	/// <summary>
	/// Clears all entries from the cache.
	/// </summary>
	public void Clear() => cache.Clear();

	/// <summary>
	/// Converts a time-to-live into an absolute expiration time, saturating rather than overflowing.
	/// A time-to-live that reaches past <see cref="DateTime.MaxValue"/> never expires, and one that
	/// reaches before <see cref="DateTime.MinValue"/> has already expired.
	/// </summary>
	private static DateTime? ComputeExpiration(TimeSpan? expiration)
	{
		if (expiration is null)
		{
			return null;
		}

		DateTime now = DateTime.UtcNow;
		if (expiration.Value > DateTime.MaxValue - now)
		{
			return null;
		}

		return expiration.Value < DateTime.MinValue - now ? DateTime.MinValue : now + expiration.Value;
	}

	private void RemoveEntry(TKey key, CacheEntry entry)
	{
		KeyValuePair<TKey, CacheEntry> observed = new(key, entry);
#if NET5_0_OR_GREATER
		cache.TryRemove(observed);
#else
		((ICollection<KeyValuePair<TKey, CacheEntry>>)cache).Remove(observed);
#endif
	}

	private sealed class CacheEntry(TValue value, DateTime? expiration)
	{
		public TValue Value { get; } = value;
		public DateTime? Expiration { get; } = expiration;
	}
}
