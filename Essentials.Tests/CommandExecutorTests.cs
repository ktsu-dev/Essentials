// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CommandExecutorTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddCommon();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> CommandExecutors => BuildProvider().EnumerateProviders<ICommandExecutor>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Can_Execute_Simple_Command(ICommandExecutor executor, string providerName)
	{
		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "echo hello"
			: "echo hello";

		CommandResult result = executor.ExecuteAsync(command, cancellationToken: TestContext.CancellationToken).Result;
		Assert.IsTrue(result.Success, $"{providerName} should execute echo successfully (exit code: {result.ExitCode}, stderr: {result.StandardError})");
		Assert.IsTrue(result.StandardOutput.Trim().Contains("hello"), $"{providerName} should capture output");
	}

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Returns_NonZero_Exit_Code_For_Bad_Command(ICommandExecutor executor, string providerName)
	{
		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "cmd /c exit 42"
			: "exit 42";

		CommandResult result = executor.ExecuteAsync(command, cancellationToken: TestContext.CancellationToken).Result;
		Assert.IsFalse(result.Success, $"{providerName} should report failure for non-zero exit code");
	}

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Captures_Standard_Error(ICommandExecutor executor, string providerName)
	{
		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "echo error_output 1>&2"
			: "echo error_output >&2";

		CommandResult result = executor.ExecuteAsync(command, cancellationToken: TestContext.CancellationToken).Result;
		Assert.IsTrue(result.StandardError.Contains("error_output"), $"{providerName} should capture stderr");
	}

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Sync_Execute_Works(ICommandExecutor executor, string providerName)
	{
		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "echo sync"
			: "echo sync";

		CommandResult result = executor.Execute(command);
		Assert.IsTrue(result.Success, $"{providerName} sync should execute successfully");
		Assert.IsTrue(result.StandardOutput.Trim().Contains("sync"), $"{providerName} sync should capture output");
	}

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Sync_Execute_Captures_Standard_Error(ICommandExecutor executor, string providerName)
	{
		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "echo error_output 1>&2"
			: "echo error_output >&2";

		CommandResult result = executor.Execute(command);
		Assert.IsTrue(result.StandardError.Contains("error_output"), $"{providerName} sync should capture stderr");
	}

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Sync_Execute_With_Environment_Variables(ICommandExecutor executor, string providerName)
	{
		Dictionary<string, string> env = new()
		{
			["TEST_VAR_KTSU"] = "hello_from_sync"
		};

		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "echo %TEST_VAR_KTSU%"
			: "echo $TEST_VAR_KTSU";

		CommandResult result = executor.Execute(command, env);
		Assert.IsTrue(result.Success, $"{providerName} sync should execute with env vars");
		Assert.IsTrue(result.StandardOutput.Contains("hello_from_sync"), $"{providerName} sync should use custom env var");
	}

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Sync_Execute_Reports_Cancellation(ICommandExecutor executor, string providerName)
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();

		CommandResult result = executor.Execute("echo never", cancellationToken: cts.Token);
		Assert.IsFalse(result.Success, $"{providerName} sync should report failure when the token is already cancelled");
	}

	/// <summary>
	/// Regression test for issue #17: the synchronous overloads used to block on <c>Task&lt;T&gt;.Result</c>,
	/// which wraps whatever the operation threw in an <see cref="AggregateException"/>. A caller writing the
	/// obvious catch for the documented exception caught nothing.
	/// </summary>
	/// <param name="executor">The executor under test.</param>
	/// <param name="providerName">The name of the executor, for assertion messages.</param>
	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Sync_ExecuteAndGetOutput_Throws_Unwrapped_On_Failure(ICommandExecutor executor, string providerName)
	{
		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "cmd /c exit 42"
			: "exit 42";

		InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(
			() => executor.ExecuteAndGetOutput(command),
			$"{providerName} should surface the original exception, not an AggregateException wrapping it");

		Assert.IsNull(thrown.InnerException, $"{providerName} should not nest the failure inside another exception");
	}

	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_With_Environment_Variables(ICommandExecutor executor, string providerName)
	{
		Dictionary<string, string> env = new()
		{
			["TEST_VAR_KTSU"] = "hello_from_test"
		};

		string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "echo %TEST_VAR_KTSU%"
			: "echo $TEST_VAR_KTSU";

		CommandResult result = executor.ExecuteAsync(command, env, cancellationToken: TestContext.CancellationToken).Result;
		Assert.IsTrue(result.Success, $"{providerName} should execute with env vars");
		Assert.IsTrue(result.StandardOutput.Contains("hello_from_test"), $"{providerName} should use custom env var");
	}

	/// <summary>
	/// Tests that the success path of <see cref="ICommandExecutor.ExecuteAndGetOutput"/> returns the
	/// captured standard output rather than throwing.
	/// </summary>
	/// <param name="executor">The executor under test.</param>
	/// <param name="providerName">The name of the executor, for assertion messages.</param>
	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Sync_ExecuteAndGetOutput_Returns_Output(ICommandExecutor executor, string providerName)
	{
		string output = executor.ExecuteAndGetOutput("echo captured");

		Assert.IsTrue(output.Contains("captured"), $"{providerName} should return the command's standard output");
	}

	/// <summary>
	/// Tests that an already-cancelled token short-circuits the asynchronous path before a process is started.
	/// </summary>
	/// <param name="executor">The executor under test.</param>
	/// <param name="providerName">The name of the executor, for assertion messages.</param>
	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Async_Reports_Cancellation_Before_Start(ICommandExecutor executor, string providerName)
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();

		CommandResult result = executor.ExecuteAsync("echo never", cancellationToken: cts.Token).Result;

		Assert.IsFalse(result.Success, $"{providerName} should report failure when the token is already cancelled");
	}

	/// <summary>
	/// Tests that cancelling while a process is running abandons the wait instead of blocking until the
	/// child exits on its own.
	/// </summary>
	/// <param name="executor">The executor under test.</param>
	/// <param name="providerName">The name of the executor, for assertion messages.</param>
	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Async_Reports_Cancellation_While_Running(ICommandExecutor executor, string providerName)
	{
		using CancellationTokenSource cts = new(CancellationDelay);

		Stopwatch stopwatch = Stopwatch.StartNew();
		CommandResult result = executor.ExecuteAsync(SleepCommand(SleepSeconds), cancellationToken: cts.Token).Result;
		stopwatch.Stop();

		Assert.IsFalse(result.Success, $"{providerName} should report failure when cancelled mid-run");
		Assert.IsTrue(
			stopwatch.Elapsed < TimeSpan.FromSeconds(SleepSeconds),
			$"{providerName} should return on cancellation rather than waiting {SleepSeconds}s for the child (took {stopwatch.Elapsed})");
	}

	/// <summary>
	/// Tests that the synchronous primitive honours a token cancelled after the child process has started:
	/// it stops polling, kills the child and returns, rather than blocking until the child exits.
	/// </summary>
	/// <param name="executor">The executor under test.</param>
	/// <param name="providerName">The name of the executor, for assertion messages.</param>
	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Sync_Execute_Cancels_A_Running_Process(ICommandExecutor executor, string providerName)
	{
		using CancellationTokenSource cts = new(CancellationDelay);

		Stopwatch stopwatch = Stopwatch.StartNew();
		CommandResult result = executor.Execute(SleepCommand(SleepSeconds), cancellationToken: cts.Token);
		stopwatch.Stop();

		Assert.IsFalse(result.Success, $"{providerName} sync should report failure when cancelled mid-run");
		Assert.IsTrue(
			stopwatch.Elapsed < TimeSpan.FromSeconds(SleepSeconds),
			$"{providerName} sync should return on cancellation rather than waiting {SleepSeconds}s for the child (took {stopwatch.Elapsed})");
	}

	/// <summary>
	/// Tests that a working directory that does not exist is reported as a failed result rather than
	/// escaping as an exception from either path.
	/// </summary>
	/// <param name="executor">The executor under test.</param>
	/// <param name="providerName">The name of the executor, for assertion messages.</param>
	[TestMethod]
	[DynamicData(nameof(CommandExecutors))]
	public void CommandExecutor_Reports_Failure_For_Missing_Working_Directory(ICommandExecutor executor, string providerName)
	{
		string missing = Path.Join(Path.GetTempPath(), $"ktsu-missing-{Guid.NewGuid():N}");

		CommandResult asyncResult = executor.ExecuteAsync("echo never", missing, TestContext.CancellationToken).Result;
		CommandResult syncResult = executor.Execute("echo never", missing);

		Assert.IsFalse(asyncResult.Success, $"{providerName} should report failure for a missing working directory");
		Assert.IsFalse(syncResult.Success, $"{providerName} sync should report failure for a missing working directory");
		Assert.AreNotEqual(string.Empty, syncResult.StandardError, $"{providerName} sync should explain why the command could not start");
	}

	/// <summary>
	/// Tests <see cref="ICommandExecutor"/>'s own synchronous default, which an implementation that
	/// declares only the asynchronous members inherits. The native executor replaces it, so it is reached
	/// through an executor that does not.
	/// </summary>
	[TestMethod]
	public void CommandExecutor_Default_Sync_Primitive_Bridges_To_Async()
	{
		ICommandExecutor executor = new AsyncOnlyCommandExecutor();

		CommandResult result = executor.Execute("bridged");

		Assert.IsTrue(result.Success, "the default synchronous primitive should return the asynchronous result");
		Assert.AreEqual("bridged", result.StandardOutput, "the default synchronous primitive should not alter the result");
	}

	/// <summary>
	/// Regression test for issue #17 on the interface's own default. Blocking on <c>Task&lt;T&gt;.Result</c>
	/// wrapped the failure in an <see cref="AggregateException"/>; <c>GetAwaiter().GetResult()</c> rethrows
	/// the original exception. This is the shape every implementer that declares no synchronous primitive
	/// inherits.
	/// </summary>
	[TestMethod]
	public void CommandExecutor_Default_Sync_Primitive_Throws_Unwrapped()
	{
		ICommandExecutor executor = new AsyncOnlyCommandExecutor();

		TimeoutException thrown = Assert.ThrowsExactly<TimeoutException>(
			() => executor.Execute(AsyncOnlyCommandExecutor.FailingCommand),
			"the default synchronous primitive should surface the original exception, not an AggregateException wrapping it");

		Assert.IsNull(thrown.InnerException, "the default synchronous primitive should not nest the failure inside another exception");
	}

	/// <summary>
	/// The delay before the token used by the mid-run cancellation tests is cancelled. Long enough for the
	/// child process to have started, short enough to keep the tests quick.
	/// </summary>
	private static readonly TimeSpan CancellationDelay = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// How long the child process in the mid-run cancellation tests sleeps for. A cancelled run has to
	/// return well inside this, so it needs to be far longer than <see cref="CancellationDelay"/>.
	/// </summary>
	private const int SleepSeconds = 20;

	/// <summary>
	/// Builds a command that occupies the shell for <paramref name="seconds"/> without producing output.
	/// </summary>
	/// <param name="seconds">How long the command should run for.</param>
	/// <returns>The platform's sleep command.</returns>
	private static string SleepCommand(int seconds) =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? $"ping -n {seconds + 1} 127.0.0.1 > nul"
			: $"sleep {seconds}";

	/// <summary>
	/// An executor that declares only the asynchronous members, so it inherits every synchronous default
	/// from <see cref="ICommandExecutor"/>. It runs no process: the point is the interface's own bridge.
	/// </summary>
	private sealed class AsyncOnlyCommandExecutor : ICommandExecutor
	{
		/// <summary>
		/// The command this executor answers with a faulted task.
		/// </summary>
		public const string FailingCommand = "fail";

		/// <inheritdoc/>
		public Task<CommandResult> ExecuteAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default) =>
			ExecuteAsync(command, null, workingDirectory, cancellationToken);

		/// <inheritdoc/>
		public Task<CommandResult> ExecuteAsync(string command, IReadOnlyDictionary<string, string>? environmentVariables, string? workingDirectory = null, CancellationToken cancellationToken = default) =>
			command == FailingCommand
				? Task.FromException<CommandResult>(new TimeoutException("the command timed out"))
				: Task.FromResult(new CommandResult(0, command, string.Empty));
	}
}
