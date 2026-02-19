// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.LoggingProviders;

using System;
using ktsu.Abstractions;

/// <summary>
/// A logging provider that writes log messages to the console.
/// Messages at Warning level and above are written to standard error; others to standard output.
/// </summary>
public class Console : ILoggingProvider
{
	/// <summary>
	/// Gets or sets the minimum log level. Messages below this level will be ignored.
	/// </summary>
	public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

	/// <summary>
	/// Writes a log entry at the specified level.
	/// </summary>
	/// <param name="level">The severity level of the log entry.</param>
	/// <param name="message">The log message.</param>
	public void Log(LogLevel level, string message)
	{
		if (!IsEnabled(level))
		{
			return;
		}

		string formatted = FormatMessage(level, message);

		if (level >= LogLevel.Warning)
		{
			System.Console.Error.WriteLine(formatted);
		}
		else
		{
			System.Console.Out.WriteLine(formatted);
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
		if (!IsEnabled(level))
		{
			return;
		}

		string formatted = $"{FormatMessage(level, message)}{Environment.NewLine}{exception}";

		if (level >= LogLevel.Warning)
		{
			System.Console.Error.WriteLine(formatted);
		}
		else
		{
			System.Console.Out.WriteLine(formatted);
		}
	}

	/// <summary>
	/// Checks if the given log level is enabled.
	/// </summary>
	/// <param name="level">The log level to check.</param>
	/// <returns>True if the log level is enabled, false otherwise.</returns>
	public bool IsEnabled(LogLevel level) => level >= MinimumLevel && level != LogLevel.None;

	private static string FormatMessage(LogLevel level, string message)
	{
		string levelString = level switch
		{
			LogLevel.Trace => "TRACE",
			LogLevel.Debug => "DEBUG",
			LogLevel.Information => "INFO",
			LogLevel.Warning => "WARN",
			LogLevel.Error => "ERROR",
			LogLevel.Critical => "CRIT",
			_ => level.ToString().ToUpperInvariant(),
		};

		return $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{levelString}] {message}";
	}
}
