// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Replaces a file's content in one step, so a reader sees either the old content or the new, never neither,
/// and reads it back without tripping over a replace in progress.
/// </summary>
/// <remarks>
/// Linked into the FileSystem and Temp providers rather than placed in the interfaces package, following
/// <c>PersistenceDefaults</c>. Each write stages its content in a temporary file of its own, so overlapping
/// writers to one key cannot move each other's file away, and publishes it with an overwriting move rather
/// than a delete followed by a move, so the target never disappears in between (issue #66).
/// </remarks>
internal static class AtomicFile
{
	/// <summary>The extension of a staging file.</summary>
	internal const string StagingExtension = ".tmp";

	private const int MaxAttempts = 50;

	// What File.WriteAllText writes, so a record created here reads back exactly as a replaced one does.
	private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

	// Win32 ERROR_SHARING_VIOLATION and ERROR_LOCK_VIOLATION, carried in the low word of IOException.HResult.
	private const int SharingViolation = 32;
	private const int LockViolation = 33;

	// Off Windows, .NET emulates FileShare.None with an advisory lock, and a second open of a file held that way
	// fails with EWOULDBLOCK, carried as the raw errno: 11 on Linux, 35 on macOS.
	private const int UnixWouldBlockLinux = 11;
	private const int UnixWouldBlockMac = 35;

	private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

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
		string tempFilePath = StagingPathFor(fileSystem, filePath);

