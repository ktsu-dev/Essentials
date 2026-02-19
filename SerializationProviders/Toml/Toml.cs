// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.SerializationProviders;

using ktsu.Essentials;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

/// <summary>
/// A serialization provider that uses Tomlyn for serializing and deserializing objects.
/// </summary>
public class Toml : ISerializationProvider
{
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
			string toml = Tomlyn.Toml.FromModel(obj);
			writer.Write(toml);
			return true;
		}
		catch (TomlException)
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
			TomlTable table = Tomlyn.Toml.ToModel(content);
			object? result = ConvertToModel<T>(table);
			if (result is T typedResult)
			{
				return typedResult;
			}

			return default;
		}
		catch (TomlException)
		{
			return default;
		}
		catch (ArgumentException)
		{
			return default;
		}
	}

	private static T? ConvertToModel<T>(TomlTable table)
	{
		Type type = typeof(T);
		object instance = Activator.CreateInstance(type)!;

		foreach (KeyValuePair<string, object> kvp in table)
		{
			PropertyInfo? property = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
			if (property is not null && property.CanWrite)
			{
				try
				{
					object? value = Convert.ChangeType(kvp.Value, property.PropertyType, CultureInfo.InvariantCulture);
					property.SetValue(instance, value);
				}
				catch (InvalidCastException)
				{
					// Skip properties that can't be converted
				}
				catch (FormatException)
				{
					// Skip properties that can't be converted
				}
				catch (OverflowException)
				{
					// Skip properties that can't be converted
				}
			}
		}

		return (T)instance;
	}
}
