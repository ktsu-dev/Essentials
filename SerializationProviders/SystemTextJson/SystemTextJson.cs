// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.SerializationProviders;

using System;
using System.IO;
using System.Text.Json;
using ktsu.Abstractions;

/// <summary>
/// A serialization provider that uses System.Text.Json for JSON serialization and deserialization.
/// </summary>
public class SystemTextJson : ISerializationProvider
{
	private readonly JsonSerializerOptions options = new();

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
			return JsonSerializer.Deserialize<T>(data, options);
		}
		catch (JsonException)
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
			if (obj is null)
			{
				writer.Write("null");
				return true;
			}

			string json = JsonSerializer.Serialize(obj, obj.GetType(), options);
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
	}
}
