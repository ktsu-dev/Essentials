// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.ConfigurationProviders;

using System;
using System.IO;
using ktsu.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// A configuration provider that uses YamlDotNet for loading and saving configuration objects.
/// </summary>
public class Yaml : IConfigurationProvider
{
	private readonly IDeserializer deserializer = new DeserializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
		.Build();

	private readonly ISerializer serializer = new SerializerBuilder()
		.WithNamingConvention(CamelCaseNamingConvention.Instance)
		.Build();

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
			config = deserializer.Deserialize<T>(source);
			return true;
		}
		catch (YamlException)
		{
			return false;
		}
		catch (InvalidOperationException)
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
			serializer.Serialize(destination, config!);
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
}
