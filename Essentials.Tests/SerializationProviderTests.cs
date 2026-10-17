// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Text;
using ktsu.Essentials;
using ktsu.Essentials.SerializationProviders.Json;
using ktsu.Essentials.SerializationProviders.NewtonsoftJson;
using ktsu.Essentials.SerializationProviders.Toml;
using ktsu.Essentials.SerializationProviders.Yaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ConfigurationProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddSingleton<ISerializationProvider, JsonSerializationProvider>();
		services.AddSingleton<ISerializationProvider, YamlSerializationProvider>();
		services.AddSingleton<ISerializationProvider, TomlSerializationProvider>();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> ConfigurationProviders => BuildProvider().EnumerateProviders<ISerializationProvider>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_Serialize_And_Deserialize_Roundtrip(ISerializationProvider configProvider, string providerName)
	{
		TestConfig original = new() { Name = $"Test with {providerName}", Count = 42, Enabled = true };

		string serialized = configProvider.Serialize(original);
		Assert.IsFalse(string.IsNullOrEmpty(serialized), $"{providerName} should produce serialized output");

		TestConfig? loaded = configProvider.Deserialize<TestConfig>(serialized);
		Assert.IsNotNull(loaded, $"{providerName} should produce non-null config");
		Assert.AreEqual(original.Name, loaded.Name, $"{providerName} should preserve Name");
		Assert.AreEqual(original.Count, loaded.Count, $"{providerName} should preserve Count");
		Assert.AreEqual(original.Enabled, loaded.Enabled, $"{providerName} should preserve Enabled");
	}

	[TestMethod]
	public void Serialize_Throws_When_TrySerialize_Fails()
	{
		// Serialize discarded TrySerialize's false and returned an empty string (issue #70).
		ISerializationProvider provider = new JsonSerializationProvider();
		CyclicConfig cyclic = new();
		cyclic.Self = cyclic;

		Assert.ThrowsExactly<InvalidOperationException>(() => provider.Serialize(cyclic));

		using StringWriter writer = new();
		Assert.ThrowsExactly<InvalidOperationException>(() => provider.Serialize(cyclic, writer));
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_TrySerialize_And_Deserialize(ISerializationProvider configProvider, string providerName)
	{
		TestConfig original = new() { Name = "TrySerialize", Count = 99, Enabled = false };

		using StringWriter writer = new();
		bool saveOk = configProvider.TrySerialize(original, writer);
		Assert.IsTrue(saveOk, $"{providerName} should successfully serialize");

		string content = writer.ToString();
		Assert.IsFalse(string.IsNullOrEmpty(content), $"{providerName} should produce content");

		using StringReader reader = new(content);
		TestConfig? loaded = configProvider.Deserialize<TestConfig>(reader);
		Assert.IsNotNull(loaded);
		Assert.AreEqual(original.Name, loaded.Name);
		Assert.AreEqual(original.Count, loaded.Count);
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_Deserialize_From_String(ISerializationProvider configProvider, string providerName)
	{
		TestConfig original = new() { Name = "FromString", Count = 7, Enabled = true };

		string serialized = configProvider.Serialize(original);
		TestConfig? loaded = configProvider.Deserialize<TestConfig>(serialized);
		Assert.IsNotNull(loaded, $"{providerName} should deserialize from string");
		Assert.AreEqual(original.Name, loaded.Name);
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_Deserialize_From_Bytes(ISerializationProvider configProvider, string providerName)
	{
		TestConfig original = new() { Name = "FromBytes", Count = 13, Enabled = true };

		string serialized = configProvider.Serialize(original);
		byte[] bytes = Encoding.UTF8.GetBytes(serialized);
		TestConfig? loaded = configProvider.Deserialize<TestConfig>(bytes);
		Assert.IsNotNull(loaded, $"{providerName} should deserialize from bytes");
		Assert.AreEqual(original.Name, loaded.Name);
		Assert.AreEqual(original.Count, loaded.Count);
	}

	public static IEnumerable<object[]> ByteDeserializingProviders =>
	[
		[new JsonSerializationProvider(), nameof(JsonSerializationProvider)],
		[new NewtonsoftJsonSerializationProvider(), nameof(NewtonsoftJsonSerializationProvider)],
		[new YamlSerializationProvider(), nameof(YamlSerializationProvider)],
		[new TomlSerializationProvider(), nameof(TomlSerializationProvider)],
	];

	[TestMethod]
	[DynamicData(nameof(ByteDeserializingProviders))]
	public void Configuration_Deserialize_From_Bytes_With_Utf8_Bom(ISerializationProvider configProvider, string providerName)
	{
		// The JSON provider returned null for UTF-8 bytes that start with a byte order mark, as
		// File.ReadAllBytes yields for a file saved by Notepad or Visual Studio (issue #76).
		TestConfig original = new() { Name = "WithBom", Count = 21, Enabled = true };

		string serialized = configProvider.Serialize(original);
		byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(serialized)];
		TestConfig? loaded = configProvider.Deserialize<TestConfig>(bytes);
		Assert.IsNotNull(loaded, $"{providerName} should deserialize BOM-prefixed bytes");
		Assert.AreEqual(original.Name, loaded.Name);
		Assert.AreEqual(original.Count, loaded.Count);
		Assert.AreEqual(original.Enabled, loaded.Enabled);
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_Deserialize_Invalid_Content(ISerializationProvider configProvider, string providerName)
	{
		_ = providerName;
		byte[] invalidBytes = Encoding.UTF8.GetBytes("this is definitely not valid structured data {{{[[[");
		// Should not throw — may return null or a default object depending on provider
		TestConfig? loaded = configProvider.Deserialize<TestConfig>(invalidBytes);
		// Reaching this point without exception means the provider handled invalid input gracefully
		Assert.IsNull(loaded, $"{providerName} should return null for invalid content");
	}

	[TestMethod]
	[DynamicData(nameof(ConfigurationProviders))]
	public void Configuration_TrySerialize_Null_Writer_Returns_False(ISerializationProvider configProvider, string providerName)
	{
		TestConfig config = new() { Name = "test" };
		bool saveOk = configProvider.TrySerialize(config, null!);
		Assert.IsFalse(saveOk, $"{providerName} should return false for null writer");
	}

	public sealed class TestConfig
	{
		public string Name { get; set; } = string.Empty;
		public int Count { get; set; }
		public bool Enabled { get; set; }
	}

	public sealed class CyclicConfig
	{
		public CyclicConfig? Self { get; set; }
	}
}
