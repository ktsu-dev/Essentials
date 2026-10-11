// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.InMemory;
using ktsu.Essentials.LoggingProviders.Console;
using ktsu.Essentials.LoggingProviders.File;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class FileLoggingProviderTests
{
	private const string LogDirectory = "logs";

	[TestMethod]
	public void Writes_One_Line_Per_Entry()
	{
		InMemoryFileSystemProvider fileSystem = new();
		ILoggingProvider logger = Create(fileSystem);

		logger.LogInformation("first");
		logger.LogWarning("second");
		logger.LogTrace("filtered out");

		string[] lines = ReadLines(fileSystem, ((FileLoggingProvider)logger).FilePath);
		Assert.HasCount(2, lines);
		StringAssert.EndsWith(lines[0], " INFO first");
		StringAssert.EndsWith(lines[1], " WARN second");
	}

	[TestMethod]
	public void Entries_Start_With_A_Utc_Timestamp()
	{
		InMemoryFileSystemProvider fileSystem = new();
		FileLoggingProvider logger = Create(fileSystem);
		DateTime before = DateTime.UtcNow.AddSeconds(-1);

		logger.Log(LogLevel.Error, "stamped");

		string line = ReadLines(fileSystem, logger.FilePath).Single();
		string stamp = line[..line.IndexOf(' ', StringComparison.Ordinal)];
		DateTime parsed = DateTime.ParseExact(stamp, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture,
			DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
		Assert.IsTrue(parsed >= before && parsed <= DateTime.UtcNow.AddSeconds(1), $"{stamp} should be now, in UTC");
	}

	[TestMethod]
	public void Line_Breaks_In_Messages_And_Exceptions_Stay_On_One_Line()
	{
		InMemoryFileSystemProvider fileSystem = new();
		FileLoggingProvider logger = Create(fileSystem);

		InvalidOperationException exception;
		try
		{
			throw new InvalidOperationException("inner\r\nfailure");
		}
		catch (InvalidOperationException caught)
		{
			exception = caught;
		}

		logger.Log(LogLevel.Error, exception, "two\nlines \\ and\u2028more");

		string line = ReadLines(fileSystem, logger.FilePath).Single();
		StringAssert.Contains(line, @" ERROR two\nlines \\ and\u2028more | System.InvalidOperationException: inner\r\nfailure");
		StringAssert.Contains(line, @"\n   at ", "the stack trace is kept, escaped onto the same line");
	}

	[TestMethod]
	public void Format_Escapes_Every_Line_Terminator()
	{
		string line = FileLoggingProvider.FormatEntry(
			new DateTimeOffset(2026, 10, 11, 15, 2, 37, 123, TimeSpan.FromHours(2)),
			LogLevel.Critical,
			"a\rb\nc\u0085d\u2028e\u2029f\\g\th",
			exception: null);

		Assert.AreEqual(@"2026-10-11T13:02:37.123Z CRIT a\rb\nc\u0085d\u2028e\u2029f\\g" + "\th", line);
	}

	[TestMethod]
	public void Rolls_When_The_Next_Entry_Would_Pass_The_Size_Limit()
	{
		InMemoryFileSystemProvider fileSystem = new();
		FileLoggingProvider logger = Create(fileSystem, maxFileSizeBytes: 200, maxFileCount: 3);

		for (int i = 0; i < 20; i++)
		{
			((ILoggingProvider)logger).LogInformation($"entry {i:00} padded out to a reasonable length");
		}

		string[] current = ReadLines(fileSystem, logger.FilePath);
		string[] previous = ReadLines(fileSystem, logger.GetRolledFilePath(1));
		string[] oldest = ReadLines(fileSystem, logger.GetRolledFilePath(2));

		Assert.IsFalse(fileSystem.File.Exists(logger.GetRolledFilePath(3)), "no more than three files are kept");
		foreach (string path in new[] { logger.FilePath, logger.GetRolledFilePath(1), logger.GetRolledFilePath(2) })
		{
			Assert.IsLessThanOrEqualTo(200L, fileSystem.FileInfo.New(path).Length, $"{path} stays within the limit");
		}

		StringAssert.Contains(current[^1], "entry 19", "the newest entry is in the file being written");
		int[] numbers = [.. oldest.Concat(previous).Concat(current).Select(EntryNumber)];
		CollectionAssert.AreEqual(Enumerable.Range(20 - numbers.Length, numbers.Length).ToArray(), numbers,
			"the kept files hold the most recent entries in order, with none lost between them");
	}

	[TestMethod]
	public void Rolled_Files_Number_Before_The_Extension()
	{
		FileLoggingProvider logger = Create(new InMemoryFileSystemProvider(), fileName: "imagegui.log");
		Assert.AreEqual(System.IO.Path.Join(LogDirectory, "imagegui.2.log"), logger.GetRolledFilePath(2));
	}

	[TestMethod]
	public void With_One_File_A_Full_Log_Starts_Again()
	{
		InMemoryFileSystemProvider fileSystem = new();
		FileLoggingProvider logger = Create(fileSystem, maxFileSizeBytes: 100, maxFileCount: 1);

		for (int i = 0; i < 10; i++)
		{
			((ILoggingProvider)logger).LogInformation($"entry {i:00} with some padding");
		}

		Assert.IsFalse(fileSystem.File.Exists(logger.GetRolledFilePath(1)));
		Assert.AreEqual(9, EntryNumber(ReadLines(fileSystem, logger.FilePath)[^1]));
		Assert.IsLessThanOrEqualTo(100L, fileSystem.FileInfo.New(logger.FilePath).Length);
	}

	[TestMethod]
	public void An_Entry_Larger_Than_The_Limit_Is_Written_Whole()
	{
		InMemoryFileSystemProvider fileSystem = new();
		FileLoggingProvider logger = Create(fileSystem, maxFileSizeBytes: 50, maxFileCount: 2);

		((ILoggingProvider)logger).LogInformation("short");
		string large = new('x', 200);
		((ILoggingProvider)logger).LogInformation(large);

		StringAssert.EndsWith(ReadLines(fileSystem, logger.FilePath).Single(), large);
		StringAssert.EndsWith(ReadLines(fileSystem, logger.GetRolledFilePath(1)).Single(), "short");
	}

	[TestMethod]
	public void Appends_To_A_Log_Left_By_An_Earlier_Run()
	{
		InMemoryFileSystemProvider fileSystem = new();
		((ILoggingProvider)Create(fileSystem)).LogInformation("first run");
		((ILoggingProvider)Create(fileSystem)).LogInformation("second run");

		string[] lines = ReadLines(fileSystem, Create(fileSystem).FilePath);
		Assert.HasCount(2, lines);
	}

	[TestMethod]
	public async Task Is_Safe_To_Call_From_Many_Threads()
	{
		InMemoryFileSystemProvider fileSystem = new();
		FileLoggingProvider logger = Create(fileSystem, maxFileSizeBytes: 4096, maxFileCount: 100);
		const int threads = 8;
		const int perThread = 100;

		await Task.WhenAll(Enumerable.Range(0, threads).Select(thread => Task.Run(() =>
		{
			for (int i = 0; i < perThread; i++)
			{
				((ILoggingProvider)logger).LogInformation($"thread {thread} entry {i}");
			}
		}))).ConfigureAwait(false);

		string[] lines = [.. fileSystem.Directory.GetFiles(LogDirectory).SelectMany(path => ReadLines(fileSystem, path))];
		Assert.HasCount(threads * perThread, lines, "every entry is written exactly once");
		Assert.IsTrue(lines.All(line => line.Contains(" INFO thread ", StringComparison.Ordinal)), "no entry is interleaved with another");
		Assert.AreEqual(0, logger.DroppedEntryCount);
	}

	[TestMethod]
	public void A_Write_That_Fails_Is_Dropped_Rather_Than_Thrown()
	{
		InMemoryFileSystemProvider fileSystem = new();
		fileSystem.File.WriteAllText(LogDirectory, "a file where the log directory should be");
		FileLoggingProvider logger = Create(fileSystem);

		((ILoggingProvider)logger).LogError("nowhere to go");
		((ILoggingProvider)logger).LogError(new InvalidOperationException("also nowhere"), "still nowhere");

		Assert.AreEqual(2, logger.DroppedEntryCount);
	}

	[TestMethod]
	public void IsEnabled_Respects_The_Minimum_Level()
	{
		FileLoggingProvider logger = Create(new InMemoryFileSystemProvider(), minimumLevel: LogLevel.Warning);

		Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
		Assert.IsTrue(logger.IsEnabled(LogLevel.Warning));
		Assert.IsFalse(logger.IsEnabled(LogLevel.None));

		logger.MinimumLevel = LogLevel.Trace;
		Assert.IsTrue(logger.IsEnabled(LogLevel.Trace));
	}

	[TestMethod]
	public void Invalid_Options_Are_Rejected()
	{
		InMemoryFileSystemProvider fileSystem = new();

		Assert.ThrowsExactly<ArgumentNullException>(() => new FileLoggingProvider(null!, new FileLoggingOptions { Directory = LogDirectory }));
		Assert.ThrowsExactly<ArgumentNullException>(() => new FileLoggingProvider(fileSystem, null!));
		Assert.ThrowsExactly<ArgumentException>(() => new FileLoggingProvider(fileSystem, new FileLoggingOptions()));
		Assert.ThrowsExactly<ArgumentException>(() => new FileLoggingProvider(fileSystem, new FileLoggingOptions { Directory = LogDirectory, FileName = "sub/log.txt" }));
		Assert.ThrowsExactly<ArgumentException>(() => new FileLoggingProvider(fileSystem, new FileLoggingOptions { Directory = LogDirectory, MaxFileSizeBytes = 0 }));
		Assert.ThrowsExactly<ArgumentException>(() => new FileLoggingProvider(fileSystem, new FileLoggingOptions { Directory = LogDirectory, MaxFileCount = 0 }));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Create(fileSystem).GetRolledFilePath(0));
	}

	[TestMethod]
	public void Registers_Beside_The_Console_Logger()
	{
		FileLoggingOptions options = new() { Directory = LogDirectory };
		ServiceCollection services = new();
		services.AddInMemoryFileSystemProvider()
			.AddConsoleLoggingProvider()
			.AddFileLoggingProvider(options)
			.AddFileLoggingProvider(new FileLoggingOptions { Directory = "ignored" });
		options.Directory = "changed after registration";
		using ServiceProvider provider = services.BuildServiceProvider();

		ILoggingProvider[] loggers = [.. provider.GetServices<ILoggingProvider>()];
		FileLoggingProvider file = provider.GetRequiredService<FileLoggingProvider>();
		Assert.HasCount(2, loggers);
		Assert.AreSame(file, loggers.OfType<FileLoggingProvider>().Single());
		Assert.AreEqual(LogDirectory, file.Directory, "the first registration's options, as they were when registered");

		((ILoggingProvider)file).LogWarning("through the container");
		InMemoryFileSystemProvider fileSystem = provider.GetRequiredService<InMemoryFileSystemProvider>();
		StringAssert.EndsWith(ReadLines(fileSystem, file.FilePath).Single(), "WARN through the container");
	}

	private static FileLoggingProvider Create(
		IFileSystemProvider fileSystem,
		long maxFileSizeBytes = FileLoggingOptions.DefaultMaxFileSizeBytes,
		int maxFileCount = FileLoggingOptions.DefaultMaxFileCount,
		LogLevel minimumLevel = LogLevel.Information,
		string fileName = FileLoggingOptions.DefaultFileName)
		=> new(fileSystem, new FileLoggingOptions
		{
			Directory = LogDirectory,
			FileName = fileName,
			MaxFileSizeBytes = maxFileSizeBytes,
			MaxFileCount = maxFileCount,
			MinimumLevel = minimumLevel,
		});

	private static string[] ReadLines(IFileSystemProvider fileSystem, string path)
		=> fileSystem.File.ReadAllLines(path);

	private static int EntryNumber(string line)
	{
		int start = line.IndexOf("entry ", StringComparison.Ordinal) + "entry ".Length;
		return int.Parse(line.AsSpan(start, 2), CultureInfo.InvariantCulture);
	}
}
