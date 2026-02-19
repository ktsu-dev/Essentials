// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CommandExecutors;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Abstractions;

/// <summary>
/// A command executor that uses the native operating system shell to execute commands.
/// </summary>
public class Native : ICommandExecutor
{
	/// <summary>
	/// Executes a command asynchronously and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="workingDirectory">The optional working directory for the command.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	public Task<CommandResult> ExecuteAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default) =>
		ExecuteAsync(command, null, workingDirectory, cancellationToken);

	/// <summary>
	/// Executes a command asynchronously with custom environment variables and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="environmentVariables">Optional environment variables to set for the command.</param>
	/// <param name="workingDirectory">The optional working directory for the command.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	public async Task<CommandResult> ExecuteAsync(string command, IReadOnlyDictionary<string, string>? environmentVariables, string? workingDirectory = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(command);

		if (cancellationToken.IsCancellationRequested)
		{
			return new CommandResult(-1, string.Empty, "Operation was cancelled.");
		}

		try
		{
			bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

			using Process process = new();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = isWindows ? "cmd.exe" : "/bin/sh",
				Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
				WorkingDirectory = workingDirectory ?? string.Empty,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			if (environmentVariables is not null)
			{
				foreach (KeyValuePair<string, string> kvp in environmentVariables)
				{
					process.StartInfo.Environment[kvp.Key] = kvp.Value;
				}
			}

			process.Start();

#if NET7_0_OR_GREATER
			Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
			Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
#else
			Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
			Task<string> stderrTask = process.StandardError.ReadToEndAsync();
#endif

#if NET5_0_OR_GREATER
			await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
#else
			await Task.Run(process.WaitForExit, cancellationToken).ConfigureAwait(false);
#endif

			string stdout = await stdoutTask.ConfigureAwait(false);
			string stderr = await stderrTask.ConfigureAwait(false);

			return new CommandResult(process.ExitCode, stdout, stderr);
		}
		catch (OperationCanceledException)
		{
			return new CommandResult(-1, string.Empty, "Operation was cancelled.");
		}
		catch (InvalidOperationException ex)
		{
			return new CommandResult(-1, string.Empty, ex.Message);
		}
		catch (System.ComponentModel.Win32Exception ex)
		{
			return new CommandResult(-1, string.Empty, ex.Message);
		}
	}
}
