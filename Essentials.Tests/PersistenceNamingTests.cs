// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.Essentials.PersistenceProviders.FileSystem;
using ktsu.Essentials.SerializationProviders.Json;
using ktsu.Essentials.SerializationProviders.Toml;
using ktsu.Essentials.SerializationProviders.Yaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for how persistence providers name files on disk.
/// </summary>
/// <remarks>
/// Two defects motivated these. Keys were sanitised by replacing every reserved character with an
/// underscore, so <c>a/b</c>, <c>a\b</c> and <c>a_b</c> all resolved to one file and silently overwrote
/// each other. Separately, the file extension was hardcoded to <c>.json</c> regardless of the configured
/// serializer, so a YAML-backed provider wrote <c>.json</c> files containing YAML.
/// </remarks>
[TestClass]
public class PersistenceNamingTests
{
	public TestContext TestContext { get; set; } = null!;

	private static readonly string[] CollidingKeys = ["a/b", "a\\b", "a_b", "a:b", "a|b", "a*b", "a?b"];

	private static readonly string[] SingleSettingsKey = ["settings"];

	[TestMethod]
	public void SafeFileName_Does_Not_Collide_For_Distinct_Keys()
	{
		List<string> names = [.. CollidingKeys.Select(PersistenceProviderUtilities.GetSafeFileName)];

		CollectionAssert.AllItemsAreUnique(names, "Distinct keys must never map onto the same filename");
	}

	[TestMethod]
	public void SafeFileName_Roundtrips_Through_Decoding()
	{
		foreach (string key in CollidingKeys.Concat(["plain", "with space", "unicode-日本語", "pct%sign", "dot.in.middle"]))
		{
			string encoded = PersistenceProviderUtilities.GetSafeFileName(key);

			Assert.AreEqual(key, PersistenceProviderUtilities.GetKeyFromFileName(encoded), $"Key '{key}' should decode back exactly");
		}
	}

	[TestMethod]
	public void SafeFileName_Escapes_Windows_Reserved_Device_Names()
	{
		foreach (string reserved in new[] { "CON", "PRN", "AUX", "NUL", "COM1", "LPT9", "con", "NuL" })
		{
			string encoded = PersistenceProviderUtilities.GetSafeFileName(reserved);

			Assert.AreNotEqual(reserved, encoded, $"'{reserved}' is a reserved device name and must be escaped");
			Assert.AreEqual(reserved, PersistenceProviderUtilities.GetKeyFromFileName(encoded), "Escaping must stay reversible");
		}
	}

	[TestMethod]
	public void SafeFileName_Escapes_Trailing_Dot_And_Space()
	{
		foreach (string key in new[] { "trailing.", "trailing " })
		{
			string encoded = PersistenceProviderUtilities.GetSafeFileName(key);

			Assert.IsFalse(encoded[^1] is '.' or ' ', $"'{key}' must not produce a name Windows rejects");
			Assert.AreEqual(key, PersistenceProviderUtilities.GetKeyFromFileName(encoded), "Escaping must stay reversible");
		}
	}

	[TestMethod]
	public void SafeFileName_Bounds_Length_And_Stays_Distinct()
	{
		string first = PersistenceProviderUtilities.GetSafeFileName(new string('k', 5000) + "one");
		string second = PersistenceProviderUtilities.GetSafeFileName(new string('k', 5000) + "two");

		Assert.IsLessThanOrEqualTo(100, first.Length, "Long keys must be bounded to keep paths within platform limits");
		Assert.AreNotEqual(first, second, "Truncated keys must remain distinct");
		Assert.IsNull(PersistenceProviderUtilities.GetKeyFromFileName(first), "Truncated names are not recoverable and must report so");
	}

	[TestMethod]
	public void SafeFileName_Rejects_Empty_Keys()
	{
		Assert.ThrowsExactly<ArgumentException>(() => PersistenceProviderUtilities.GetSafeFileName(""));
		Assert.ThrowsExactly<ArgumentException>(() => PersistenceProviderUtilities.GetSafeFileName("   "));
	}

	[TestMethod]
	public void TryConvertToKey_Reports_Failure_Instead_Of_Default()
	{
		Assert.IsFalse(PersistenceProviderUtilities.TryConvertToKey("not-a-number", out int _), "Unparseable input should fail, not yield 0");
		Assert.IsFalse(PersistenceProviderUtilities.TryConvertToKey("not-a-guid", out Guid _), "Unparseable input should fail, not yield Guid.Empty");

		Assert.IsTrue(PersistenceProviderUtilities.TryConvertToKey("42", out int parsed));
		Assert.AreEqual(42, parsed);
	}

	[TestMethod]
	public async Task Colliding_Keys_Do_Not_Overwrite_Each_Other()
	{
		string dir = Path.Combine(Path.GetTempPath(), "NamingTests_" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			NativeFileSystemProvider fs = new();
			JsonSerializationProvider serializer = new();
			FileSystemPersistenceProvider<string> persistence = new(fs, serializer, dir);

			foreach (string key in CollidingKeys)
			{
				await persistence.StoreAsync(key, key, TestContext.CancellationToken).ConfigureAwait(false);
			}

			foreach (string key in CollidingKeys)
			{
				string? value = await persistence.RetrieveAsync<string>(key, TestContext.CancellationToken).ConfigureAwait(false);
				Assert.AreEqual(key, value, $"Key '{key}' should retain its own value");
			}

			string[] keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			Assert.AreEqual(CollidingKeys.Length, keys.Length, "Every distinct key should be stored separately");
			CollectionAssert.AreEquivalent(CollidingKeys, keys, "GetAllKeys should report the original keys, not their encoded names");
		}
		finally
		{
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}
		}
	}

	[TestMethod]
	public void Serializers_Report_Their_Own_Extension()
	{
		Assert.AreEqual(".json", new JsonSerializationProvider().FileExtension);
		Assert.AreEqual(".yaml", new YamlSerializationProvider().FileExtension);
		Assert.AreEqual(".toml", new TomlSerializationProvider().FileExtension);
	}

	[TestMethod]
	public async Task Files_Are_Named_For_The_Configured_Serializer()
	{
		string dir = Path.Combine(Path.GetTempPath(), "NamingTests_" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			NativeFileSystemProvider fs = new();
			YamlSerializationProvider serializer = new();
			FileSystemPersistenceProvider<string> persistence = new(fs, serializer, dir);

			await persistence.StoreAsync("settings", "value", TestContext.CancellationToken).ConfigureAwait(false);

			string[] written = Directory.GetFiles(dir);
			Assert.HasCount(1, written, "One file should have been written");
			Assert.EndsWith(".yaml", written[0], "A YAML serializer must not produce a .json file");

			// The round-trip must still work through the new extension.
			string? loaded = await persistence.RetrieveAsync<string>("settings", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.AreEqual("value", loaded);

			string[] keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			CollectionAssert.AreEquivalent(SingleSettingsKey, keys, "Key enumeration should match the serializer's extension");
		}
		finally
		{
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}
		}
	}
}
