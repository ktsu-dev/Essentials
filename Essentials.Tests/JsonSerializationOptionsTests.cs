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

	[TestMethod]
	public void A_Type_With_Colliding_Names_Fails_Without_Throwing()
	{
		ISerializationProvider provider = new JsonSerializationProvider();
		using StringWriter writer = new();

		Assert.IsFalse(provider.TrySerialize(new Colliding { A = 1, B = 2 }, writer));
		Assert.AreEqual(string.Empty, writer.ToString(), "nothing reaches the writer when serialization fails");
		Assert.IsNull(provider.Deserialize<Colliding>("{\"x\":1}"u8));
		Assert.ThrowsExactly<InvalidOperationException>(() => provider.Serialize(new Colliding()));
	}

	[TestMethod]
	public void A_Constructor_That_Does_Not_Bind_Fails_Without_Throwing()
	{
		ISerializationProvider provider = new JsonSerializationProvider();
		Assert.IsNull(provider.Deserialize<Unbindable>("{\"Value\":1}"u8));
	}

	public sealed class Colliding
	{
		[JsonPropertyName("x")]
		public int A { get; set; }

		[JsonPropertyName("x")]
		public int B { get; set; }
	}

	// The only constructor's parameter is not named after a property, so it cannot be bound.
	public sealed class Unbindable(int p)
	{
		public int Value { get; } = p;
	}

	private static string Serialize(ISerializationProvider provider, object value)
	{
		using StringWriter writer = new();
		Assert.IsTrue(provider.TrySerialize(value, writer));
		return writer.ToString();
	}
}
