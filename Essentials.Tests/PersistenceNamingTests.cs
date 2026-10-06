// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.Essentials.PersistenceProviders.FileSystem;
using ktsu.Essentials.PersistenceProviders.Temp;
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
		foreach (string key in CollidingKeys.Concat(["plain", "with space", "unicode-日本語", "pct%sign", "dot.in.middle", "backup~1", "~draft", "a~b"]))
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
	public async Task Keys_Containing_A_Tilde_Are_Listed()
	{
		string[] tildeKeys = ["a~b", "backup~1", "~draft"];
		string dir = Directory.CreateTempSubdirectory("NamingTests_").FullName;
		try
		{
			FileSystemPersistenceProvider<string> persistence = new(new NativeFileSystemProvider(), new JsonSerializationProvider(), dir);

			foreach (string key in tildeKeys)
			{
				await persistence.StoreAsync(key, key, TestContext.CancellationToken).ConfigureAwait(false);
			}

			string[] keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			CollectionAssert.AreEquivalent(tildeKeys, keys, "Keys that contain '~' but were never truncated must be listed");
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

	private static readonly double[] CultureSensitiveDoubleKeys = [1.5, 0.1, -2.25e-7, 1234567.875];

	private static readonly DateTime[] CultureSensitiveDateKeys =
	[
		new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc),
		new DateTime(2026, 12, 31, 23, 59, 59, 123, DateTimeKind.Unspecified).AddTicks(4567),
	];

	[TestMethod]
	public void FormatKey_Ignores_The_Current_Culture()
	{
		CultureInfo original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CommaDecimalDayFirstCulture();

			Assert.AreEqual("1.5", PersistenceProviderUtilities.FormatKey(1.5));
			Assert.AreEqual("2026-03-04T05:06:07.0000000Z", PersistenceProviderUtilities.FormatKey(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)));
			Assert.AreEqual("a,b", PersistenceProviderUtilities.FormatKey("a,b"));
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	[TestMethod]
	public void FormatKey_Round_Trips_Every_Key_Kind_Through_TryConvertToKey()
	{
		CultureInfo original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CommaDecimalDayFirstCulture();

			AssertRoundTrips(0.1f, "0.1");
			AssertRoundTrips(12.5m, "12.5");
			AssertRoundTrips(-7L, "-7");
			AssertRoundTrips(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(10)), "2026-03-04T05:06:07.0000000+10:00");

			DateTime utc = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
			Assert.IsTrue(PersistenceProviderUtilities.TryConvertToKey(PersistenceProviderUtilities.FormatKey(utc), out DateTime parsedUtc));
			Assert.AreEqual(DateTimeKind.Utc, parsedUtc.Kind, "A UTC key should not come back as local time");
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}

		Assert.AreEqual("plain", PersistenceProviderUtilities.FormatKey(new NonFormattableKey("plain")), "A key that is not IFormattable falls back to ToString");
		Assert.IsFalse(PersistenceProviderUtilities.TryConvertToKey("not-a-date", out DateTime _), "Unparseable input should fail, not yield DateTime.MinValue");
		Assert.IsFalse(PersistenceProviderUtilities.TryConvertToKey("not-a-date", out DateTimeOffset _), "Unparseable input should fail, not yield DateTimeOffset.MinValue");
	}

	private static void AssertRoundTrips<TKey>(TKey key, string expectedText) where TKey : notnull
	{
		string text = PersistenceProviderUtilities.FormatKey(key);
		Assert.AreEqual(expectedText, text);
		Assert.IsTrue(PersistenceProviderUtilities.TryConvertToKey(text, out TKey parsed), $"'{text}' should parse back");
		Assert.AreEqual(key, parsed);
	}

	private sealed record NonFormattableKey(string Name)
	{
		public override string ToString() => Name;
	}

	[TestMethod]
	public async Task FileSystem_Double_And_Date_Keys_Round_Trip_Across_Cultures()
	{
		string dir = Path.Join(Path.GetTempPath(), "NamingTests_" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			NativeFileSystemProvider fs = new();
			JsonSerializationProvider serializer = new();
			await AssertKeysRoundTripAcrossCultures(new FileSystemPersistenceProvider<double>(fs, serializer, dir), CultureSensitiveDoubleKeys).ConfigureAwait(false);
			await AssertKeysRoundTripAcrossCultures(new FileSystemPersistenceProvider<DateTime>(fs, serializer, Path.Join(dir, "dates")), CultureSensitiveDateKeys).ConfigureAwait(false);
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
	public async Task Temp_Double_And_Date_Keys_Round_Trip_Across_Cultures()
	{
		NativeFileSystemProvider fs = new();
		JsonSerializationProvider serializer = new();

		using TempPersistenceProvider<double> doubles = new(fs, serializer, "NamingTests");
		await AssertKeysRoundTripAcrossCultures(doubles, CultureSensitiveDoubleKeys).ConfigureAwait(false);

		using TempPersistenceProvider<DateTime> dates = new(fs, serializer, "NamingTests");
		await AssertKeysRoundTripAcrossCultures(dates, CultureSensitiveDateKeys).ConfigureAwait(false);
	}

	/// <summary>
	/// Stores each key under a culture whose decimal separator and date order differ from the invariant
	/// culture, then checks that enumeration lists exactly those keys and that each can still be
	/// retrieved after the process switches to a culture that formats both differently again.
	/// </summary>
	private async Task AssertKeysRoundTripAcrossCultures<TKey>(IPersistenceProvider<TKey> persistence, TKey[] keys) where TKey : notnull
	{
		CultureInfo original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CommaDecimalDayFirstCulture();
			for (int i = 0; i < keys.Length; i++)
			{
				await persistence.StoreAsync(keys[i], $"value {i}", TestContext.CancellationToken).ConfigureAwait(false);
			}

			TKey[] listed = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			CollectionAssert.AreEquivalent(keys, listed, "GetAllKeys should list the stored keys, not culture-mangled ones");

			CultureInfo.CurrentCulture = MonthFirstTwelveHourCulture();
			for (int i = 0; i < keys.Length; i++)
			{
				string? value = await persistence.RetrieveAsync<string>(keys[i], TestContext.CancellationToken).ConfigureAwait(false);
				Assert.AreEqual($"value {i}", value, $"Key '{keys[i]}' stored under de-DE should be found under another culture");
			}
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	/// <summary>
	/// A culture formatted like de-DE: a comma decimal separator and day-first dates. It is built from
	/// the invariant culture so the tests do not depend on the machine's ICU data.
	/// </summary>
	private static CultureInfo CommaDecimalDayFirstCulture()
	{
		CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		culture.NumberFormat.NumberDecimalSeparator = ",";
		culture.NumberFormat.NumberGroupSeparator = ".";
		culture.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
		culture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
		return culture;
	}

	/// <summary>A culture formatted like en-US: month-first dates and a twelve-hour clock.</summary>
	private static CultureInfo MonthFirstTwelveHourCulture()
	{
		CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		culture.DateTimeFormat.ShortDatePattern = "M/d/yyyy";
		culture.DateTimeFormat.LongTimePattern = "h:mm:ss tt";
		return culture;
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

	[Flags]
	private enum SlotKey
	{
		None = 0,
		Primary = 1,
		Secondary = 2,
	}

	private static readonly SlotKey[] EnumKeys = [SlotKey.Primary, SlotKey.Primary | SlotKey.Secondary, (SlotKey)8];

	private static readonly TimeSpan[] TimeSpanKeys = [TimeSpan.FromMinutes(5), TimeSpan.FromDays(-1.5), new TimeSpan(1234567)];

	[TestMethod]
	public void TryConvertToKey_Reads_Back_Keys_That_Are_Not_IConvertible()
	{
		AssertRoundTrips(SlotKey.Primary, "Primary");
		AssertRoundTrips(SlotKey.Primary | SlotKey.Secondary, "Primary, Secondary");
		AssertRoundTrips((SlotKey)8, "8");
		AssertRoundTrips(TimeSpan.FromMinutes(5), "00:05:00");
		AssertRoundTrips(new TimeSpan(1234567), "00:00:00.1234567");
		AssertRoundTrips(new Uri("https://example.com/a?b=c"), "https://example.com/a?b=c");

		Assert.IsFalse(PersistenceProviderUtilities.TryConvertToKey("Tertiary", out SlotKey _), "An unknown enum name should fail");
		Assert.IsFalse(PersistenceProviderUtilities.TryConvertToKey("primary", out SlotKey _), "Enum names are written in their declared case, so parsing stays case-sensitive");
		Assert.IsFalse(PersistenceProviderUtilities.TryConvertToKey("not-a-timespan", out TimeSpan _), "Unparseable input should fail, not yield TimeSpan.Zero");
	}

	[TestMethod]
	public async Task FileSystem_Enum_And_TimeSpan_Keys_Are_Listed()
	{
		string dir = Path.Join(Path.GetTempPath(), "NamingTests_" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			NativeFileSystemProvider fs = new();
			JsonSerializationProvider serializer = new();
			await AssertKeysRoundTripAcrossCultures(new FileSystemPersistenceProvider<SlotKey>(fs, serializer, dir), EnumKeys).ConfigureAwait(false);
			await AssertKeysRoundTripAcrossCultures(new FileSystemPersistenceProvider<TimeSpan>(fs, serializer, Path.Join(dir, "spans")), TimeSpanKeys).ConfigureAwait(false);
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
	public async Task Temp_Enum_And_TimeSpan_Keys_Are_Listed()
	{
		NativeFileSystemProvider fs = new();
		JsonSerializationProvider serializer = new();

		using TempPersistenceProvider<SlotKey> slots = new(fs, serializer, "NamingTests");
		await AssertKeysRoundTripAcrossCultures(slots, EnumKeys).ConfigureAwait(false);

		using TempPersistenceProvider<TimeSpan> spans = new(fs, serializer, "NamingTests");
		await AssertKeysRoundTripAcrossCultures(spans, TimeSpanKeys).ConfigureAwait(false);
	}
}
