// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.SerializationProviders.Yaml;

using ktsu.Essentials;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// A serialization provider that uses YamlDotNet for serializing and deserializing objects.
/// </summary>
public class YamlSerializationProvider : ISerializationProvider
{
	/// <summary>
	/// Gets the conventional file extension for YAML, including the leading dot.
	/// </summary>
	public string FileExtension => ".yaml";

	// Unmatched keys are ignored, as the JSON providers do, so a record written before a property was
	// removed or renamed still loads instead of reading back as default.
	private readonly IDeserializer deserializer = new DeserializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	// Strings that YAML would read as something else, such as "null", "~" or "true", are quoted, so they
	// read back as the strings they were instead of as null or another type.
	private readonly ISerializer serializer = new SerializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
		.WithQuotingNecessaryStrings()
		.Build();

	/// <summary>
	/// Tries to serialize the specified object into the writer.
	/// </summary>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="writer">The writer to write the serialized data to.</param>
	/// <returns>True if the serialization was successful, false otherwise.</returns>
	public bool TrySerialize(object obj, TextWriter writer)
	{
		if (writer is null)
		{
			return false;
		}

		try
		{
			// Serialize into a local buffer and copy it to the caller's writer only on success, so a
			// failure partway through leaves the writer as it was rather than holding half a document.
			using StringWriter buffer = new(CultureInfo.InvariantCulture);
			serializer.Serialize(buffer, obj);
			writer.Write(buffer.ToString());
			return true;
		}
		catch (YamlException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
		catch (TargetInvocationException)
		{
			// A property getter threw while YamlDotNet read it
			return false;
		}
	}

	/// <summary>
	/// Deserializes the specified data into a specific type.
	/// </summary>
	/// <typeparam name="T">The type to deserialize into.</typeparam>
	/// <param name="data">The UTF-8 encoded data to deserialize.</param>
	/// <returns>The deserialized object, or default if deserialization fails.</returns>
	public T? Deserialize<T>(ReadOnlySpan<byte> data)
	{
		if (data.IsEmpty)
		{
			return default;
		}

		try
		{
			string content = Encoding.UTF8.GetString(Utf8Bom.Skip(data));
			return deserializer.Deserialize<T>(content);
		}
		catch (YamlException)
		{
			return default;
		}
		catch (InvalidOperationException)
		{
			return default;
		}
		catch (ArgumentException)
		{
			return default;
		}
	}
}
