// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.Temp;

using ktsu.Essentials;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A temporary directory-based persistence provider that stores objects in the system temporary directory.
/// Objects are typically cleaned up by the system but may persist between application sessions.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="TempPersistenceProvider{TKey}"/> class.
/// </remarks>
/// <param name="fileSystemProvider">The file system provider to use for file operations.</param>
/// <param name="serializationProvider">The serialization provider to use for object serialization.</param>
/// <param name="applicationName">Optional application name to create a subdirectory in temp folder.</param>
public sealed class TempPersistenceProvider<TKey>(
	IFileSystemProvider fileSystemProvider,
	ISerializationProvider serializationProvider,
	string? applicationName = null) : IPersistenceProvider<TKey>, IDisposable
	where TKey : notnull
{
	private readonly IFileSystemProvider _fileSystemProvider = Ensure.NotNull(fileSystemProvider);
	private readonly FileRecordStore<TKey> _records = new(
		Ensure.NotNull(fileSystemProvider),
		Ensure.NotNull(serializationProvider),
		CreateTempDirectory(fileSystemProvider, applicationName),
		"temp directory");

	/// <summary>
	/// Gets the directory this provider stores objects in: a fresh subdirectory of the system temporary directory.
	/// </summary>
	public string BaseDirectory => _records.Directory;

	/// <inheritdoc/>
	public string ProviderName => "Temp";

	/// <inheritdoc/>
	public bool IsPersistent => false; // Temp files can be cleaned up by the system

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

	/// <summary>
	/// Releases resources. Does not automatically clean up the temporary directory.
	/// Use <see cref="CleanupDirectory"/> to explicitly remove the temporary directory if needed.
	/// </summary>
	public void Dispose() => GC.SuppressFinalize(this);

	/// <summary>
	/// Explicitly cleans up the temporary directory and all its contents.
	/// This is separate from Dispose() to give consumers control over directory cleanup.
	/// </summary>
	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally ignoring cleanup errors")]
	public void CleanupDirectory()
	{
		try
		{
			if (_fileSystemProvider.Directory.Exists(_records.Directory))
			{
				_fileSystemProvider.Directory.Delete(_records.Directory, recursive: true);
			}
		}
		catch (Exception)
		{
			// Ignore cleanup errors - temp directories may be cleaned up by the system
			// Intentionally catching all exceptions during cleanup
		}
	}

	private static string CreateTempDirectory(IFileSystemProvider fileSystemProvider, string? applicationName)
	{
		string tempPath = fileSystemProvider.Path.GetTempPath();
		string directoryName = applicationName ?? "PersistenceProvider";
		string tempDirectory = fileSystemProvider.Path.Combine(tempPath, directoryName, Guid.NewGuid().ToString("N")[..8]);

		fileSystemProvider.Directory.CreateDirectory(tempDirectory);
		return tempDirectory;
	}
}
