// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents the result of executing a command, including exit code, standard output, and standard error.
/// </summary>
/// <param name="exitCode">The exit code of the command.</param>
/// <param name="standardOutput">The standard output produced by the command.</param>
/// <param name="standardError">The standard error output produced by the command.</param>
public class CommandResult(int exitCode, string standardOutput, string standardError)
{
	/// <summary>
	/// Gets the exit code of the command. A value of 0 typically indicates success.
	/// </summary>
	public int ExitCode { get; } = exitCode;

	/// <summary>
	/// Gets the standard output produced by the command.
	/// </summary>
	public string StandardOutput { get; } = Ensure.NotNull(standardOutput);

	/// <summary>
	/// Gets the standard error output produced by the command.
	/// </summary>
	public string StandardError { get; } = Ensure.NotNull(standardError);

	/// <summary>
	/// Gets a value indicating whether the command executed successfully (exit code is 0).
	/// </summary>
	public bool Success => ExitCode == 0;
}

/// <summary>
/// Interface for command executors that can run shell commands and capture their output.
/// This provides a unified abstraction for executing external processes.
/// </summary>
public interface ICommandExecutor
{
	/// <summary>
	/// Executes a command asynchronously and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="workingDirectory">The optional working directory for the command. If null, uses the current directory.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	public Task<CommandResult> ExecuteAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Executes a command asynchronously with custom environment variables and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="environmentVariables">Optional environment variables to set for the command. If null, inherits the current environment.</param>
	/// <param name="workingDirectory">The optional working directory for the command. If null, uses the current directory.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	public Task<CommandResult> ExecuteAsync(string command, IReadOnlyDictionary<string, string>? environmentVariables, string? workingDirectory = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Executes a command synchronously and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="workingDirectory">The optional working directory for the command. If null, uses the current directory.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	public CommandResult Execute(string command, string? workingDirectory = null) =>
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
		ExecuteAsync(command, workingDirectory, CancellationToken.None).Result;
#pragma warning restore VSTHRD002

	/// <summary>
	/// Executes a command synchronously with custom environment variables and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="environmentVariables">Optional environment variables to set for the command. If null, inherits the current environment.</param>
	/// <param name="workingDirectory">The optional working directory for the command. If null, uses the current directory.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	public CommandResult Execute(string command, IReadOnlyDictionary<string, string>? environmentVariables, string? workingDirectory = null) =>
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
		ExecuteAsync(command, environmentVariables, workingDirectory, CancellationToken.None).Result;
#pragma warning restore VSTHRD002

	/// <summary>
	/// Executes a command asynchronously and returns just the standard output, throwing if the command fails.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="workingDirectory">The optional working directory for the command. If null, uses the current directory.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>The standard output of the command.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the command exits with a non-zero exit code.</exception>
	public async Task<string> ExecuteAndGetOutputAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default)
	{
		CommandResult result = await ExecuteAsync(command, workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success)
		{
			throw new InvalidOperationException($"Command failed with exit code {result.ExitCode}: {result.StandardError}");
		}

		return result.StandardOutput;
	}

	/// <summary>
	/// Executes a command synchronously and returns just the standard output, throwing if the command fails.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="workingDirectory">The optional working directory for the command. If null, uses the current directory.</param>
	/// <returns>The standard output of the command.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the command exits with a non-zero exit code.</exception>
	public string ExecuteAndGetOutput(string command, string? workingDirectory = null) =>
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
		ExecuteAndGetOutputAsync(command, workingDirectory, CancellationToken.None).Result;
#pragma warning restore VSTHRD002
}
