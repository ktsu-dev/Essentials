// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a contract for persistence providers that can store and retrieve objects using different storage backends.
/// This interface supports dependency injection and allows swapping between different persistence strategies
/// such as file system, memory, cloud storage, or database backends.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects (e.g., string, Guid, etc.).</typeparam>
public interface IPersistenceProvider<TKey> where TKey : notnull
{
	/// <summary>
	/// Gets the name of the persistence provider (e.g., "FileSystem", "Memory", "Cloud").
	/// </summary>
	public string ProviderName { get; }

	/// <summary>
	/// Gets a value indicating whether the persistence provider supports long-term storage beyond the application lifecycle.
	/// </summary>
	public bool IsPersistent { get; }

	/// <summary>
	/// Stores an object using the specified key.
	/// </summary>
	/// <typeparam name="T">The type of object to store.</typeparam>
	/// <param name="key">The unique key to identify the stored object.</param>
	/// <param name="obj">The object to store.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous storage operation.</returns>
	public Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves an object using the specified key.
	/// </summary>
	/// <typeparam name="T">The type of object to retrieve.</typeparam>
	/// <param name="key">The unique key that identifies the stored object.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous retrieval operation. Returns null if the object is not found.</returns>
	public Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves an object using the specified key, or creates and stores a new instance if not found.
	/// </summary>
	/// <typeparam name="T">The type of object to retrieve, must have a parameterless constructor.</typeparam>
	/// <param name="key">The unique key that identifies the stored object.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous retrieval operation. Returns a new instance if the object is not found.</returns>
	public async Task<T> RetrieveOrCreateAsync<T>(TKey key, CancellationToken cancellationToken = default) where T : new()
	{
		T? result = await RetrieveAsync<T>(key, cancellationToken).ConfigureAwait(false);
		if (result is not null)
		{
			return result;
		}

		T newInstance = new();
		await StoreAsync(key, newInstance, cancellationToken).ConfigureAwait(false);
		return newInstance;
	}

	/// <summary>
	/// Checks whether an object with the specified key exists in storage.
	/// </summary>
	/// <param name="key">The unique key to check for.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous existence check. Returns true if the object exists, false otherwise.</returns>
	public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);

	/// <summary>
	/// Removes an object with the specified key from storage.
	/// </summary>
	/// <param name="key">The unique key that identifies the object to remove.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous removal operation. Returns true if the object was removed, false if it didn't exist.</returns>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default);

	/// <summary>
	/// Retrieves all keys that are currently stored.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous operation to get all keys.</returns>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Clears all stored objects from the persistence provider.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task that represents the asynchronous clear operation.</returns>
	public Task ClearAsync(CancellationToken cancellationToken = default);
}
