// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SerializationProviders;

using System;
using System.IO;
using System.Text;
using ktsu.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// A serialization provider that uses YamlDotNet for serializing and deserializing objects.
/// </summary>
public class Yaml : ISerializationProvider
{
	private readonly IDeserializer deserializer = new DeserializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
		.Build();

	private readonly ISerializer serializer = new SerializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
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
			serializer.Serialize(writer, obj);
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
			string content = Encoding.UTF8.GetString(data);
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
