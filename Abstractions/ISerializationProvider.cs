// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a contract for serialization providers that can serialize and deserialize objects.
/// Supports JSON, YAML, TOML, and other text-based serialization formats.
/// </summary>
[SuppressMessage("Maintainability", "CA1510:Use ArgumentNullException throw helper", Justification = "Not available in netstandard")]
public interface ISerializationProvider
{
	/// <summary>
	/// Tries to serialize the specified object into the destination buffer without allocating.
	/// </summary>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="writer">The writer to write the serialized data to.</param>
	/// <returns>True if the serialization was successful, false otherwise.</returns>
	public bool TrySerialize(object obj, TextWriter writer);

	/// <summary>
	/// Tries to deserialize the specified data into a specific type.
	/// </summary>
	/// <typeparam name="T">The type to deserialize into.</typeparam>
	/// <param name="data">The data to deserialize.</param>
	/// <returns>The deserialized object, or default if deserialization fails.</returns>
	public T? Deserialize<T>(ReadOnlySpan<byte> data);

	/// <summary>
	/// Tries to deserialize the specified data into a specific type from a text reader.
	/// </summary>
	/// <typeparam name="T">The type to deserialize into.</typeparam>
	/// <param name="reader">The reader to read the serialized data from.</param>
	/// <returns>The deserialized object, or default if deserialization fails.</returns>
	public T? Deserialize<T>(TextReader reader)
	{
		Ensure.NotNull(reader);

		string data = reader.ReadToEnd();
		byte[] bytes = Encoding.UTF8.GetBytes(data);
		return Deserialize<T>(bytes);
	}

	/// <summary>
	/// Deserializes the specified string content into a specific type.
	/// </summary>
	/// <typeparam name="T">The type to deserialize into.</typeparam>
	/// <param name="content">The string containing the serialized data.</param>
	/// <returns>The deserialized object, or default if deserialization fails.</returns>
	public T? Deserialize<T>(string content)
	{
		Ensure.NotNull(content);

		using StringReader reader = new(content);
		return Deserialize<T>(reader);
	}

	/// <summary>
	/// Tries to serialize the specified object into the destination buffer asynchronously.
	/// </summary>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="writer">The writer to write the serialized data to.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the serialization was successful, false otherwise.</returns>
	public Task<bool> TrySerializeAsync(object obj, TextWriter writer, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => TrySerialize(obj, writer), cancellationToken);

	/// <summary>
	/// Tries to deserialize the specified data into a specific type asynchronously.
	/// </summary>
	/// <typeparam name="T">The type to deserialize into.</typeparam>
	/// <param name="data">The data to deserialize.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The deserialized object, or default if deserialization fails.</returns>
	public Task<T?> DeserializeAsync<T>(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Deserialize<T>(data.Span), cancellationToken);

	/// <summary>
	/// Tries to deserialize the specified data into a specific type asynchronously from a text reader.
	/// </summary>
	/// <typeparam name="T">The type to deserialize into.</typeparam>
	/// <param name="reader">The reader to read the serialized data from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The deserialized object, or default if deserialization fails.</returns>
	public Task<T?> DeserializeAsync<T>(TextReader reader, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Deserialize<T>(reader), cancellationToken);

	/// <summary>
	/// Deserializes the specified string content into a specific type asynchronously.
	/// </summary>
	/// <typeparam name="T">The type to deserialize into.</typeparam>
	/// <param name="content">The string containing the serialized data.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The deserialized object, or default if deserialization fails.</returns>
	public Task<T?> DeserializeAsync<T>(string content, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Deserialize<T>(content), cancellationToken);

	/// <summary>
	/// Serializes the specified object.
	/// </summary>
	/// <param name="obj">The object to serialize.</param>
	/// <returns>A string containing the serialized data.</returns>
	public string Serialize(object obj)
	{
		using StringWriter writer = new();
		TrySerialize(obj, writer);
		return writer.ToString();
	}

	/// <summary>
	/// Serializes the specified object.
	/// </summary>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="writer">The writer to write the serialized data to.</param>
	public void Serialize(object obj, TextWriter writer)
		=> TrySerialize(obj, writer);

	/// <summary>
	/// Serializes the specified object asynchronously.
	/// </summary>
	/// <param name="obj">The object to serialize.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A string containing the serialized data.</returns>
	public Task<string> SerializeAsync(object obj, CancellationToken cancellationToken = default)
		=> ProviderHelpers.RunAsync(() => Serialize(obj), cancellationToken);
}
