// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CommandExecutors.Native;

using ktsu.Essentials;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
	/// How long an abandoned reader thread is given to notice its stream has closed before the process
	/// is disposed out from under it. Reaching the timeout costs nothing: the threads are background
	/// threads, and whatever they read is discarded along with the rest of the cancelled result.
	/// </summary>
	private const int DrainAbandonTimeoutMilliseconds = 1000;

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
	/// Each redirected stream is drained to the end by its own dedicated thread, so neither can fill its
	/// buffer while the other is being read, and the calling thread stays free to poll
	/// <paramref name="cancellationToken"/>. The streams are read raw, so the captured output is
	/// byte-for-byte what the child wrote — the same text
	/// <see cref="ExecuteAsync(string, IReadOnlyDictionary{string, string}?, string?, CancellationToken)"/>
	/// returns for the same command.
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

			process.Start();

			StreamDrain stdout = StreamDrain.Start(process.StandardOutput);
			StreamDrain stderr = StreamDrain.Start(process.StandardError);

			try
			{
				while (!process.WaitForExit(CancellationPollIntervalMilliseconds))
				{
					if (cancellationToken.IsCancellationRequested)
					{
						TryKill(process);
						return Cancelled();
					}
				}

				return new CommandResult(process.ExitCode, stdout.Wait(), stderr.Wait());
			}
			finally
			{
				// The cancelled path abandons the drains rather than waiting for them. They still have to
				// stop touching the streams before the `using` above disposes the process, so they are
				// given a bounded chance to notice the child is gone. On the path that returns a result
				// both have already been waited for, and these joins return immediately.
				stdout.Abandon();
				stderr.Abandon();
			}
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

	/// <summary>
	/// Reads one redirected stream to the end on a thread of its own.
	/// </summary>
	/// <remarks>
	/// Reading both streams on the calling thread deadlocks as soon as the one not being read fills its
	/// buffer, and reading either of them there would block the cancellation poll for as long as the child
	/// stays silent. A thread per stream avoids both, and unlike the line-splitting
	/// <see cref="Process.OutputDataReceived"/> callbacks it preserves the child's own line terminators and
	/// its choice not to write a final one.
	/// <para>
	/// The thread is a dedicated one rather than a pooled one, because the pool thread it would otherwise
	/// borrow would be held for the lifetime of the child process — the cost the synchronous primitive
	/// exists to avoid.
	/// </para>
	/// </remarks>
	private sealed class StreamDrain
	{
		/// <summary>
		/// The thread reading the stream. Joining it publishes <see cref="text"/> to the caller.
		/// </summary>
		private readonly Thread thread;

		/// <summary>
		/// What was read. Written only by <see cref="thread"/>, and read only after it has been joined.
		/// </summary>
		private string text = string.Empty;

		/// <summary>
		/// Initializes a new instance of the <see cref="StreamDrain"/> class and starts reading.
		/// </summary>
		/// <param name="reader">The redirected stream to read to the end.</param>
		private StreamDrain(StreamReader reader)
		{
			thread = new Thread(() =>
			{
				try
				{
					text = reader.ReadToEnd();
				}
				catch (Exception ex) when (ex is IOException or ObjectDisposedException)
				{
					// The child was killed, or the process disposed, part way through the stream. This only
					// happens on the cancelled path, which reports no output at all.
				}
			})
			{
				IsBackground = true,
				Name = "ktsu.Essentials command output",
			};

			thread.Start();
		}

		/// <summary>
		/// Starts draining <paramref name="reader"/> on a new thread.
		/// </summary>
		/// <param name="reader">The redirected stream to read to the end.</param>
		/// <returns>The drain, which <see cref="Wait"/> collects the output from.</returns>
		public static StreamDrain Start(StreamReader reader) => new(reader);

		/// <summary>
		/// Waits for the stream to reach its end and returns everything it carried.
		/// </summary>
		/// <returns>The stream's contents, exactly as the child wrote them.</returns>
		public string Wait()
		{
			thread.Join();
			return text;
		}

		/// <summary>
		/// Gives an abandoned drain a bounded chance to finish, so it is not still reading a stream whose
		/// process is about to be disposed. Whatever it read is discarded.
		/// </summary>
		public void Abandon() => thread.Join(DrainAbandonTimeoutMilliseconds);
	}
}