		try
		{
			await fileSystem.File.WriteAllTextAsync(tempFilePath, content, cancellationToken).ConfigureAwait(false);

			// Every attempt but the last may be refused and retried; the last one's failure surfaces.
			for (int attempt = 1; attempt < MaxAttempts; attempt++)
			{
				try
				{
					Replace(fileSystem, tempFilePath, filePath);
					return;
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					// Windows refuses to replace a file another handle has open without delete sharing, which
					// a concurrent reader or a second writer's replace holds for a moment. Give it that moment.
					await Task.Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
				}
			}

			Replace(fileSystem, tempFilePath, filePath);
		}
		finally
		{
			if (fileSystem.File.Exists(tempFilePath))
			{
				fileSystem.File.Delete(tempFilePath);
			}
		}
	}

	/// <summary>
	/// Reads <paramref name="filePath"/>, waiting out a concurrent <see cref="WriteAllTextAsync"/> replacing it.
	/// </summary>
	/// <param name="fileSystem">The filesystem to read through.</param>
	/// <param name="filePath">The file to read.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The file's content.</returns>
	internal static async Task<string> ReadAllTextAsync(IFileSystem fileSystem, string filePath, CancellationToken cancellationToken)
	{
		for (int attempt = 1; attempt < MaxAttempts; attempt++)
		{
			try
			{
				return await fileSystem.File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
			}
			catch (IOException ex) when (IsSharingViolation(ex))
			{
				// Windows holds the target exclusively for the instant an overwriting move swaps it, so a
				// read that lands then is refused rather than served either version. Only that refusal is
				// retried; a missing file and every other error surface at once.
				await Task.Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
			}
		}

		return await fileSystem.File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Writes <paramref name="content"/> to <paramref name="filePath"/> only if no file is there yet, so a record
	/// another writer published first is never replaced.
	/// </summary>
	/// <param name="fileSystem">The filesystem to write through.</param>
	/// <param name="filePath">The file to create.</param>
	/// <param name="content">The text to write.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if this call created the file; <see langword="false"/> if one was already there.</returns>
	internal static async Task<bool> TryCreateAsync(IFileSystem fileSystem, string filePath, string content, CancellationToken cancellationToken)
	{
		// Not staged and moved like a replace: a move that does not overwrite is a check followed by a rename on
		// Unix, so a writer publishing in between is silently replaced. Opening with CreateNew is the one create
		// that is atomic on every platform. Readers can see the file empty until the content is written, which
		// FileRecordStore waits out; on Windows the share mode makes them wait here instead.
		Stream stream;
		try
		{
			stream = fileSystem.File.Open(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
		}
		catch (IOException ex) when (ex.GetType() == typeof(IOException))
		{
			// The file already exists, or did until a remover took it just now; either way this call did not
			// create it, and the caller looks again. A missing directory and the like are subclasses and surface.
			return false;
		}

		byte[] bytes = Utf8NoBom.GetBytes(content);
		await using (stream.ConfigureAwait(false))
		{
			await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
			await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
		}

		return true;
	}

	/// <summary>
	/// Deletes <paramref name="filePath"/>, waiting out a concurrent reader or replace holding it open.
	/// </summary>
	/// <param name="fileSystem">The filesystem to delete through.</param>
	/// <param name="filePath">The file to delete.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the file was there to delete; <see langword="false"/> if it was already gone.</returns>
	internal static async Task<bool> DeleteAsync(IFileSystem fileSystem, string filePath, CancellationToken cancellationToken)
	{
		for (int attempt = 1; ; attempt++)
		{
			if (!fileSystem.File.Exists(filePath))
			{
				return false;
			}

			try
			{
				fileSystem.File.Delete(filePath);
				return true;
			}
			catch (DirectoryNotFoundException)
			{
				return false;
			}
			catch (Exception ex) when (attempt < MaxAttempts && ex is IOException or UnauthorizedAccessException)
			{
				// Windows refuses to delete a file a reader has open without delete sharing, as ReadAllText
				// opens it, and a replace holds the target for an instant. The same moment as above is enough.
				await Task.Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Whether <paramref name="fileName"/> is the name of a staging file this class writes: a GUID in
	/// <c>N</c> format with a <c>.tmp</c> extension. No key encodes to that shape with a serializer's extension,
	/// so a file so named was left behind by a write that never finished.
	/// </summary>
	/// <param name="fileName">The file name, without its directory.</param>
	/// <returns><see langword="true"/> for a staging file's name.</returns>
	internal static bool IsStagingFileName(string fileName)
		=> fileName.Length == 36
			&& fileName.EndsWith(StagingExtension, StringComparison.OrdinalIgnoreCase)
			&& Guid.TryParseExact(fileName[..32], "N", out _);

	private static string StagingPathFor(IFileSystem fileSystem, string filePath)
	{
		// Same directory as the target, so the move is a rename on one volume. The name is not derived from
		// the target's, because PersistenceProviderUtilities budgets a key's filename for only the extension
		// and ".tmp", and a longer suffix on a long key would exceed the 255-byte name limit. The .tmp
		// extension keeps the staging file out of the providers' "*<extension>" key listings.
		string? directory = fileSystem.Path.GetDirectoryName(filePath);
		string tempFileName = $"{Guid.NewGuid():N}{StagingExtension}";
		return string.IsNullOrEmpty(directory) ? tempFileName : fileSystem.Path.Combine(directory, tempFileName);
	}

#if NETSTANDARD2_1
	private static void Replace(IFileSystem fileSystem, string tempFilePath, string filePath)
	{
		// No overwriting Move here. Replace swaps an existing target in one step; a target that does not exist
		// yet takes a plain Move, and one created by another writer in between makes that Move throw, so the
		// retry takes the Replace path.
		if (fileSystem.File.Exists(filePath))
		{
			fileSystem.File.Replace(tempFilePath, filePath, destinationBackupFileName: null);
		}
		else
		{
			fileSystem.File.Move(tempFilePath, filePath);
		}
	}
#else
	private static void Replace(IFileSystem fileSystem, string tempFilePath, string filePath)
		=> fileSystem.File.Move(tempFilePath, filePath, overwrite: true);
#endif

	private static bool IsSharingViolation(IOException ex)
		=> IsWindows
			? (ex.HResult & 0xFFFF) is SharingViolation or LockViolation
			: ex.HResult is UnixWouldBlockLinux or UnixWouldBlockMac;

	private static int Backoff(int attempt) => Math.Min(attempt * 2, 20);
}
