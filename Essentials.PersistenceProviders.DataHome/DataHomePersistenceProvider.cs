// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.DataHome;

using ktsu.Essentials;
using ktsu.Essentials.PersistenceProviders.FileSystem;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A persistence provider that stores objects in the user's data directory.
/// </summary>
/// <remarks>
/// Resolves to <c>$XDG_DATA_HOME/&lt;applicationName&gt;</c> when that variable is set to an absolute
/// path, and to <c>~/.local/share/&lt;applicationName&gt;</c> otherwise. The layout is the same on every
/// platform; on Windows <c>~</c> is <c>%USERPROFILE%</c>. Use this for application state — caches,
/// databases, saved documents. For user-editable settings use the <c>ConfigHome</c> provider instead.
/// Storage itself is delegated to <see cref="FileSystemPersistenceProvider{TKey}"/>.
/// </remarks>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
/// <param name="fileSystemProvider">The file system provider to use for file operations.</param>
/// <param name="serializationProvider">The serialization provider to use for object serialization.</param>
/// <param name="applicationName">The application name used to namespace the data directory.</param>
/// <param name="subdirectory">An optional subdirectory beneath the application directory.</param>
public sealed class DataHomePersistenceProvider<TKey>(
	IFileSystemProvider fileSystemProvider,
	ISerializationProvider serializationProvider,
	string applicationName,
	string? subdirectory = null) : IPersistenceProvider<TKey>
	where TKey : notnull
{
	private readonly FileSystemPersistenceProvider<TKey> _inner = new(
		fileSystemProvider,
		serializationProvider,
		UserDirectories.GetApplicationDataDirectory(applicationName, subdirectory));

	/// <summary>
	/// Gets the directory this provider stores objects in.
	/// </summary>
	public string BaseDirectory => _inner.BaseDirectory;

	/// <inheritdoc/>
	public string ProviderName => "DataHome";

	/// <inheritdoc/>
	public bool IsPersistent => true;

	/// <inheritdoc/>
	public Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
		=> _inner.StoreAsync(key, obj, cancellationToken);

	/// <inheritdoc/>
	public Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
		=> _inner.RetrieveAsync<T>(key, cancellationToken);

	/// <inheritdoc/>
	public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
		=> _inner.ExistsAsync(key, cancellationToken);

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
		=> _inner.RemoveAsync(key, cancellationToken);

	/// <inheritdoc/>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
		=> _inner.GetAllKeysAsync(cancellationToken);

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken = default)
		=> _inner.ClearAsync(cancellationToken);
}
