// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// One record per file in a directory, each named after its key and holding the serialized object: the whole
/// of what the FileSystem and Temp persistence providers do, which differ only in where the directory is.
/// </summary>
/// <remarks>
/// Linked into both providers rather than placed in the interfaces package, following <c>AtomicFile</c>.
/// Every operation lets cancellation through as itself rather than wrapping it (issue #121), and wraps every
/// other failure in a <see cref="PersistenceProviderException"/> naming <c>location</c>.
/// </remarks>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
/// <param name="fileSystem">The file system to store through.</param>
/// <param name="serializer">The serializer records are written and read with.</param>
/// <param name="directory">The directory records are stored in.</param>
/// <param name="location">How failure messages name the store, such as "file system".</param>
internal sealed class FileRecordStore<TKey>(IFileSystemProvider fileSystem, ISerializationProvider serializer, string directory, string location)
	where TKey : notnull
{
	/// <summary>
	/// How old a staging file has to be before <see cref="ClearAsync"/> treats it as abandoned. A write in
	/// progress stages its file for milliseconds, and deleting it would fail that write.
	/// </summary>
	private static readonly TimeSpan AbandonedStagingAge = TimeSpan.FromMinutes(1);

	/// <summary>How many times an empty record is read before it is believed.</summary>
	private const int EmptyRecordAttempts = 10;

	/// <summary>Gets the directory records are stored in.</summary>
	internal string Directory { get; } = directory;

	/// <summary>Stores <paramref name="obj"/> under <paramref name="key"/>, or removes the key when it is null.</summary>
	/// <typeparam name="T">The type of object to store.</typeparam>
	/// <param name="key">The key to store under.</param>
	/// <param name="obj">The object to store.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes once the record is in place.</returns>
	internal async Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken)
	{
		if (obj is null)
		{
			await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
			return;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);
			string serializedData = await serializer.SerializeAsync(obj, cancellationToken).ConfigureAwait(false);
			EnsureDirectory();
			await AtomicFile.WriteAllTextAsync(fileSystem, filePath, serializedData, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new PersistenceProviderException($"Failed to store object with key '{key}' to {location}", ex);
		}
	}

	/// <summary>Retrieves the object stored under <paramref name="key"/>.</summary>
	/// <typeparam name="T">The type of object to retrieve.</typeparam>
	/// <param name="key">The key to read.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The stored object, or <see langword="default"/> when nothing is stored under the key.</returns>
	/// <exception cref="PersistenceProviderException">
	/// The record could not be read, including when it is there but cannot be deserialized as <typeparamref name="T"/>.
	/// </exception>
	internal async Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string? serializedData = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
			return string.IsNullOrEmpty(serializedData)
				? default
				: await DeserializeAsync<T>(key, serializedData!, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not PersistenceProviderException)
		{
			throw new PersistenceProviderException($"Failed to retrieve object with key '{key}' from {location}", ex);
		}
	}

	/// <summary>
	/// Retrieves the object stored under <paramref name="key"/>, or creates, stores and returns a new one.
	/// </summary>
	/// <remarks>
	/// The new record is published only if no record has appeared in the meantime (issue #144): when another
	/// writer gets there first, its record is the one returned and the new instance is discarded. A record that is
	/// there but cannot be read is never replaced (issue #82); that throws instead, leaving the file as it was.
	/// </remarks>
	/// <typeparam name="T">The type of object to retrieve or create.</typeparam>
	/// <param name="key">The key to read or create.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The stored object, or the new instance now stored under <paramref name="key"/>.</returns>
	internal async Task<T> RetrieveOrCreateAsync<T>(TKey key, CancellationToken cancellationToken)
		where T : new()
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);

			// Each pass either returns or sees the record removed between looking and creating, which only a
			// concurrent remover can do, so a handful of passes is far more than any real interleaving needs.
			for (int attempt = 1; ; attempt++)
			{
				string? serializedData = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
				if (!string.IsNullOrEmpty(serializedData))
				{
					return (await DeserializeAsync<T>(key, serializedData!, cancellationToken).ConfigureAwait(false))!;
				}

				T newInstance = new();
				string newData = await serializer.SerializeAsync(newInstance!, cancellationToken).ConfigureAwait(false);
				EnsureDirectory();

				if (serializedData is not null || attempt >= 10)
				{
					// An empty file holds no record to keep, so it is replaced as a missing one would be created.
					await AtomicFile.WriteAllTextAsync(fileSystem, filePath, newData, cancellationToken).ConfigureAwait(false);
					return newInstance;
				}

				if (await AtomicFile.TryCreateAsync(fileSystem, filePath, newData, cancellationToken).ConfigureAwait(false))
				{
					return newInstance;
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not PersistenceProviderException)
		{
			throw new PersistenceProviderException($"Failed to retrieve or create object with key '{key}' in {location}", ex);
		}
	}

	/// <summary>Whether a record is stored under <paramref name="key"/>.</summary>
	/// <param name="key">The key to look for.</param>
	/// <returns><see langword="true"/> if a record is stored under the key.</returns>
	internal bool Exists(TKey key) => fileSystem.File.Exists(GetFilePath(key));

	/// <summary>Removes the record stored under <paramref name="key"/>.</summary>
	/// <param name="key">The key to remove.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if there was a record to remove.</returns>
	internal async Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await AtomicFile.DeleteAsync(fileSystem, GetFilePath(key), cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new PersistenceProviderException($"Failed to remove object with key '{key}' from {location}", ex);
		}
	}

	/// <summary>Lists the keys of every record stored.</summary>
	/// <returns>The keys, in no particular order.</returns>
	internal IEnumerable<TKey> GetAllKeys()
	{
		try
		{
			if (!fileSystem.Directory.Exists(Directory))
			{
				return [];
			}

			List<TKey> keys = [];
			foreach (string? name in fileSystem.Directory.GetFiles(Directory, "*" + serializer.FileExtension, SearchOption.TopDirectoryOnly).Select(fileSystem.Path.GetFileNameWithoutExtension))
			{
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

			return keys;
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to retrieve all keys from {location}", ex);
		}
	}

	/// <summary>
	/// Removes every record, and every staging file a write abandoned when the process stopped mid-write
	/// (issue #145), since each of those holds a whole record too.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes once the records are gone.</returns>
	internal async Task ClearAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!fileSystem.Directory.Exists(Directory))
			{
				return;
			}

			foreach (string file in fileSystem.Directory.GetFiles(Directory, "*" + serializer.FileExtension, SearchOption.TopDirectoryOnly))
			{
				await AtomicFile.DeleteAsync(fileSystem, file, cancellationToken).ConfigureAwait(false);
			}

			DateTime abandonedBefore = DateTime.UtcNow - AbandonedStagingAge;
			IEnumerable<string> abandoned = fileSystem.Directory
				.GetFiles(Directory, "*" + AtomicFile.StagingExtension, SearchOption.TopDirectoryOnly)
				.Where(file => AtomicFile.IsStagingFileName(fileSystem.Path.GetFileName(file))
					&& fileSystem.File.GetLastWriteTimeUtc(file) < abandonedBefore);
			foreach (string file in abandoned)
			{
				await AtomicFile.DeleteAsync(fileSystem, file, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new PersistenceProviderException($"Failed to clear all objects from {location}", ex);
		}
	}

	/// <summary>
	/// Reads the record stored under <paramref name="key"/>, or returns <see langword="null"/> when there is none,
	/// including one a concurrent remover deleted between looking and reading (issue #123).
	/// </summary>
	private async Task<string?> ReadAsync(TKey key, CancellationToken cancellationToken)
	{
		string filePath = GetFilePath(key);
		if (!fileSystem.File.Exists(filePath))
		{
			return null;
		}

		try
		{
			// A record being created is empty until its creator has written it (AtomicFile.TryCreateAsync), so
			// an empty file is given a moment to fill before it is taken for an empty record.
			for (int attempt = 1; ; attempt++)
			{
				string content = await AtomicFile.ReadAllTextAsync(fileSystem, filePath, cancellationToken).ConfigureAwait(false);
				if (content.Length > 0 || attempt >= EmptyRecordAttempts)
				{
					return content;
				}

				await Task.Delay(attempt, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
		{
			return null;
		}
	}

	private async Task<T?> DeserializeAsync<T>(TKey key, string serializedData, CancellationToken cancellationToken)
	{
		T? value = await serializer.DeserializeAsync<T>(serializedData, cancellationToken).ConfigureAwait(false);

		// The serializers report content they cannot parse as default rather than throwing, which for a
		// reference type is indistinguishable from a missing key. Content is there, so it is a failure: returning
		// null would invite RetrieveOrCreateAsync, or the caller, to write a fresh default over the record.
		return value is null
			? throw new PersistenceProviderException($"The object stored with key '{key}' in {location} could not be read as {typeof(T).Name}")
			: value;
	}

	private void EnsureDirectory() => fileSystem.Directory.CreateDirectory(Directory);

	private string GetFilePath(TKey key)
	{
		string fileName = PersistenceProviderUtilities.GetSafeFileName(PersistenceProviderUtilities.FormatKey(key)) + serializer.FileExtension;
		return fileSystem.Path.Combine(Directory, fileName);
	}
}
