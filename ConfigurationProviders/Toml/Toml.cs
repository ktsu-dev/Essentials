// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.ConfigurationProviders;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ktsu.Abstractions;
using Tomlyn;
using Tomlyn.Model;

/// <summary>
/// A configuration provider that uses Tomlyn for loading and saving configuration objects.
/// </summary>
public class Toml : IConfigurationProvider
{
	/// <summary>
	/// Tries to load a configuration object from the specified source.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to load.</typeparam>
	/// <param name="source">The text reader containing the configuration data.</param>
	/// <param name="config">When this method returns, contains the loaded configuration if successful, or the default value if not.</param>
	/// <returns>True if the configuration was loaded successfully, false otherwise.</returns>
	public bool TryLoad<T>(TextReader source, out T? config)
	{
		config = default;

		if (source is null)
		{
			return false;
		}

		try
		{
			string content = source.ReadToEnd();
			TomlTable table = Tomlyn.Toml.ToModel(content);
			object? result = ConvertToModel<T>(table);
			if (result is T typedResult)
			{
				config = typedResult;
				return true;
			}

			return false;
		}
		catch (TomlException)
		{
			return false;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to save a configuration object to the specified destination.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to save.</typeparam>
	/// <param name="config">The configuration object to save.</param>
	/// <param name="destination">The text writer to write the configuration data to.</param>
	/// <returns>True if the configuration was saved successfully, false otherwise.</returns>
	public bool TrySave<T>(T config, TextWriter destination)
	{
		if (destination is null)
		{
			return false;
		}

		try
		{
			string toml = Tomlyn.Toml.FromModel(config!);
			destination.Write(toml);
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
