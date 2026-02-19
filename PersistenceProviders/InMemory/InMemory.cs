// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProviders;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Abstractions;

/// <summary>
/// An in-memory persistence provider that stores objects in a concurrent dictionary.
/// Data does not persist beyond the application lifecycle.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
public class InMemory<TKey> : IPersistenceProvider<TKey> where TKey : notnull
{
	private readonly ConcurrentDictionary<TKey, object?> store = new();

	/// <summary>
	/// Gets the name of the persistence provider.
	/// </summary>
	public string ProviderName => "InMemory";

	/// <summary>
	/// Gets a value indicating whether the persistence provider supports long-term storage beyond the application lifecycle.
	/// </summary>
	public bool IsPersistent => false;

	/// <summary>
	/// Stores an object using the specified key.
	/// </summary>
	/// <typeparam name="T">The type of object to store.</typeparam>
	/// <param name="key">The unique key to identify the stored object.</param>
	/// <param name="obj">The object to store.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous storage operation.</returns>
	public Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		store[key] = obj;
		return Task.CompletedTask;
	}

	/// <summary>
	/// Retrieves an object using the specified key.
	/// </summary>
	/// <typeparam name="T">The type of object to retrieve.</typeparam>
	/// <param name="key">The unique key that identifies the stored object.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous retrieval operation. Returns null if the object is not found.</returns>
	public Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (store.TryGetValue(key, out object? value) && value is T typedValue)
		{
			return Task.FromResult<T?>(typedValue);
		}

		return Task.FromResult<T?>(default);
	}

	/// <summary>
	/// Checks whether an object with the specified key exists in storage.
	/// </summary>
	/// <param name="key">The unique key to check for.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous existence check.</returns>
	public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(store.ContainsKey(key));
	}

	/// <summary>
	/// Removes an object with the specified key from storage.
	/// </summary>
	/// <param name="key">The unique key that identifies the object to remove.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous removal operation.</returns>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(store.TryRemove(key, out _));
	}

	/// <summary>
	/// Retrieves all keys that are currently stored.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous operation to get all keys.</returns>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult<IEnumerable<TKey>>([.. store.Keys]);
	}

	/// <summary>
	/// Clears all stored objects from the persistence provider.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous clear operation.</returns>
	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		store.Clear();
		return Task.CompletedTask;
	}
}
