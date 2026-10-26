// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using ktsu.Essentials;
using ktsu.Essentials.SerializationProviders.Json;
using ktsu.Essentials.SerializationProviders.NewtonsoftJson;
using ktsu.Essentials.SerializationProviders.Toml;
using ktsu.Essentials.SerializationProviders.Yaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A realistic settings object, with a multi-word property, a list and a nested object, comes back from every
/// serializer as it went in. TOML wrote <c>display_name</c> and read it back looking for <c>DisplayName</c>, and
/// dropped lists and nested tables, so most of such an object was lost on the way back (issue #69).
/// </summary>
[TestClass]
public sealed class SerializationRoundTripTests
{
	private static readonly string[] ExpectedTags = ["a", "b"];

	public static IEnumerable<object[]> Serializers =>
	[
		[new JsonSerializationProvider(), nameof(JsonSerializationProvider)],
		[new NewtonsoftJsonSerializationProvider(), nameof(NewtonsoftJsonSerializationProvider)],
		[new YamlSerializationProvider(), nameof(YamlSerializationProvider)],
		[new TomlSerializationProvider(), nameof(TomlSerializationProvider)],
	];

	[TestMethod]
	[DynamicData(nameof(Serializers))]
	public void A_Settings_Object_Survives_A_Round_Trip(ISerializationProvider serializer, string name)
	{
		Settings original = new()
		{
			DisplayName = "hi",
			MaxCount = 5,
			Tags = ["a", "b"],
			Inner = new() { Value = 3 },
		};

		string text = serializer.Serialize(original);
		Settings? back = serializer.Deserialize<Settings>(text);

		Assert.IsNotNull(back, $"{name} could not read back what it wrote:\n{text}");
		Assert.AreEqual("hi", back.DisplayName, $"{name} lost a multi-word property:\n{text}");
		Assert.AreEqual(5, back.MaxCount, $"{name} lost a multi-word property:\n{text}");
		CollectionAssert.AreEqual(ExpectedTags, back.Tags, $"{name} lost a list:\n{text}");
		Assert.AreEqual(3, back.Inner?.Value, $"{name} lost a nested object:\n{text}");
	}

	[TestMethod]
	public void Toml_Returns_Default_For_A_Type_It_Cannot_Create()
	{
		// Activator.CreateInstance threw MissingMethodException out of Deserialize for a positional record.
		ISerializationProvider serializer = new TomlSerializationProvider();

		Assert.IsNull(serializer.Deserialize<Positional>("a = 1"));
		Assert.IsNull(serializer.Deserialize<string>("a = 1"));
	}

	[TestMethod]
	public void Toml_Ignores_Keys_With_No_Property()
	{
		ISerializationProvider serializer = new TomlSerializationProvider();
		Settings? back = serializer.Deserialize<Settings>("display_name = \"hi\"\nunknown = 1\n");

		Assert.AreEqual("hi", back?.DisplayName);
	}

	public sealed class Settings
	{
		public string DisplayName { get; set; } = string.Empty;

		public int MaxCount { get; set; }

		[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "A settable list is what a settings class written for a serializer usually has, and not every serializer here fills a read-only one.")]
		public Collection<string> Tags { get; set; } = [];

		public Inner? Inner { get; set; }
	}

	public sealed class Inner
	{
		public int Value { get; set; }
	}

	public sealed record Positional(int A);
}
