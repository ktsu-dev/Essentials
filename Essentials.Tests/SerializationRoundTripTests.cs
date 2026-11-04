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

	// YAML is left out: it writes a Uri as its properties and cannot read a DateTimeOffset back, which is a
	// separate gap from the TOML one this covers.
	public static IEnumerable<object[]> ScalarSerializers =>
	[
		[new JsonSerializationProvider(), nameof(JsonSerializationProvider)],
		[new NewtonsoftJsonSerializationProvider(), nameof(NewtonsoftJsonSerializationProvider)],
		[new TomlSerializationProvider(), nameof(TomlSerializationProvider)],
	];

	[TestMethod]
	[DynamicData(nameof(ScalarSerializers))]
	public void Common_Scalars_Survive_A_Round_Trip(ISerializationProvider serializer, string name)
	{
		// TOML wrote Guid, decimal, TimeSpan and Uri as empty tables, so they read back as defaults while the
		// serialize reported success, and threw on char instead of returning false (issue #165).
		Scalars original = new()
		{
			Id = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
			Price = 12.34m,
			Timeout = TimeSpan.FromSeconds(30),
			Address = new Uri("https://x.y/path?q=1"),
			Letter = 'q',
			When = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
			Kind = ScalarKind.B,
		};

		using StringWriter writer = new();
		Assert.IsTrue(serializer.TrySerialize(original, writer), $"{name} could not serialize the scalars");
		string text = writer.ToString();
		Scalars? back = serializer.Deserialize<Scalars>(text);

		Assert.IsNotNull(back, $"{name} could not read back what it wrote:\n{text}");
		Assert.AreEqual(original.Id, back.Id, $"{name} lost a Guid:\n{text}");
		Assert.AreEqual(original.Price, back.Price, $"{name} lost a decimal:\n{text}");
		Assert.AreEqual(original.Timeout, back.Timeout, $"{name} lost a TimeSpan:\n{text}");
		Assert.AreEqual(original.Address, back.Address, $"{name} lost a Uri:\n{text}");
		Assert.AreEqual(original.Letter, back.Letter, $"{name} lost a char:\n{text}");
		Assert.AreEqual(original.When, back.When, $"{name} lost a DateTimeOffset:\n{text}");
		Assert.AreEqual(original.Kind, back.Kind, $"{name} lost an enum:\n{text}");
	}

	[TestMethod]
	public void Toml_Reports_A_Value_It_Has_No_Type_For_As_A_Failed_Serialize()
	{
		// Tomlyn throws InvalidOperationException for a primitive it cannot write, which TrySerialize let escape.
		ISerializationProvider serializer = new TomlSerializationProvider();
		using StringWriter writer = new();

		Assert.IsFalse(serializer.TrySerialize(new Unwritable { Handle = 5 }, writer));
	}

	[TestMethod]
	public void Toml_Reads_A_Hand_Written_Decimal_Written_As_A_Number()
	{
		ISerializationProvider serializer = new TomlSerializationProvider();

		Assert.AreEqual(12m, serializer.Deserialize<Scalars>("price = 12\n")?.Price);
		Assert.AreEqual(0.5m, serializer.Deserialize<Scalars>("price = 0.5\n")?.Price);
	}

	[TestMethod]
	public void Toml_Round_Trips_Nullable_Scalars()
	{
		ISerializationProvider serializer = new TomlSerializationProvider();
		NullableScalars original = new() { Id = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"), Price = 1.25m };

		NullableScalars? back = serializer.Deserialize<NullableScalars>(serializer.Serialize(original));

		Assert.AreEqual(original.Id, back?.Id);
		Assert.AreEqual(original.Price, back?.Price);
	}

	[TestMethod]
	public void Toml_Reports_A_Throwing_Getter_As_A_Failed_Serialize()
	{
		ISerializationProvider serializer = new TomlSerializationProvider();
		using StringWriter writer = new();

		Assert.IsFalse(serializer.TrySerialize(new ThrowingGetter(), writer));
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

	public sealed class NullableScalars
	{
		public Guid? Id { get; set; }

		public decimal? Price { get; set; }
	}

	public sealed class ThrowingGetter
	{
		[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "A serializer reads instance properties, which is what is under test.")]
		public string Value
		{
			get => throw new InvalidOperationException("unreadable");
			set => _ = value;
		}
	}

	public sealed class Unwritable
	{
		public nint Handle { get; set; }
	}

	public enum ScalarKind
	{
		A,
		B,
	}

	public sealed class Scalars
	{
		public Guid Id { get; set; }

		public decimal Price { get; set; }

		public TimeSpan Timeout { get; set; }

		public Uri? Address { get; set; }

		public char Letter { get; set; }

		public DateTimeOffset When { get; set; }

		public ScalarKind Kind { get; set; }
	}
}
