// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.LoggingProviders.File;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ktsu.Essentials;

/// <summary>
/// A logging provider that appends one line per entry to a file, rolling to a new file when it grows
/// past a size limit and keeping a bounded number of old files.
/// </summary>
/// <remarks>
/// <para>
/// Every entry is one line, <c>2026-10-11T15:02:37.123Z WARN message</c>, with the timestamp in UTC.
/// Line breaks, backslashes and other line terminators inside a message or exception are escaped
/// (<c>\n</c>, <c>\r</c> and <c>\\</c>, and the Unicode line and paragraph separators by code
/// point), so a line is always exactly one entry and a reader can split the file on newlines. An exception follows the message after <c> | </c>.
/// </para>
/// <para>
/// All file access goes through the <see cref="IFileSystemProvider"/> it is given, so a test can log
/// to an in-memory file system. The file is opened for each entry and closed again, which means no
/// handle is held between entries: another program can read, copy or delete the log while the
/// application runs, and nothing needs disposing. Entries are serialized within the provider, so it
/// is safe to call from any thread. Rolling is not coordinated between processes, so give each
/// process its own file.
/// </para>
/// <para>
/// Logging never throws for an I/O failure. An entry that cannot be written, because the disk is
/// full or the file is locked, is dropped and counted in <see cref="DroppedEntryCount"/>. A roll that
/// cannot be done, because another program holds a file without delete sharing, leaves every file
/// where it was, and the entry is appended to the active file past the size limit until a roll succeeds.
/// </para>
/// </remarks>
public class FileLoggingProvider : ILoggingProvider
{
	private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

	private readonly IFileSystemProvider fileSystem;
	private readonly long maxFileSizeBytes;
	private readonly int maxFileCount;
	private readonly string baseName;
	private readonly string extension;
	private readonly System.Threading.Lock writeLock = new();
	private long droppedEntryCount;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileLoggingProvider"/> class.
	/// </summary>
	/// <param name="fileSystem">The file system the log is written through.</param>
	/// <param name="options">Where to write and when to roll. The values are copied.</param>
	/// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> or <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// The directory is empty, the file name is empty or contains a directory separator, the size limit is
	/// not positive, or the file count is less than one.
	/// </exception>
	public FileLoggingProvider(IFileSystemProvider fileSystem, FileLoggingOptions options)
	{
		Ensure.NotNull(fileSystem);
		Ensure.NotNull(options);

		if (string.IsNullOrWhiteSpace(options.Directory))
		{
			throw new ArgumentException("The log directory cannot be null or whitespace.", nameof(options));
		}

		if (string.IsNullOrWhiteSpace(options.FileName)
			|| options.FileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
		{
			throw new ArgumentException("The log file name must be a file name, without a directory.", nameof(options));
		}

		if (options.MaxFileSizeBytes <= 0)
		{
			throw new ArgumentException("The maximum file size must be positive.", nameof(options));
		}

		if (options.MaxFileCount < 1)
		{
			throw new ArgumentException("At least one log file must be kept.", nameof(options));
		}

		this.fileSystem = fileSystem;
		maxFileSizeBytes = options.MaxFileSizeBytes;
		maxFileCount = options.MaxFileCount;
		MinimumLevel = options.MinimumLevel;
		Directory = options.Directory;
		FilePath = fileSystem.Path.Combine(options.Directory, options.FileName);
		baseName = fileSystem.Path.GetFileNameWithoutExtension(options.FileName);
		extension = fileSystem.Path.GetExtension(options.FileName);
	}

	/// <summary>
	/// Gets the directory the log files are written to.
	/// </summary>
	public string Directory { get; }

	/// <summary>
	/// Gets the path of the file currently being written.
	/// </summary>
	public string FilePath { get; }

	/// <summary>
	/// Gets or sets the minimum log level. Entries below this level are ignored.
	/// </summary>
	public LogLevel MinimumLevel { get; set; }

	/// <summary>
	/// Gets the number of entries that could not be written because of an I/O failure.
	/// </summary>
	public long DroppedEntryCount => System.Threading.Interlocked.Read(ref droppedEntryCount);

	/// <summary>
	/// Gets the path of an older file, where 1 is the most recent one rolled away.
	/// </summary>
	/// <param name="index">The file's position, from 1 to one less than the number of files kept.</param>
	/// <returns>The path of that file, whether or not it exists.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 1.</exception>
	public string GetRolledFilePath(int index)
	{
		if (index < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(index), index, "Rolled files are numbered from 1.");
		}

		return fileSystem.Path.Combine(Directory, string.Format(CultureInfo.InvariantCulture, "{0}.{1}{2}", baseName, index, extension));
	}

	/// <summary>
	/// Writes a log entry at the specified level.
	/// </summary>
	/// <param name="level">The severity level of the log entry.</param>
	/// <param name="message">The log message.</param>
	public void Log(LogLevel level, string message)
	{
		if (IsEnabled(level))
		{
			Write(FormatEntry(DateTimeOffset.UtcNow, level, message, exception: null));
		}
	}

	/// <summary>
	/// Writes a log entry at the specified level with an associated exception.
	/// </summary>
	/// <param name="level">The severity level of the log entry.</param>
	/// <param name="exception">The exception associated with this log entry.</param>
	/// <param name="message">The log message.</param>
	public void Log(LogLevel level, Exception exception, string message)
	{
		if (IsEnabled(level))
		{
			Write(FormatEntry(DateTimeOffset.UtcNow, level, message, exception));
		}
	}

