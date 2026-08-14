// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.FileSystem;

using ktsu.Essentials;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
	private readonly IFileSystemProvider _fileSystemProvider = Ensure.NotNull(fileSystemProvider);
	private readonly ISerializationProvider _serializationProvider = Ensure.NotNull(serializationProvider);

	/// <summary>
	/// Gets the directory this provider stores objects in.
	/// </summary>
	public string BaseDirectory { get; } = Ensure.NotNull(baseDirectory);

	/// <inheritdoc/>
	public string ProviderName => "FileSystem";

	/// <inheritdoc/>
	public bool IsPersistent => true;

	/// <inheritdoc/>
	public async Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003

		if (obj is null)
		{
			await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
			return;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);
			string serializedData = await _serializationProvider.SerializeAsync(obj, cancellationToken).ConfigureAwait(false);

			// Ensure directory exists
			string? directory = _fileSystemProvider.Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory))
			{
				_fileSystemProvider.Directory.CreateDirectory(directory);
			}

			// Write to temporary file first, then move for atomic operation
			string tempFilePath = filePath + ".tmp";
			await _fileSystemProvider.File.WriteAllTextAsync(tempFilePath, serializedData, cancellationToken).ConfigureAwait(false);

			// Atomic move
			if (_fileSystemProvider.File.Exists(filePath))
			{
				_fileSystemProvider.File.Delete(filePath);
			}
			_fileSystemProvider.File.Move(tempFilePath, filePath);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to store object with key '{key}' to file system", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);

			if (!_fileSystemProvider.File.Exists(filePath))
			{
				return default;
			}

			string serializedData = await _fileSystemProvider.File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

			if (string.IsNullOrEmpty(serializedData))
			{
				return default;
			}

			return await _serializationProvider.DeserializeAsync<T>(serializedData, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to retrieve object with key '{key}' from file system", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<T> RetrieveOrCreateAsync<T>(TKey key, CancellationToken cancellationToken = default) where T : new()
	{
		T? obj = await RetrieveAsync<T>(key, cancellationToken).ConfigureAwait(false);
		return obj ?? new T();
	}

	/// <inheritdoc/>
	public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		cancellationToken.ThrowIfCancellationRequested();

		string filePath = GetFilePath(key);
		bool exists = _fileSystemProvider.File.Exists(filePath);
		return Task.FromResult(exists);
	}

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);

			if (!_fileSystemProvider.File.Exists(filePath))
			{
				return Task.FromResult(false);
			}

			_fileSystemProvider.File.Delete(filePath);
			return Task.FromResult(true);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to remove object with key '{key}' from file system", ex);
		}
	}

	/// <inheritdoc/>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_fileSystemProvider.Directory.Exists(BaseDirectory))
			{
				return Task.FromResult(Enumerable.Empty<TKey>());
			}

			string[] files = _fileSystemProvider.Directory.GetFiles(BaseDirectory, "*" + _serializationProvider.FileExtension, SearchOption.TopDirectoryOnly);
			List<TKey> keys = [];
			foreach (string file in files)
			{
				string? name = _fileSystemProvider.Path.GetFileNameWithoutExtension(file);
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}

				// Names are percent-encoded, so the original key is recovered exactly. Truncated
				// names cannot be decoded and are skipped rather than reported as a wrong key.
				string? decoded = PersistenceProviderUtilities.GetKeyFromFileName(name!);
				if (decoded is not null && PersistenceProviderUtilities.TryConvertToKey(decoded, out TKey key))
				{
					keys.Add(key);
				}
			}

			return Task.FromResult<IEnumerable<TKey>>(keys);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException("Failed to retrieve all keys from file system", ex);
		}
	}

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_fileSystemProvider.Directory.Exists(BaseDirectory))
			{
				return Task.CompletedTask;
			}

			string[] files = _fileSystemProvider.Directory.GetFiles(BaseDirectory, "*" + _serializationProvider.FileExtension, SearchOption.TopDirectoryOnly);
			foreach (string file in files)
			{
				_fileSystemProvider.File.Delete(file);
			}

			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException("Failed to clear all objects from file system", ex);
		}
	}

	private string GetFilePath(TKey key)
	{
		string fileName = PersistenceProviderUtilities.GetSafeFileName(key.ToString()!) + _serializationProvider.FileExtension;
		return _fileSystemProvider.Path.Combine(BaseDirectory, fileName);
	}
}
