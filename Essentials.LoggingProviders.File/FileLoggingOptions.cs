// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.LoggingProviders.File;

using ktsu.Essentials;

/// <summary>
/// Configures where a <see cref="FileLoggingProvider"/> writes and when it rolls to a new file.
/// </summary>
/// <remarks>
/// The provider copies these values when it is constructed, so changing an options instance afterwards
/// does not affect a provider already built from it.
/// </remarks>
public class FileLoggingOptions
{
	/// <summary>The default largest size of one log file, in bytes: 1 MiB.</summary>
	public const long DefaultMaxFileSizeBytes = 1024 * 1024;

	/// <summary>The default number of files kept, counting the one being written.</summary>
	public const int DefaultMaxFileCount = 5;

	/// <summary>The default log file name.</summary>
	public const string DefaultFileName = "log.txt";

	/// <summary>
	/// Gets or sets the directory the log files are written to. It is created on the first write.
	/// </summary>
	public string Directory { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the file being written. Older files insert their number before the
	/// extension, so <c>log.txt</c> rolls to <c>log.1.txt</c>, then <c>log.2.txt</c>, and so on.
	/// </summary>
	public string FileName { get; set; } = DefaultFileName;

	/// <summary>
	/// Gets or sets the size, in bytes, a file may reach before the next entry starts a new one.
	/// </summary>
	/// <remarks>
	/// A file is never left empty to honour the limit: an entry larger than the limit on its own is
	/// written whole to a fresh file.
	/// </remarks>
	public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

	/// <summary>
	/// Gets or sets how many files are kept, counting the one being written. When a roll would exceed it,
	/// the oldest file is deleted. With 1, a full file is deleted and started again.
	/// </summary>
	public int MaxFileCount { get; set; } = DefaultMaxFileCount;

	/// <summary>
	/// Gets or sets the minimum level written. Entries below it are ignored.
	/// </summary>
	public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}
