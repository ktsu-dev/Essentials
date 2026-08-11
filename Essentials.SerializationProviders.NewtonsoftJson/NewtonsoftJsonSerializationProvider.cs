// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.SerializationProviders.NewtonsoftJson;

using ktsu.Essentials;
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// A serialization provider that uses Newtonsoft.Json for JSON serialization and deserialization.
/// </summary>
public class NewtonsoftJsonSerializationProvider : ISerializationProvider
{
	private readonly JsonSerializerSettings settings = new();

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
}
