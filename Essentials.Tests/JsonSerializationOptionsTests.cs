// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ktsu.Essentials;
using ktsu.Essentials.SerializationProviders.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class JsonSerializationOptionsTests
{
	public enum Shade
	{
		Light,
		Dark,
	}

	public sealed class Settings
	{
		public string DisplayName { get; set; } = string.Empty;

		public Shade Theme { get; set; }
	}

	[TestMethod]
	public void Default_Constructor_Keeps_Indented_Output()
	{
		JsonSerializationProvider provider = new();
		string json = Serialize(provider, new Settings { DisplayName = "a" });
		StringAssert.Contains(json, "\n", "the parameterless constructor writes indented JSON, as it always has");
		StringAssert.Contains(json, "\"DisplayName\"");
	}

	[TestMethod]
	public void Options_Are_Used_To_Serialize_And_Deserialize()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() },
		};
		ISerializationProvider provider = new JsonSerializationProvider(options);

		string json = Serialize(provider, new Settings { DisplayName = "Matt", Theme = Shade.Dark });
		Assert.AreEqual("{\"displayName\":\"Matt\",\"theme\":\"Dark\"}", json);

		Settings? read = provider.Deserialize<Settings>(Encoding.UTF8.GetBytes(json));
		Assert.IsNotNull(read);
		Assert.AreEqual("Matt", read.DisplayName);
		Assert.AreEqual(Shade.Dark, read.Theme);
	}

	[TestMethod]
	public void Options_Are_Copied()
	{
		JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		JsonSerializationProvider provider = new(options);

		options.PropertyNamingPolicy = null;
		options.WriteIndented = true;

		Assert.AreEqual("{\"displayName\":\"x\",\"theme\":0}", Serialize(provider, new Settings { DisplayName = "x" }));
	}

	[TestMethod]
	public void Null_Options_Are_Rejected()
		=> Assert.ThrowsExactly<ArgumentNullException>(() => new JsonSerializationProvider(null!));

	[TestMethod]
	public void Registration_With_Options_Is_The_One_Resolved()
	{
		ServiceCollection services = new();
		services.AddJsonSerializationProvider(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		services.AddJsonSerializationProvider();
		using ServiceProvider provider = services.BuildServiceProvider();

		JsonSerializationProvider concrete = provider.GetRequiredService<JsonSerializationProvider>();
		ISerializationProvider contract = provider.GetRequiredService<ISerializationProvider>();
		Assert.AreSame(concrete, contract, "one instance serves both registrations");
		Assert.AreEqual("{\"displayName\":\"x\",\"theme\":0}", Serialize(contract, new Settings { DisplayName = "x" }));
	}

	private static string Serialize(ISerializationProvider provider, object value)
	{
		using StringWriter writer = new();
		Assert.IsTrue(provider.TrySerialize(value, writer));
		return writer.ToString();
	}
}
