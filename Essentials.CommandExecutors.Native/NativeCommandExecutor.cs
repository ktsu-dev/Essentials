// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CommandExecutors.Native;

using ktsu.Essentials;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A command executor that uses the native operating system shell to execute commands.
/// </summary>
public class NativeCommandExecutor : ICommandExecutor
{
	/// <summary>
	/// How long the synchronous wait blocks before checking the cancellation token again.
	/// </summary>
	private const int CancellationPollIntervalMilliseconds = 50;

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
			return Cancelled();
		}

		try
		{
			using Process process = new();
			process.StartInfo = CreateStartInfo(command, environmentVariables, workingDirectory);

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
			return Cancelled();
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

	/// <summary>
	/// Executes a command synchronously with custom environment variables and returns the result.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="environmentVariables">Optional environment variables to set for the command.</param>
	/// <param name="workingDirectory">The optional working directory for the command.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="CommandResult"/> containing the exit code, standard output, and standard error.</returns>
	/// <remarks>
	/// Declaring the synchronous primitive replaces <see cref="ICommandExecutor"/>'s default, which bridges
	/// to the asynchronous path and blocks a thread-pool thread for the lifetime of the child process. This
	/// implementation drives <see cref="Process"/> synchronously instead, so no thread is borrowed from the
	/// pool and no <see cref="Task"/> is awaited.
	/// <para>
	/// Output is captured through the asynchronous read handlers rather than
	/// <see cref="System.IO.StreamReader.ReadToEnd"/>, because reading one redirected stream to the end while
	/// the other fills its buffer deadlocks.
	/// </para>
	/// </remarks>
	public CommandResult Execute(string command, IReadOnlyDictionary<string, string>? environmentVariables, string? workingDirectory = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(command);

		if (cancellationToken.IsCancellationRequested)
		{
			return Cancelled();
		}

		try
		{
			using Process process = new();
			process.StartInfo = CreateStartInfo(command, environmentVariables, workingDirectory);

			StringBuilder stdout = new();
			StringBuilder stderr = new();
			process.OutputDataReceived += (_, e) => AppendLine(stdout, e.Data);
			process.ErrorDataReceived += (_, e) => AppendLine(stderr, e.Data);

			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			while (!process.WaitForExit(CancellationPollIntervalMilliseconds))
			{
				if (cancellationToken.IsCancellationRequested)
				{
					TryKill(process);
					return Cancelled();
				}
			}

			// WaitForExit(int) can return before the asynchronous read handlers have drained the streams.
			// The parameterless overload waits for them, and is documented as the way to flush them.
			process.WaitForExit();

			return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
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

	/// <summary>
	/// Builds the <see cref="ProcessStartInfo"/> that runs <paramref name="command"/> through the platform shell.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="environmentVariables">Optional environment variables to set for the command.</param>
	/// <param name="workingDirectory">The optional working directory for the command.</param>
	/// <returns>The configured start info.</returns>
	private static ProcessStartInfo CreateStartInfo(string command, IReadOnlyDictionary<string, string>? environmentVariables, string? workingDirectory)
	{
		bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		ProcessStartInfo startInfo = new()
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
				startInfo.Environment[kvp.Key] = kvp.Value;
			}
		}

		return startInfo;
	}

	/// <summary>
	/// Appends one line of redirected output, ignoring the null that signals end of stream.
	/// </summary>
	/// <param name="buffer">The buffer to append to. Only the one handler thread for that stream writes to it, and the parameterless <see cref="Process.WaitForExit()"/> publishes those writes to the caller.</param>
	/// <param name="line">The line received, or null at end of stream.</param>
	private static void AppendLine(StringBuilder buffer, string? line)
	{
		if (line is not null)
		{
			buffer.AppendLine(line);
		}
	}

	/// <summary>
	/// Kills a process that is being abandoned because the operation was cancelled, ignoring the races
	/// where it has already exited or was never started.
	/// </summary>
	/// <param name="process">The process to kill.</param>
	private static void TryKill(Process process)
	{
		try
		{
			process.Kill();
		}
		catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
		{
			// The process exited between the wait timing out and this call, or could not be terminated.
		}
	}

	/// <summary>
	/// Builds the result returned when an operation is cancelled.
	/// </summary>
	/// <returns>A failed <see cref="CommandResult"/> describing the cancellation.</returns>
	private static CommandResult Cancelled() =>
		new(-1, string.Empty, "Operation was cancelled.");
}
