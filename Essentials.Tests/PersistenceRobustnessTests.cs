// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.Essentials.PersistenceProviders.FileSystem;
using ktsu.Essentials.PersistenceProviders.InMemory;
using ktsu.Essentials.PersistenceProviders.Temp;
using ktsu.Essentials.SerializationProviders.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The file-backed providers under the conditions a real application meets: a record it cannot parse, a call
/// cancelled part way, and other tasks reading, writing and removing the same keys.
/// </summary>
[TestClass]
public sealed class PersistenceRobustnessTests
{
	private readonly List<string> directories = [];
	private readonly List<TempPersistenceProvider<string>> temps = [];

	public TestContext TestContext { get; set; } = null!;

	[TestCleanup]
	public void Cleanup()
	{
		foreach (TempPersistenceProvider<string> temp in temps)
		{
			temp.CleanupDirectory();
			temp.Dispose();
		}

		foreach (string directory in directories.Where(Directory.Exists))
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[TestMethod]
	public async Task A_Record_That_Cannot_Be_Parsed_Throws_And_Is_Never_Overwritten()
	{
		// The serializer reports a parse failure as null, which RetrieveOrCreateAsync took for a missing key and
		// replaced with new T(), destroying a hand-edited config over a trailing comma (issue #82).
		foreach ((IPersistenceProvider<string> persistence, string directory) in FileBackedProviders(new JsonSerializationProvider()))
		{
			string name = persistence.ProviderName;
			string path = Path.Join(directory, "settings.json");
			const string malformed = "{\"Name\":\"prod\",}";
			await File.WriteAllTextAsync(path, malformed, TestContext.CancellationToken).ConfigureAwait(false);

			await Assert.ThrowsExactlyAsync<PersistenceProviderException>(
				() => persistence.RetrieveOrCreateAsync<Settings>("settings", TestContext.CancellationToken),
				$"{name} took an unreadable record for a missing one").ConfigureAwait(false);
			await Assert.ThrowsExactlyAsync<PersistenceProviderException>(
				() => persistence.RetrieveAsync<Settings>("settings", TestContext.CancellationToken),
				$"{name} reported an unreadable record as missing").ConfigureAwait(false);

			Assert.AreEqual(malformed, await File.ReadAllTextAsync(path, TestContext.CancellationToken).ConfigureAwait(false), $"{name} changed the unreadable record");
		}
	}

	[TestMethod]
	public async Task A_Missing_Record_Is_Still_Created()
	{
		foreach ((IPersistenceProvider<string> persistence, string _) in FileBackedProviders(new JsonSerializationProvider()))
		{
			Settings created = await persistence.RetrieveOrCreateAsync<Settings>("fresh", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.AreEqual("default", created.Name);

			Settings? stored = await persistence.RetrieveAsync<Settings>("fresh", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsNotNull(stored, $"{persistence.ProviderName} did not store the new instance");
		}
	}

	[TestMethod]
	public async Task Cancellation_Part_Way_Through_Surfaces_As_Cancellation()
	{
		// Cancellation that arrived during an operation was wrapped in a PersistenceProviderException, so
		// callers' cancellation handling saw a storage failure (issue #121).
		CancellingSerializer serializer = new();

		foreach ((IPersistenceProvider<string> persistence, string _) in FileBackedProviders(serializer))
		{
			string name = persistence.ProviderName;

			using (CancellationTokenSource cancellation = serializer.CancelOnSerialize(new()))
			{
				await Assert.ThrowsAsync<OperationCanceledException>(
					() => persistence.StoreAsync("k", new Settings(), cancellation.Token),
					$"{name} wrapped a cancelled store").ConfigureAwait(false);
			}

			serializer.Stop();
			await persistence.StoreAsync("k", new Settings(), TestContext.CancellationToken).ConfigureAwait(false);

			using (CancellationTokenSource cancellation = serializer.CancelOnDeserialize(new()))
			{
				await Assert.ThrowsAsync<OperationCanceledException>(
					() => persistence.RetrieveAsync<Settings>("k", cancellation.Token),
					$"{name} wrapped a cancelled retrieve").ConfigureAwait(false);
			}

			serializer.Stop();
		}
	}

	[TestMethod]
	public async Task Readers_Writers_And_Removers_On_One_Key_Never_Throw()
	{
		// A remove landing between a reader's existence check and its read made the read throw instead of
		// reporting the key missing (issue #123), and on Windows a delete under a reader or a replace failed
		// at once (issue #143).
		foreach ((IPersistenceProvider<string> persistence, string _) in FileBackedProviders(new JsonSerializationProvider()))
		{
			const int rounds = 200;
			Task[] work =
			[
				Repeat(rounds, i => persistence.StoreAsync("k", new Settings { Name = $"writer {i}" }, TestContext.CancellationToken)),
				Repeat(rounds, _ => persistence.RemoveAsync("k", TestContext.CancellationToken)),
				Repeat(rounds, _ => persistence.RetrieveAsync<Settings>("k", TestContext.CancellationToken)),
				Repeat(rounds, _ => persistence.RetrieveOrCreateAsync<Settings>("k", TestContext.CancellationToken)),
			];

			await Task.WhenAll(work).ConfigureAwait(false);
			await persistence.ClearAsync(TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsFalse(await persistence.ExistsAsync("k", TestContext.CancellationToken).ConfigureAwait(false), $"{persistence.ProviderName} did not clear");
		}
	}

	[TestMethod]
	public async Task RetrieveOrCreate_Never_Replaces_A_Record_Stored_Concurrently()
	{
		// The create step stored new T() unconditionally, so a record stored between its look and its store was
		// replaced by defaults (issue #144).
		List<IPersistenceProvider<string>> providers = [.. FileBackedProviders(new JsonSerializationProvider()).Select(p => p.Persistence)];
		providers.Add(new InMemoryPersistenceProvider<string>());

		foreach (IPersistenceProvider<string> persistence in providers)
		{
			for (int round = 0; round < 100; round++)
			{
				await persistence.RemoveAsync("cfg", TestContext.CancellationToken).ConfigureAwait(false);

				Task<Settings> bootstrap = Task.Run(() => persistence.RetrieveOrCreateAsync<Settings>("cfg", TestContext.CancellationToken), TestContext.CancellationToken);
				Task import = Task.Run(() => persistence.StoreAsync("cfg", new Settings { Name = "imported" }, TestContext.CancellationToken), TestContext.CancellationToken);
				await Task.WhenAll(bootstrap, import).ConfigureAwait(false);

				Settings? stored = await persistence.RetrieveAsync<Settings>("cfg", TestContext.CancellationToken).ConfigureAwait(false);
				Assert.AreEqual("imported", stored?.Name, $"{persistence.ProviderName} replaced a concurrent store with a default in round {round}");
			}
		}
	}

	[TestMethod]
	public async Task InMemory_RetrieveOrCreate_Gives_Every_Racing_Caller_The_Stored_Instance()
	{
		InMemoryPersistenceProvider<string> persistence = new();
		Task<Settings>[] callers = [.. Enumerable.Range(0, 32).Select(_ => Task.Run(
			() => persistence.RetrieveOrCreateAsync<Settings>("shared", TestContext.CancellationToken),
			TestContext.CancellationToken))];
		Settings[] results = await Task.WhenAll(callers).ConfigureAwait(false);

		Settings? stored = await persistence.RetrieveAsync<Settings>("shared", TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsTrue(results.All(r => ReferenceEquals(r, stored)), "a caller got an instance that is not the stored one");
	}

	[TestMethod]
	public async Task Clear_Removes_Abandoned_Staging_Files_And_Nothing_Else()
	{
		// A write interrupted by a crash left its "<guid>.tmp", holding a whole record, and ClearAsync left it
		// there (issue #145).
		foreach ((IPersistenceProvider<string> persistence, string directory) in FileBackedProviders(new JsonSerializationProvider()))
		{
			string name = persistence.ProviderName;
			await persistence.StoreAsync("k", new Settings(), TestContext.CancellationToken).ConfigureAwait(false);

			string abandoned = Path.Join(directory, $"{Guid.NewGuid():N}.tmp");
			string inFlight = Path.Join(directory, $"{Guid.NewGuid():N}.tmp");
			string unrelated = Path.Join(directory, "notes.tmp");
			foreach (string path in new[] { abandoned, inFlight, unrelated })
			{
				await File.WriteAllTextAsync(path, "{\"Name\":\"secret\"}", TestContext.CancellationToken).ConfigureAwait(false);
			}

			File.SetLastWriteTimeUtc(abandoned, DateTime.UtcNow.AddHours(-1));
			File.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddHours(-1));

			await persistence.ClearAsync(TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsFalse(File.Exists(abandoned), $"{name} kept an abandoned staging file");
			Assert.IsTrue(File.Exists(inFlight), $"{name} deleted a staging file a write may still be using");
			Assert.IsTrue(File.Exists(unrelated), $"{name} deleted a file it did not write");
			Assert.IsFalse(await persistence.ExistsAsync("k", TestContext.CancellationToken).ConfigureAwait(false));
		}
	}

	private static Task Repeat(int count, Func<int, Task> body) => Task.Run(async () =>
	{
		for (int i = 0; i < count; i++)
		{
			await body(i).ConfigureAwait(false);
		}
	});

	private IEnumerable<(IPersistenceProvider<string> Persistence, string Directory)> FileBackedProviders(ISerializationProvider serializer)
	{
		NativeFileSystemProvider fs = new();

		string fileSystemDir = Path.Join(Path.GetTempPath(), "PersistenceRobustness_" + Guid.NewGuid().ToString("N")[..8]);
		directories.Add(fileSystemDir);
		Directory.CreateDirectory(fileSystemDir);
		yield return (new FileSystemPersistenceProvider<string>(fs, serializer, fileSystemDir), fileSystemDir);

		TempPersistenceProvider<string> temp = new(fs, serializer, "PersistenceRobustness_" + Guid.NewGuid().ToString("N")[..8]);
		temps.Add(temp);
		yield return (temp, temp.BaseDirectory);
	}

	public sealed class Settings
	{
		public string Name { get; set; } = "default";
	}

	/// <summary>A serializer whose work is where the caller's token fires, as a slow one's would.</summary>
	private sealed class CancellingSerializer : ISerializationProvider
	{
		private readonly JsonSerializationProvider inner = new();
		private CancellationTokenSource? onSerialize;
		private CancellationTokenSource? onDeserialize;

		public string FileExtension => inner.FileExtension;

		public CancellationTokenSource CancelOnSerialize(CancellationTokenSource source) => onSerialize = source;

		public CancellationTokenSource CancelOnDeserialize(CancellationTokenSource source) => onDeserialize = source;

		public void Stop() => onSerialize = onDeserialize = null;

		public bool TrySerialize(object obj, TextWriter writer)
		{
			Fire(onSerialize);
			return inner.TrySerialize(obj, writer);
		}

		public T? Deserialize<T>(ReadOnlySpan<byte> data)
		{
			Fire(onDeserialize);
			return inner.Deserialize<T>(data);
		}

		private static void Fire(CancellationTokenSource? source)
		{
			if (source is not null)
			{
				source.Cancel();
				source.Token.ThrowIfCancellationRequested();
			}
		}
	}
}
