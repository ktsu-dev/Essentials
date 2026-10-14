// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Replaces a file's content in one step, so a reader sees either the old content or the new, never neither.
/// </summary>
/// <remarks>
/// Linked into the FileSystem and Temp providers rather than placed in the interfaces package, following
/// <c>PersistenceDefaults</c>. Each write stages its content in a temporary file of its own, so overlapping
/// writers to one key cannot move each other's file away, and publishes it with an overwriting move rather
/// than a delete followed by a move, so the target never disappears in between (issue #66).
/// </remarks>
internal static class AtomicFileWrite
{
	private const int MaxMoveAttempts = 50;

	/// <summary>
	/// Writes <paramref name="content"/> to <paramref name="filePath"/>, replacing any existing file atomically.
	/// </summary>
	/// <param name="fileSystem">The filesystem to write through.</param>
	/// <param name="filePath">The file to create or replace.</param>
	/// <param name="content">The text to write.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes once the new content is in place.</returns>
	internal static async Task WriteAllTextAsync(IFileSystem fileSystem, string filePath, string content, CancellationToken cancellationToken)
	{
		// Same directory as the target, so the move is a rename on one volume. The name is not derived from
		// the target's, because PersistenceProviderUtilities budgets a key's filename for only the extension
		// and ".tmp", and a longer suffix on a long key would exceed the 255-byte name limit. The .tmp
		// extension keeps the staging file out of the providers' "*<extension>" key listings.
		string? directory = fileSystem.Path.GetDirectoryName(filePath);
		string tempFileName = $"{Guid.NewGuid():N}.tmp";
		string tempFilePath = string.IsNullOrEmpty(directory) ? tempFileName : fileSystem.Path.Combine(directory, tempFileName);

		try
		{
			await fileSystem.File.WriteAllTextAsync(tempFilePath, content, cancellationToken).ConfigureAwait(false);

			for (int attempt = 1; ; attempt++)
			{
				try
				{
#if NETSTANDARD2_1
					// No overwriting Move here. Replace swaps an existing target in one step; a target that does
					// not exist yet takes a plain Move, and one created by another writer in between makes that
					// Move throw, so the retry takes the Replace path.
					if (fileSystem.File.Exists(filePath))
					{
						fileSystem.File.Replace(tempFilePath, filePath, destinationBackupFileName: null);
					}
					else
					{
						fileSystem.File.Move(tempFilePath, filePath);
					}
#else
					fileSystem.File.Move(tempFilePath, filePath, overwrite: true);
#endif
					return;
				}
				catch (Exception ex) when (attempt < MaxMoveAttempts && ex is IOException or UnauthorizedAccessException)
				{
					// Windows refuses to replace a file another handle has open without delete sharing, which
					// a concurrent reader or a second writer's replace holds for a moment. Give it that moment.
					await Task.Delay(Math.Min(attempt * 2, 20), cancellationToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			if (fileSystem.File.Exists(tempFilePath))
			{
				fileSystem.File.Delete(tempFilePath);
			}
		}
	}
}
