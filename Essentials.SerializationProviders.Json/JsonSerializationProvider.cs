// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.SerializationProviders.Json;

using ktsu.Essentials;
using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// A serialization provider that uses System.Text.Json for serializing and deserializing objects.
/// </summary>
public class JsonSerializationProvider : ISerializationProvider
{
	/// <summary>
	/// Gets the conventional file extension for JSON, including the leading dot.
	/// </summary>
	public string FileExtension => ".json";

	private readonly JsonSerializerOptions options = new()
	{
		WriteIndented = true,
	};

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
			string json = JsonSerializer.Serialize(obj, options);
			writer.Write(json);
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
		catch (NotSupportedException)
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
			return JsonSerializer.Deserialize<T>(data, options);
		}
		catch (JsonException)
		{
			return default;
		}
		catch (NotSupportedException)
		{
			return default;
		}
		catch (ArgumentException)
		{
			return default;
		}
	}
}
