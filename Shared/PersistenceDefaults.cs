// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The interface default bodies a persistence provider has to repeat when it declares the member itself.
/// </summary>
/// <remarks>
/// Linked into the FileSystem and Temp providers rather than placed in the interfaces package, following
/// <c>DistributionArguments</c>. Both declare a public <c>RetrieveOrCreateAsync</c> of their own, which
/// hides the <see cref="IPersistenceProvider{TKey}"/> default body, so the documented behaviour has to be
/// restated; restating it once here keeps the two from drifting apart again.
/// </remarks>
internal static class PersistenceDefaults
{
	/// <summary>
	/// Retrieves the object stored under <paramref name="key"/>, or creates, stores and returns a new one.
	/// </summary>
	/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
	/// <typeparam name="T">The type of object to retrieve or create.</typeparam>
	/// <param name="provider">The provider to read from and store into.</param>
	/// <param name="key">The key of the object.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The stored object, or the new instance now stored under <paramref name="key"/>.</returns>
	internal static async Task<T> RetrieveOrCreateAsync<TKey, T>(IPersistenceProvider<TKey> provider, TKey key, CancellationToken cancellationToken)
		where TKey : notnull
		where T : new()
	{
		T? existing = await provider.RetrieveAsync<T>(key, cancellationToken).ConfigureAwait(false);
		if (existing is not null)
		{
			return existing;
		}

		// Stored before it is returned, as the interface documents: callers bootstrap a record with this,
		// and one that was never written comes back as a fresh default on every call.
		T newInstance = new();
		await provider.StoreAsync(key, newInstance, cancellationToken).ConfigureAwait(false);
		return newInstance;
	}
}
