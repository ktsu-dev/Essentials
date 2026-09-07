// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
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
}
