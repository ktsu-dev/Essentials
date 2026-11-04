// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.InMemory;

using ktsu.Essentials;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An in-memory persistence provider that stores objects in a concurrent dictionary.
/// Data does not persist beyond the application lifecycle.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
public class InMemoryPersistenceProvider<TKey> : IPersistenceProvider<TKey> where TKey : notnull
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
	/// Stores an object using the specified key. Storing <see langword="null"/> removes the key, as the file-backed providers do.
	/// </summary>
	/// <remarks>
	/// The provider keeps a reference to <paramref name="obj"/> rather than a snapshot, so later changes to the object
	/// are visible to <see cref="RetrieveAsync{T}"/> without storing it again. A file-backed provider returns what was written.
	/// </remarks>
	/// <typeparam name="T">The type of object to store.</typeparam>
	/// <param name="key">The unique key to identify the stored object.</param>
	/// <param name="obj">The object to store, or <see langword="null"/> to remove the key.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous storage operation.</returns>
	public Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (obj is null)
		{
			store.TryRemove(key, out _);
		}
		else
		{
			store[key] = obj;
		}

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
	/// Retrieves an object using the specified key, or creates and stores a new instance if not found.
	/// </summary>
	/// <remarks>
	/// Creation is atomic: callers racing on one missing key, or racing a <see cref="StoreAsync{T}"/>, all get
	/// the instance left stored, and a value stored in the meantime is never replaced by a new one.
	/// </remarks>
	/// <typeparam name="T">The type of object to retrieve or create.</typeparam>
	/// <param name="key">The unique key that identifies the stored object.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>The stored object, or the new instance now stored under <paramref name="key"/>.</returns>
	public Task<T> RetrieveOrCreateAsync<T>(TKey key, CancellationToken cancellationToken = default) where T : new()
	{
		cancellationToken.ThrowIfCancellationRequested();

		while (true)
		{
			object? stored = store.GetOrAdd(key, static _ => new T());
			if (stored is T typed)
			{
				return Task.FromResult(typed);
			}

			// Something of another type is stored, which RetrieveAsync reports as missing, so it is replaced as a
			// missing key would be created; but only if it is still what was seen, so a concurrent store wins.
			T created = new();
			if (store.TryUpdate(key, created, stored))
			{
				return Task.FromResult(created);
			}
		}
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
