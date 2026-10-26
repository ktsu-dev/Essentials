// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.FileSystem;

using ktsu.Essentials;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A file system-based persistence provider that stores objects as files using serialization.
/// Objects persist beyond application lifecycle.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="FileSystemPersistenceProvider{TKey}"/> class.
/// </remarks>
/// <param name="fileSystemProvider">The file system provider to use for file operations.</param>
/// <param name="serializationProvider">The serialization provider to use for object serialization.</param>
/// <param name="baseDirectory">The base directory where objects will be stored.</param>
public sealed class FileSystemPersistenceProvider<TKey>(
	IFileSystemProvider fileSystemProvider,
	ISerializationProvider serializationProvider,
	string baseDirectory) : IPersistenceProvider<TKey>
	where TKey : notnull
{
	private readonly FileRecordStore<TKey> _records = new(
		Ensure.NotNull(fileSystemProvider),
		Ensure.NotNull(serializationProvider),
		Ensure.NotNull(baseDirectory),
		"file system");

	/// <summary>
	/// Gets the directory this provider stores objects in.
	/// </summary>
	public string BaseDirectory { get; } = baseDirectory;

	/// <inheritdoc/>
	public string ProviderName => "FileSystem";

	/// <inheritdoc/>
	public bool IsPersistent => true;

	/// <inheritdoc/>
	public Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		return _records.StoreAsync(key, obj, cancellationToken);
	}

	/// <inheritdoc/>
	/// <exception cref="PersistenceProviderException">The record is there but cannot be read as <typeparamref name="T"/>, or reading it failed.</exception>
	public Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		return _records.RetrieveAsync<T>(key, cancellationToken);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The new instance is stored only if no record has appeared under the key in the meantime; if one has, it is
	/// returned instead. A record that is there but cannot be read is left untouched and throws.
	/// </remarks>
	/// <exception cref="PersistenceProviderException">The record is there but cannot be read as <typeparamref name="T"/>, or reading or storing it failed.</exception>
	public Task<T> RetrieveOrCreateAsync<T>(TKey key, CancellationToken cancellationToken = default) where T : new()
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		return _records.RetrieveOrCreateAsync<T>(key, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(_records.Exists(key));
	}

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		return _records.RemoveAsync(key, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(_records.GetAllKeys());
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Also removes staging files that writes interrupted by a crash or power loss left behind, each of which
	/// holds a whole record. Staging files younger than a minute belong to writes still in progress and are kept.
	/// </remarks>
	public Task ClearAsync(CancellationToken cancellationToken = default) => _records.ClearAsync(cancellationToken);

}
