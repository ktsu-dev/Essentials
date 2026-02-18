// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for configuration providers that can load and save typed configuration objects
/// from various sources such as YAML, TOML, JSON, or other configuration formats.
/// </summary>
[SuppressMessage("Maintainability", "CA1510:Use ArgumentNullException throw helper", Justification = "Not available in netstandard")]
public interface IConfigurationProvider
{
	/// <summary>
	/// Tries to load a configuration object from the specified source.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to load.</typeparam>
	/// <param name="source">The text reader containing the configuration data.</param>
	/// <param name="config">When this method returns, contains the loaded configuration if successful, or the default value if not.</param>
	/// <returns>True if the configuration was loaded successfully, false otherwise.</returns>
	public bool TryLoad<T>(TextReader source, out T? config);

	/// <summary>
	/// Tries to save a configuration object to the specified destination.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to save.</typeparam>
	/// <param name="config">The configuration object to save.</param>
	/// <param name="destination">The text writer to write the configuration data to.</param>
	/// <returns>True if the configuration was saved successfully, false otherwise.</returns>
	public bool TrySave<T>(T config, TextWriter destination);

	/// <summary>
	/// Loads a configuration object from the specified source.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to load.</typeparam>
	/// <param name="source">The text reader containing the configuration data.</param>
	/// <returns>The loaded configuration object.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the configuration could not be loaded.</exception>
	public T? Load<T>(TextReader source)
	{
		Ensure.NotNull(source);

		if (!TryLoad<T>(source, out T? config))
		{
			throw new InvalidOperationException("Failed to load configuration.");
		}

		return config;
	}

	/// <summary>
	/// Loads a configuration object from the specified string content.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to load.</typeparam>
	/// <param name="content">The string containing the configuration data.</param>
	/// <returns>The loaded configuration object.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the configuration could not be loaded.</exception>
	public T? Load<T>(string content)
	{
		Ensure.NotNull(content);

		using StringReader reader = new(content);
		return Load<T>(reader);
	}

	/// <summary>
	/// Saves a configuration object and returns the result as a string.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to save.</typeparam>
	/// <param name="config">The configuration object to save.</param>
	/// <returns>A string containing the serialized configuration.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the configuration could not be saved.</exception>
	public string Save<T>(T config)
	{
		using StringWriter writer = new();
		if (!TrySave(config, writer))
		{
			throw new InvalidOperationException("Failed to save configuration.");
		}

		return writer.ToString();
	}

	/// <summary>
	/// Saves a configuration object to the specified destination.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to save.</typeparam>
	/// <param name="config">The configuration object to save.</param>
	/// <param name="destination">The text writer to write the configuration data to.</param>
	/// <exception cref="InvalidOperationException">Thrown when the configuration could not be saved.</exception>
	public void Save<T>(T config, TextWriter destination)
	{
		if (!TrySave(config, destination))
		{
			throw new InvalidOperationException("Failed to save configuration.");
		}
	}

	/// <summary>
	/// Tries to load a configuration object from the specified source asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to load.</typeparam>
	/// <param name="source">The text reader containing the configuration data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A tuple containing a boolean indicating success and the loaded configuration.</returns>
	public Task<(bool Success, T? Config)> TryLoadAsync<T>(TextReader source, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<(bool, T?)>(cancellationToken)
			: Task.Run(() =>
			{
				bool success = TryLoad<T>(source, out T? config);
				return (success, config);
			}, cancellationToken);

	/// <summary>
	/// Loads a configuration object from the specified source asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to load.</typeparam>
	/// <param name="source">The text reader containing the configuration data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The loaded configuration object.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the configuration could not be loaded.</exception>
	public Task<T?> LoadAsync<T>(TextReader source, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<T?>(cancellationToken)
			: Task.Run(() => Load<T>(source), cancellationToken);

	/// <summary>
	/// Loads a configuration object from the specified string content asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to load.</typeparam>
	/// <param name="content">The string containing the configuration data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The loaded configuration object.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the configuration could not be loaded.</exception>
	public Task<T?> LoadAsync<T>(string content, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<T?>(cancellationToken)
			: Task.Run(() => Load<T>(content), cancellationToken);

	/// <summary>
	/// Tries to save a configuration object to the specified destination asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to save.</typeparam>
	/// <param name="config">The configuration object to save.</param>
	/// <param name="destination">The text writer to write the configuration data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the configuration was saved successfully, false otherwise.</returns>
	public Task<bool> TrySaveAsync<T>(T config, TextWriter destination, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => TrySave(config, destination), cancellationToken);

	/// <summary>
	/// Saves a configuration object and returns the result as a string asynchronously.
	/// </summary>
	/// <typeparam name="T">The type of configuration object to save.</typeparam>
	/// <param name="config">The configuration object to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A string containing the serialized configuration.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the configuration could not be saved.</exception>
	public Task<string> SaveAsync<T>(T config, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<string>(cancellationToken)
			: Task.Run(() => Save<T>(config), cancellationToken);
}
