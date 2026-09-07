// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

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
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	/// <remarks>Composes over the synchronous primitive, so overriding that converts this too.</remarks>
	public CommandResult Execute(string command, string? workingDirectory = null, CancellationToken cancellationToken = default) =>
		Execute(command, null, workingDirectory, cancellationToken);

	/// <summary>
	/// Executes a command synchronously with custom environment variables and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="environmentVariables">Optional environment variables to set for the command. If null, inherits the current environment.</param>
	/// <param name="workingDirectory">The optional working directory for the command. If null, uses the current directory.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	/// <remarks>
	/// This is the synchronous primitive: every other synchronous member composes over it. The default
	/// implementation bridges to <see cref="ExecuteAsync(string, IReadOnlyDictionary{string, string}?, string?, CancellationToken)"/>
	/// and therefore blocks a thread for the lifetime of the child process. A provider that can run a
	/// process synchronously should declare this member itself, which replaces the default and converts
	/// every synchronous overload at once.
	/// <para>
	/// The bridge uses <c>GetAwaiter().GetResult()</c> rather than <c>.Result</c> so a failure surfaces as
	/// the original exception with its stack trace intact, not wrapped in an <see cref="AggregateException"/>.
	/// </para>
	/// </remarks>
	public CommandResult Execute(string command, IReadOnlyDictionary<string, string>? environmentVariables, string? workingDirectory = null, CancellationToken cancellationToken = default) =>
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits: the bridge is the documented fallback when a provider declares no synchronous primitive.
		ExecuteAsync(command, environmentVariables, workingDirectory, cancellationToken).GetAwaiter().GetResult();
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
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>The standard output of the command.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the command exits with a non-zero exit code.</exception>
	/// <remarks>Composes over the synchronous primitive, so the failure surfaces as <see cref="InvalidOperationException"/> rather than wrapped in an <see cref="AggregateException"/>.</remarks>
	public string ExecuteAndGetOutput(string command, string? workingDirectory = null, CancellationToken cancellationToken = default)
	{
		CommandResult result = Execute(command, workingDirectory, cancellationToken);
		if (!result.Success)
		{
			throw new InvalidOperationException($"Command failed with exit code {result.ExitCode}: {result.StandardError}");
		}

		return result.StandardOutput;
	}
}
