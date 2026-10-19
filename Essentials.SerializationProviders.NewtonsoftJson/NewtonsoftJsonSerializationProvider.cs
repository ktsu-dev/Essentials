// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.SerializationProviders.NewtonsoftJson;

using ktsu.Essentials;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// A serialization provider that uses Newtonsoft.Json for JSON serialization and deserialization.
/// </summary>
public class NewtonsoftJsonSerializationProvider : ISerializationProvider
{
	/// <summary>
	/// Gets the conventional file extension for JSON, including the leading dot.
	/// </summary>
	public string FileExtension => ".json";

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
			// Serialize into a local buffer and copy it to the caller's writer only on success.
			// Disposing a JsonTextWriter closes any object or array a failure left open, so
			// streaming straight into the caller's writer would leave truncated JSON that parses.
			using StringWriter buffer = new(CultureInfo.InvariantCulture);
			using (JsonTextWriter jsonWriter = new(buffer))
			{
				JsonSerializer serializer = JsonSerializer.Create(settings);
				serializer.Serialize(jsonWriter, obj);
			}

			writer.Write(buffer.ToString());
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
			string jsonString = Encoding.UTF8.GetString(Utf8Bom.Skip(data));
			return JsonConvert.DeserializeObject<T>(jsonString, settings);
		}
		catch (JsonReaderException)
		{
			return default;
		}
		catch (JsonSerializationException)
		{
			return default;
		}
		catch (ArgumentException)
		{
			return default;
		}
	}
}
