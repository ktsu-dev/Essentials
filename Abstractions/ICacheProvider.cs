// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for cache providers that can store, retrieve, and manage cached key-value pairs with optional expiration.
/// </summary>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cached value.</typeparam>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Get/Set are the natural names for cache operations")]
public interface ICacheProvider<TKey, TValue> where TKey : notnull
{
	/// <summary>
	/// Tries to get a cached value by key.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">When this method returns, contains the cached value if found, or the default value if not found.</param>
	/// <returns>True if the value was found in the cache, false otherwise.</returns>
	public bool TryGet(TKey key, out TValue? value);

	/// <summary>
	/// Sets a value in the cache with an optional expiration time.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">The value to cache.</param>
	/// <param name="expiration">The optional time-to-live for the cached entry. If null, the entry does not expire.</param>
	public void Set(TKey key, TValue value, TimeSpan? expiration = null);

	/// <summary>
	/// Removes a cached value by key.
	/// </summary>
	/// <param name="key">The cache key to remove.</param>
	/// <returns>True if the value was found and removed, false if it didn't exist.</returns>
	public bool Remove(TKey key);

	/// <summary>
	/// Clears all entries from the cache.
	/// </summary>
	public void Clear();

	/// <summary>
	/// Gets a cached value by key, throwing if not found.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <returns>The cached value.</returns>
	/// <exception cref="KeyNotFoundException">Thrown when the key is not found in the cache.</exception>
	public TValue Get(TKey key)
	{
		if (!TryGet(key, out TValue? value) || value is null)
		{
			throw new KeyNotFoundException($"The key '{key}' was not found in the cache.");
		}

		return value;
	}

	/// <summary>
	/// Gets a cached value by key, or adds it using the provided factory if not found.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="factory">A factory function to create the value if not found in cache.</param>
	/// <param name="expiration">The optional time-to-live for the cached entry if it is created.</param>
	/// <returns>The cached or newly created value.</returns>
	public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory, TimeSpan? expiration = null)
	{
		if (TryGet(key, out TValue? value) && value is not null)
		{
			return value;
		}

		Ensure.NotNull(factory);
		TValue newValue = factory(key);
		Set(key, newValue, expiration);
		return newValue;
	}

	/// <summary>
	/// Tries to get a cached value by key asynchronously.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A tuple containing a boolean indicating success and the cached value.</returns>
	public Task<(bool Found, TValue? Value)> TryGetAsync(TKey key, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() =>
		{
			bool found = TryGet(key, out TValue? value);
			return (found, value);
		}, cancellationToken);

	/// <summary>
	/// Gets a cached value by key asynchronously, throwing if not found.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The cached value.</returns>
	/// <exception cref="KeyNotFoundException">Thrown when the key is not found in the cache.</exception>
	public Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Get(key), cancellationToken);

	/// <summary>
	/// Sets a value in the cache asynchronously with an optional expiration time.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">The value to cache.</param>
	/// <param name="expiration">The optional time-to-live for the cached entry.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous set operation.</returns>
	public Task SetAsync(TKey key, TValue value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Set(key, value, expiration), cancellationToken);

	/// <summary>
	/// Removes a cached value by key asynchronously.
	/// </summary>
	/// <param name="key">The cache key to remove.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the value was found and removed, false if it didn't exist.</returns>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Remove(key), cancellationToken);

	/// <summary>
	/// Clears all entries from the cache asynchronously.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous clear operation.</returns>
	public Task ClearAsync(CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(Clear, cancellationToken);

	/// <summary>
	/// Gets a cached value by key asynchronously, or adds it using the provided factory if not found.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="factory">A factory function to create the value if not found in cache.</param>
	/// <param name="expiration">The optional time-to-live for the cached entry if it is created.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The cached or newly created value.</returns>
	public Task<TValue> GetOrAddAsync(TKey key, Func<TKey, TValue> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => GetOrAdd(key, factory, expiration), cancellationToken);
}
