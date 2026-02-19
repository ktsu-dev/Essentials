// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Common.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using ktsu.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[DoNotParallelize]
public class LoggingProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddCommon();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> LoggingProviders => BuildProvider().EnumerateProviders<ILoggingProvider>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	[DynamicData(nameof(LoggingProviders))]
	public void LoggingProvider_Can_Log_Information(ILoggingProvider logger, string providerName)
	{
		using StringWriter stdoutCapture = new();
		TextWriter originalOut = System.Console.Out;
		System.Console.SetOut(stdoutCapture);
		try
		{
			logger.Log(LogLevel.Information, "test message");
			string output = stdoutCapture.ToString();
			Assert.IsTrue(output.Contains("test message"), $"{providerName} should write info message to stdout");
			Assert.IsTrue(output.Contains("INFO"), $"{providerName} should include level in output");
		}
		finally
		{
			System.Console.SetOut(originalOut);
		}
	}

	[TestMethod]
	[DynamicData(nameof(LoggingProviders))]
	public void LoggingProvider_Writes_Errors_To_Stderr(ILoggingProvider logger, string providerName)
	{
		using StringWriter stderrCapture = new();
		TextWriter originalErr = System.Console.Error;
		System.Console.SetError(stderrCapture);
		try
		{
			logger.Log(LogLevel.Error, "error message");
			string output = stderrCapture.ToString();
			Assert.IsTrue(output.Contains("error message"), $"{providerName} should write error to stderr");
			Assert.IsTrue(output.Contains("ERROR"), $"{providerName} should include ERROR level");
		}
		finally
		{
			System.Console.SetError(originalErr);
		}
	}

	[TestMethod]
	[DynamicData(nameof(LoggingProviders))]
	public void LoggingProvider_IsEnabled_Respects_Level(ILoggingProvider logger, string providerName)
	{
		Assert.IsTrue(logger.IsEnabled(LogLevel.Information), $"{providerName} should enable Information by default");
		Assert.IsTrue(logger.IsEnabled(LogLevel.Error), $"{providerName} should enable Error");
		Assert.IsFalse(logger.IsEnabled(LogLevel.None), $"{providerName} should not enable None level");
	}

	[TestMethod]
	[DynamicData(nameof(LoggingProviders))]
	public void LoggingProvider_Log_With_Exception(ILoggingProvider logger, string providerName)
	{
		using StringWriter stderrCapture = new();
		TextWriter originalErr = System.Console.Error;
		System.Console.SetError(stderrCapture);
		try
		{
			Exception ex = new InvalidOperationException("test exception");
			logger.Log(LogLevel.Error, ex, "error with exception");
			string output = stderrCapture.ToString();
			Assert.IsTrue(output.Contains("error with exception"), $"{providerName} should include message");
			Assert.IsTrue(output.Contains("test exception"), $"{providerName} should include exception");
		}
		finally
		{
			System.Console.SetError(originalErr);
		}
	}

	[TestMethod]
	[DynamicData(nameof(LoggingProviders))]
	public void LoggingProvider_Convenience_Methods_Work(ILoggingProvider logger, string providerName)
	{
		using StringWriter stdoutCapture = new();
		TextWriter originalOut = System.Console.Out;
		System.Console.SetOut(stdoutCapture);
		try
		{
			logger.LogInformation("info via convenience");
			string output = stdoutCapture.ToString();
			Assert.IsTrue(output.Contains("info via convenience"), $"{providerName} should support LogInformation");
		}
		finally
		{
			System.Console.SetOut(originalOut);
		}
	}
}
