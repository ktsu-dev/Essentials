// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SerializationProviders;

using System;
using System.IO;
using System.Text;
using ktsu.Abstractions;
using Newtonsoft.Json;

/// <summary>
/// A serialization provider that uses Newtonsoft.Json for JSON serialization and deserialization.
/// </summary>
public class NewtonsoftJson : ISerializationProvider
{
	private readonly JsonSerializerSettings settings = new();

	/// <summary>
	/// Deserializes JSON data from a byte span into a T object.
	/// </summary>
	/// <typeparam name="T">The type of object to deserialize.</typeparam>
	/// <param name="data">The UTF-8 encoded JSON byte data to deserialize.</param>
	/// <returns>A T object representing the JSON data, or default if the data is empty or invalid.</returns>
	public T? Deserialize<T>(ReadOnlySpan<byte> data)
	{
		if (data.IsEmpty)
		{
			return default;
		}

		try
		{
			string jsonString = Encoding.UTF8.GetString(data);
			return JsonConvert.DeserializeObject<T>(jsonString, settings);
		}
		catch (JsonReaderException)
		{
			return default;
		}
		catch (ArgumentException)
		{
			return default;
		}
	}

	/// <summary>
	/// Attempts to serialize an object to JSON and write it to the specified TextWriter.
	/// </summary>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="writer">The TextWriter to write the JSON output to.</param>
	/// <returns>true if serialization was successful; false otherwise.</returns>
	public bool TrySerialize(object obj, TextWriter writer)
	{
		if (writer is null)
		{
			return false;
		}

		try
		{
			using JsonTextWriter jsonWriter = new(writer);
			JsonSerializer serializer = JsonSerializer.Create(settings);
			serializer.Serialize(jsonWriter, obj);
			return true;
		}
		catch (JsonSerializationException)
		{
			return false;
		}
		catch (JsonWriterException)
		{
			return false;
		}
	}
}
