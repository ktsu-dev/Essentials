// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Globalization;
using ktsu.Essentials;
using ktsu.Essentials.LoggingProviders.Console;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ConsoleLoggingProviderTests
{
	[TestMethod]
	public void FormatMessage_CultureWithDotTimeSeparator_StillWritesColons()
	{
		// fi-FI's time separator. The ktsu.Sdk builds this test project with InvariantGlobalization, so named
		// cultures such as fi-FI or th-TH can't be loaded here; a modified clone of the invariant culture can.
		CultureInfo dotSeparated = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		dotSeparated.DateTimeFormat.TimeSeparator = ".";

		AssertTimestampIsInvariantUtc(FormatUnder(dotSeparated));
	}

	private static string FormatUnder(CultureInfo culture)
	{
		CultureInfo original = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = culture;
		try
		{
			return ConsoleLoggingProvider.FormatMessage(LogLevel.Information, "message");
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	private static void AssertTimestampIsInvariantUtc(string line)
	{
		// "[yyyy-MM-dd HH:mm:ss.fff] [INFO] message"
		string stamp = line.Substring(1, 23);
		bool parsed = DateTime.TryParseExact(stamp, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture,
			DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime timestamp);

		Assert.IsTrue(parsed, $"'{stamp}' is not an invariant yyyy-MM-dd HH:mm:ss.fff timestamp");
		Assert.IsLessThan(TimeSpan.FromMinutes(5), (DateTime.UtcNow - timestamp).Duration(), $"'{stamp}' is not the current UTC time");
		Assert.AreEqual("] [INFO] message", line[24..]);
	}
}
