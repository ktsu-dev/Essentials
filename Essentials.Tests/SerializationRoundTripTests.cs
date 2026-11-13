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

			// TOML moved an offset to UTC at millisecond precision, and wrote a Version as an empty table that
			// read back as 0.0 (issue #177). DateTimeOffset equality compares instants only, so the offset is
			// checked on its own.
			Recorded = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(-7)).AddTicks(1234567),
			Schema = new Version(1, 2, 3),
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
		Assert.AreEqual(original.Recorded, back.Recorded, $"{name} lost a DateTimeOffset's instant:\n{text}");
		Assert.AreEqual(original.Recorded.Offset, back.Recorded.Offset, $"{name} lost a DateTimeOffset's offset:\n{text}");
		Assert.AreEqual(original.Schema, back.Schema, $"{name} lost a Version:\n{text}");
	}

	[TestMethod]
	[DynamicData(nameof(ScalarSerializers))]
	public void Common_Scalars_Survive_A_Round_Trip_Inside_Lists_And_Arrays(ISerializationProvider serializer, string name)
	{
		// TOML converted these scalars only as properties and dictionary values, so in a list or array it wrote
		// empty tables that read back as defaults, while the serialize reported success (issue #207).
		ScalarLists original = new()
		{
			Ids = [new Guid("11111111-2222-3333-4444-555555555555")],
			IdArray = [new Guid("0f8fad5b-d9cb-469f-a165-70867728950e")],
			Prices = [12.34m, 0.5m],
			Timeouts = [TimeSpan.FromSeconds(30)],
			Addresses = [new Uri("https://x.y/")],
			Schemas = [new Version(1, 2, 3)],
			Recorded = [new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(-7))],
			Letters = ['a', 'b'],
			OptionalIds = [new Guid("0f8fad5b-d9cb-469f-a165-70867728950e")],
		};

		using StringWriter writer = new();
		Assert.IsTrue(serializer.TrySerialize(original, writer), $"{name} could not serialize the lists");
		string text = writer.ToString();
		ScalarLists? back = serializer.Deserialize<ScalarLists>(text);

		Assert.IsNotNull(back, $"{name} could not read back what it wrote:\n{text}");
		CollectionAssert.AreEqual(original.Ids, back.Ids, $"{name} lost a Guid list:\n{text}");
		CollectionAssert.AreEqual(original.IdArray, back.IdArray, $"{name} lost a Guid array:\n{text}");
		CollectionAssert.AreEqual(original.Prices, back.Prices, $"{name} lost a decimal list:\n{text}");
		CollectionAssert.AreEqual(original.Timeouts, back.Timeouts, $"{name} lost a TimeSpan list:\n{text}");
		CollectionAssert.AreEqual(original.Addresses, back.Addresses, $"{name} lost a Uri list:\n{text}");
		CollectionAssert.AreEqual(original.Schemas, back.Schemas, $"{name} lost a Version list:\n{text}");
		CollectionAssert.AreEqual(original.Recorded, back.Recorded, $"{name} lost a DateTimeOffset list:\n{text}");
		Assert.AreEqual(original.Recorded[0].Offset, back.Recorded.Single().Offset, $"{name} lost a DateTimeOffset's offset in a list:\n{text}");
		CollectionAssert.AreEqual(original.Letters, back.Letters, $"{name} lost a char list:\n{text}");
		CollectionAssert.AreEqual(original.OptionalIds, back.OptionalIds, $"{name} lost a nullable Guid list:\n{text}");
	}

	[TestMethod]
	public void Toml_Reports_A_Null_Item_In_A_Scalar_List_As_A_Failed_Serialize()
	{
		// A TOML array has no null, so writing the list without the item would lose it silently.
		ISerializationProvider serializer = new TomlSerializationProvider();
		ScalarLists original = new() { OptionalIds = [null] };

		using StringWriter writer = new();
		Assert.IsFalse(serializer.TrySerialize(original, writer));
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

	// A getter that throws is an object the provider cannot serialize, whichever provider it is: TrySerialize
	// reports it as false and leaves the writer empty, and Serialize throws the InvalidOperationException it
	// documents. YAML let YamlDotNet's TargetInvocationException escape both (issue #183), and JSON let
	// through anything but the exception types it expected from System.Text.Json itself (issue #201), so
	// the getter throws one of neither.
	[TestMethod]
	[DynamicData(nameof(Serializers))]
	public void A_Throwing_Getter_Is_A_Failed_Serialize(ISerializationProvider serializer, string name)
	{
		using StringWriter writer = new();

		Assert.IsFalse(serializer.TrySerialize(new ThrowingGetter(), writer), $"{name} reported success");
		Assert.AreEqual(string.Empty, writer.ToString(), $"{name} left partial output in the writer");
		Assert.ThrowsExactly<InvalidOperationException>(() => serializer.Serialize(new ThrowingGetter()), name);
	}

	// A setter that throws is input the provider cannot deserialize: Deserialize returns default rather than
	// the setter's own exception (issue #201).
	[TestMethod]
	[DynamicData(nameof(Serializers))]
	public void A_Throwing_Setter_Is_A_Failed_Deserialize(ISerializationProvider serializer, string name)
	{
		string text = serializer.Serialize(new Inner { Value = 3 });

		Assert.IsNull(serializer.Deserialize<ThrowingSetter>(text), name);
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
		[SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "A getter that throws something other than the exceptions a getter is expected to is what is under test (issue #201).")]
		public string Value
		{
			get => throw new FormatException("unreadable");
			set => _ = value;
		}
	}

	public sealed class ThrowingSetter
	{
		[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "A serializer writes instance properties, which is what is under test.")]
		public int Value
		{
			get => 0;
			set => throw new ArithmeticException($"{value} is unwritable");
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

	[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "A settable collection is what a settings class written for a serializer usually has, and not every serializer here fills a read-only one.")]
	[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "An array property is one of the shapes under test.")]
	public sealed class ScalarLists
	{
		public Collection<Guid> Ids { get; set; } = [];

		public Guid[] IdArray { get; set; } = [];

		public Collection<decimal> Prices { get; set; } = [];

		public Collection<TimeSpan> Timeouts { get; set; } = [];

		public Collection<Uri> Addresses { get; set; } = [];

		public Collection<Version> Schemas { get; set; } = [];

		public Collection<DateTimeOffset> Recorded { get; set; } = [];

		public Collection<char> Letters { get; set; } = [];

		public Collection<Guid?> OptionalIds { get; set; } = [];
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

		public DateTimeOffset Recorded { get; set; }

		public Version? Schema { get; set; }
	}
}
