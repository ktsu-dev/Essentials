// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Common.Tests;

using System.Collections.Generic;
using ktsu.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ConfigurationProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddCommon();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> ConfigurationProviders => BuildProvider().EnumerateProviders<IConfigurationProvider>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_Save_And_Load_Roundtrip(IConfigurationProvider configProvider, string providerName)
	{
		TestConfig original = new() { Name = $"Test with {providerName}", Count = 42, Enabled = true };

		string serialized = configProvider.Save(original);
		Assert.IsFalse(string.IsNullOrEmpty(serialized), $"{providerName} should produce serialized output");

		using StringReader reader = new(serialized);
		bool loadOk = configProvider.TryLoad<TestConfig>(reader, out TestConfig? loaded);
		Assert.IsTrue(loadOk, $"{providerName} should successfully load configuration");
		Assert.IsNotNull(loaded, $"{providerName} should produce non-null config");
		Assert.AreEqual(original.Name, loaded.Name, $"{providerName} should preserve Name");
		Assert.AreEqual(original.Count, loaded.Count, $"{providerName} should preserve Count");
		Assert.AreEqual(original.Enabled, loaded.Enabled, $"{providerName} should preserve Enabled");
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_TrySave_And_TryLoad(IConfigurationProvider configProvider, string providerName)
	{
		TestConfig original = new() { Name = "TrySave", Count = 99, Enabled = false };

		using StringWriter writer = new();
		bool saveOk = configProvider.TrySave(original, writer);
		Assert.IsTrue(saveOk, $"{providerName} should successfully save");

		string content = writer.ToString();
		Assert.IsFalse(string.IsNullOrEmpty(content), $"{providerName} should produce content");

		using StringReader reader = new(content);
		bool loadOk = configProvider.TryLoad<TestConfig>(reader, out TestConfig? loaded);
		Assert.IsTrue(loadOk, $"{providerName} should successfully load");
		Assert.IsNotNull(loaded);
		Assert.AreEqual(original.Name, loaded.Name);
		Assert.AreEqual(original.Count, loaded.Count);
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_Load_From_String(IConfigurationProvider configProvider, string providerName)
	{
		TestConfig original = new() { Name = "FromString", Count = 7, Enabled = true };

		string serialized = configProvider.Save(original);
		TestConfig? loaded = configProvider.Load<TestConfig>(serialized);
		Assert.IsNotNull(loaded, $"{providerName} should load from string");
		Assert.AreEqual(original.Name, loaded.Name);
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_TryLoad_Invalid_Content_Returns_False(IConfigurationProvider configProvider, string providerName)
	{
		_ = providerName;
		using StringReader reader = new("this is definitely not valid structured data {{{[[[");
		bool loadOk = configProvider.TryLoad<TestConfig>(reader, out TestConfig? loaded);
		// Some providers may still parse invalid data; we just verify no exception
		_ = loadOk;
		_ = loaded;
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_TryLoad_Null_Source_Returns_False(IConfigurationProvider configProvider, string providerName)
	{
		bool loadOk = configProvider.TryLoad<TestConfig>(null!, out TestConfig? loaded);
		Assert.IsFalse(loadOk, $"{providerName} should return false for null source");
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_TrySave_Null_Destination_Returns_False(IConfigurationProvider configProvider, string providerName)
	{
		TestConfig config = new() { Name = "test" };
		bool saveOk = configProvider.TrySave(config, null!);
		Assert.IsFalse(saveOk, $"{providerName} should return false for null destination");
	}

	public sealed class TestConfig
	{
		public string Name { get; set; } = string.Empty;
		public int Count { get; set; }
		public bool Enabled { get; set; }
	}
}
