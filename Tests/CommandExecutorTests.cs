// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Common.Tests;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using ktsu.Abstractions;
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