	/// <summary>
	/// Checks if the given log level is enabled.
	/// </summary>
	/// <param name="level">The log level to check.</param>
	/// <returns>True if the log level is enabled, false otherwise.</returns>
	public bool IsEnabled(LogLevel level) => level >= MinimumLevel && level != LogLevel.None;

	internal static string FormatEntry(DateTimeOffset timestamp, LogLevel level, string? message, Exception? exception)
	{
		StringBuilder builder = new();
		builder.Append(timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
		builder.Append(' ');
		builder.Append(LevelName(level));
		builder.Append(' ');
		AppendEscaped(builder, message);

		if (exception is not null)
		{
			builder.Append(" | ");
			AppendEscaped(builder, exception.ToString());
		}

		return builder.ToString();
	}

	private static string LevelName(LogLevel level) => level switch
	{
		LogLevel.Trace => "TRACE",
		LogLevel.Debug => "DEBUG",
		LogLevel.Information => "INFO",
		LogLevel.Warning => "WARN",
		LogLevel.Error => "ERROR",
		LogLevel.Critical => "CRIT",
		_ => level.ToString().ToUpperInvariant(),
	};

	private static void AppendEscaped(StringBuilder builder, string? text)
	{
		if (text is null)
		{
			return;
		}

		foreach (char c in text)
		{
			switch (c)
			{
				case '\\':
					builder.Append(@"\\");
					break;
				case '\n':
					builder.Append(@"\n");
					break;
				case '\r':
					builder.Append(@"\r");
					break;
				case '\u0085':
				case '\u2028':
				case '\u2029':
					builder.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					break;
				default:
					builder.Append(c);
					break;
			}
		}
	}

	private void Write(string entry)
	{
		byte[] bytes = Utf8.GetBytes(entry + Environment.NewLine);

		lock (writeLock)
		{
			try
			{
				fileSystem.Directory.CreateDirectory(Directory);
				long length = fileSystem.File.Exists(FilePath) ? fileSystem.FileInfo.New(FilePath).Length : 0;
				// A roll that cannot be done, because another program holds the file, leaves every file as it was,
				// and the entry is appended past the limit rather than dropped: the write itself still succeeds.
				if (length > 0 && length + bytes.Length > maxFileSizeBytes)
				{
					_ = TryRoll();
				}

				using Stream stream = fileSystem.FileStream.New(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
				stream.Write(bytes, 0, bytes.Length);
			}
			catch (IOException)
			{
				System.Threading.Interlocked.Increment(ref droppedEntryCount);
			}
			catch (UnauthorizedAccessException)
			{
				System.Threading.Interlocked.Increment(ref droppedEntryCount);
			}
		}
	}

	/// <summary>
	/// Moves the active file into slot 1, shifting the rolled files up a slot and discarding the oldest, all or
	/// nothing. Every move is recorded, and a failure undoes them in reverse, so a roll refused part way leaves
	/// every file where it was. The oldest file is moved aside rather than deleted, and deleted only once the roll
	/// is complete: deleting it first meant each refused roll discarded another file, until a log a viewer held
	/// open had lost all its history (issue #120).
	/// </summary>
	/// <returns><see langword="true"/> if the active file was rolled.</returns>
	private bool TryRoll()
	{
		if (maxFileCount == 1)
		{
			try
			{
				fileSystem.File.Delete(FilePath);
				return true;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return false;
			}
		}

		// Unique names, so a staging file a crash left behind cannot block every later roll.
		string activeStaging = StagingPath();
		string? oldestStaging = null;
		List<(string From, string To)> moves = [];

		try
		{
			Move(moves, FilePath, activeStaging);

			string oldest = GetRolledFilePath(maxFileCount - 1);
			if (fileSystem.File.Exists(oldest))
			{
				oldestStaging = StagingPath();
				Move(moves, oldest, oldestStaging);
			}

			for (int index = maxFileCount - 2; index >= 1; index--)
			{
				string source = GetRolledFilePath(index);
				if (fileSystem.File.Exists(source))
				{
					Move(moves, source, GetRolledFilePath(index + 1));
				}
			}

			Move(moves, activeStaging, GetRolledFilePath(1));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			for (int i = moves.Count - 1; i >= 0; i--)
			{
				try
				{
					fileSystem.File.Move(moves[i].To, moves[i].From);
				}
				catch (Exception undo) when (undo is IOException or UnauthorizedAccessException)
				{
					// Nothing more can be done for this file; the rest are still put back.
				}
			}

			return false;
		}

		if (oldestStaging is not null)
		{
			try
			{
				fileSystem.File.Delete(oldestStaging);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// The roll is complete; a discarded file another program holds open is left for it to release.
			}
		}

		return true;
	}

	/// <summary>Moves a file and records the move, so a roll refused part way can undo it.</summary>
	/// <param name="moves">The moves made so far.</param>
	/// <param name="from">The file to move.</param>
	/// <param name="to">Where it goes.</param>
	private void Move(List<(string From, string To)> moves, string from, string to)
	{
		fileSystem.File.Move(from, to);
		moves.Add((from, to));
	}

	private string StagingPath()
		=> fileSystem.Path.Combine(Directory, string.Format(CultureInfo.InvariantCulture, "{0}.{1:N}.rolling", baseName, Guid.NewGuid()));
}
