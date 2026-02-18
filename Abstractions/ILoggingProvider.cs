// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;

/// <summary>
/// Defines the severity levels for log messages.
/// </summary>
public enum LogLevel
{
	/// <summary>
	/// Logs that contain the most detailed messages. These messages may contain sensitive application data.
	/// These messages are disabled by default and should never be enabled in a production environment.
	/// </summary>
	Trace = 0,

	/// <summary>
	/// Logs that are used for interactive investigation during development. These logs should primarily contain
	/// information useful for debugging and have no long-term value.
	/// </summary>
	Debug = 1,

	/// <summary>
	/// Logs that track the general flow of the application. These logs should have long-term value.
	/// </summary>
	Information = 2,

	/// <summary>
	/// Logs that highlight an abnormal or unexpected event in the application flow,
	/// but do not otherwise cause the application execution to stop.
	/// </summary>
	Warning = 3,

	/// <summary>
	/// Logs that highlight when the current flow of execution is stopped due to a failure.
	/// These should indicate a failure in the current activity, not an application-wide failure.
	/// </summary>
	Error = 4,

	/// <summary>
	/// Logs that describe an unrecoverable application or system crash,
	/// or a catastrophic failure that requires immediate attention.
	/// </summary>
	Critical = 5,

	/// <summary>
	/// Not used for writing log messages. Specifies that a logging category should not write any messages.
	/// </summary>
	None = 6,
}

/// <summary>
/// Interface for logging providers that can write structured log messages at various severity levels.
/// </summary>
public interface ILoggingProvider
{
	/// <summary>
	/// Writes a log entry at the specified level.
	/// </summary>
	/// <param name="level">The severity level of the log entry.</param>
	/// <param name="message">The log message.</param>
	public void Log(LogLevel level, string message);

	/// <summary>
	/// Writes a log entry at the specified level with an associated exception.
	/// </summary>
	/// <param name="level">The severity level of the log entry.</param>
	/// <param name="exception">The exception associated with this log entry.</param>
	/// <param name="message">The log message.</param>
	public void Log(LogLevel level, Exception exception, string message);

	/// <summary>
	/// Checks if the given log level is enabled.
	/// </summary>
	/// <param name="level">The log level to check.</param>
	/// <returns>True if the log level is enabled, false otherwise.</returns>
	public bool IsEnabled(LogLevel level);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Trace"/> level.
	/// </summary>
	/// <param name="message">The log message.</param>
	public void LogTrace(string message) => Log(LogLevel.Trace, message);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Debug"/> level.
	/// </summary>
	/// <param name="message">The log message.</param>
	public void LogDebug(string message) => Log(LogLevel.Debug, message);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Information"/> level.
	/// </summary>
	/// <param name="message">The log message.</param>
	public void LogInformation(string message) => Log(LogLevel.Information, message);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Warning"/> level.
	/// </summary>
	/// <param name="message">The log message.</param>
	public void LogWarning(string message) => Log(LogLevel.Warning, message);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Error"/> level.
	/// </summary>
	/// <param name="message">The log message.</param>
	public void LogError(string message) => Log(LogLevel.Error, message);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Error"/> level with an associated exception.
	/// </summary>
	/// <param name="exception">The exception associated with this log entry.</param>
	/// <param name="message">The log message.</param>
	public void LogError(Exception exception, string message) => Log(LogLevel.Error, exception, message);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Critical"/> level.
	/// </summary>
	/// <param name="message">The log message.</param>
	public void LogCritical(string message) => Log(LogLevel.Critical, message);

	/// <summary>
	/// Writes a log entry at the <see cref="LogLevel.Critical"/> level with an associated exception.
	/// </summary>
	/// <param name="exception">The exception associated with this log entry.</param>
	/// <param name="message">The log message.</param>
	public void LogCritical(Exception exception, string message) => Log(LogLevel.Critical, exception, message);
}
