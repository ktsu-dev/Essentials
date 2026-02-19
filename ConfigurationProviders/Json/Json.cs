// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.ConfigurationProviders;

using System;
using System.IO;
using System.Text.Json;
using ktsu.Abstractions;

/// <summary>
/// A configuration provider that uses System.Text.Json for loading and saving configuration objects.
/// </summary>
public class Json : IConfigurationProvider
{
	private readonly JsonSerializerOptions options = new()
	{
		WriteIndented = true,
	};

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
			config = JsonSerializer.Deserialize<T>(content, options);
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
			string json = JsonSerializer.Serialize(config, options);
			destination.Write(json);
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
}
