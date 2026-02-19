// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CacheProviders;

using System;
using System.Collections.Concurrent;
using ktsu.Abstractions;

/// <summary>
/// An in-memory cache provider that stores key-value pairs with optional expiration support.
/// </summary>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cached value.</typeparam>
public class InMemory<TKey, TValue> : ICacheProvider<TKey, TValue> where TKey : notnull
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

			// Entry has expired, remove it
			cache.TryRemove(key, out _);
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
	public void Set(TKey key, TValue value, TimeSpan? expiration = null)
	{
		DateTime? expirationTime = expiration.HasValue ? DateTime.UtcNow + expiration.Value : null;
		cache[key] = new CacheEntry(value, expirationTime);
	}

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

	private sealed class CacheEntry(TValue value, DateTime? expiration)
	{
		public TValue Value { get; } = value;
		public DateTime? Expiration { get; } = expiration;
	}
}
