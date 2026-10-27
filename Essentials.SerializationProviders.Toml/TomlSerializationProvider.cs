// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.SerializationProviders.Toml;

using ktsu.Essentials;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using Tomlyn;

/// <summary>
/// A serialization provider that uses Tomlyn for serializing and deserializing objects.
/// </summary>
/// <remarks>
/// Both directions go through Tomlyn's model binding with the same options, so a property is written and read
/// under one name: <c>DisplayName</c> becomes <c>display_name</c> on the way out and is matched by it on the way
/// back, and lists and nested objects round trip. Keys with no matching property are ignored. Deserializing
/// needs a class with a public parameterless constructor; any other type, such as a positional record or a
/// struct, deserializes to <see langword="default"/>.
/// </remarks>
public class TomlSerializationProvider : ISerializationProvider
{
	/// <summary>The options both directions bind with, so names convert the same way in each.</summary>
	private static readonly TomlModelOptions ModelOptions = new() { IgnoreMissingProperties = true };

	/// <summary>
	/// Gets the conventional file extension for TOML, including the leading dot.
	/// </summary>
	public string FileExtension => ".toml";

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

		// Tomlyn recurses without cycle detection, and the stack overflow a cycle causes cannot be
		// caught, so a self-referencing graph would take the whole process down.
		if (ContainsCycle(obj, new HashSet<object>(ReferenceComparer.Instance), depth: 0))
		{
			return false;
		}

		try
		{
			string toml = Tomlyn.Toml.FromModel(obj, ModelOptions);
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

		Func<string, T?>? bind = ModelBinder<T>.Bind;
		if (bind is null)
		{
			return default;
		}

		try
		{
			string content = Encoding.UTF8.GetString(Utf8Bom.Skip(data));
			return bind(content);
		}
		catch (TomlException)
		{
			return default;
		}
		catch (ArgumentException)
		{
			return default;
		}
		catch (InvalidOperationException)
		{
			return default;
		}
		catch (InvalidCastException)
		{
			return default;
		}
		catch (MissingMethodException)
		{
			return default;
		}
	}

	/// <summary>
	/// The deepest nesting <see cref="ContainsCycle"/> follows before treating the graph as
	/// unserializable. It only stops a value type whose properties keep producing new instances of
	/// itself, which Tomlyn would recurse into without end as well.
	/// </summary>
	private const int MaxDepth = 512;

	/// <summary>
	/// Reports whether the graph below <paramref name="value"/> reaches one of its own ancestors,
	/// following the same members Tomlyn's model writer visits: dictionary values, sequence items,
	/// and public readable instance properties that are not indexers or marked
	/// <see cref="IgnoreDataMemberAttribute"/>. An object reached twice along different branches is
	/// not a cycle; only one that appears on its own path is.
	/// </summary>
	private static bool ContainsCycle(object? value, HashSet<object> path, int depth)
	{
		if (value is null || IsScalar(value.GetType()))
		{
			return false;
		}

		if (depth > MaxDepth)
		{
			return true;
		}

		bool tracked = !value.GetType().IsValueType;
		if (tracked && !path.Add(value))
		{
			return true;
		}

		try
		{
			foreach (object? child in Children(value))
			{
				if (ContainsCycle(child, path, depth + 1))
				{
					return true;
				}
			}

			return false;
		}
		finally
		{
			if (tracked)
			{
				path.Remove(value);
			}
		}
	}

	private static IEnumerable<object?> Children(object value)
	{
		if (value is IDictionary dictionary)
		{
			foreach (DictionaryEntry entry in dictionary)
			{
				yield return entry.Value;
			}

			yield break;
		}

		if (value is IEnumerable sequence)
		{
			foreach (object? item in sequence)
			{
				yield return item;
			}

			yield break;
		}

		foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (!property.CanRead || property.GetIndexParameters().Length != 0 || property.IsDefined(typeof(IgnoreDataMemberAttribute), inherit: true))
			{
				continue;
			}

			object? child;
			try
			{
				child = property.GetValue(value);
			}
			catch (TargetInvocationException)
			{
				// A throwing getter is Tomlyn's to report; it is not a cycle.
				continue;
			}

			yield return child;
		}
	}

	private static bool IsScalar(Type type)
		=> type.IsPrimitive
		|| type.IsEnum
		|| type == typeof(string)
		|| type == typeof(decimal)
		|| type == typeof(DateTime)
		|| type == typeof(DateTimeOffset)
		|| type == typeof(TimeSpan)
		|| type == typeof(Guid)
		|| type == typeof(TomlDateTime);

	/// <summary>Compares by reference, so value-equal objects on a path are not mistaken for a cycle.</summary>
	private sealed class ReferenceComparer : IEqualityComparer<object>
	{
		internal static readonly ReferenceComparer Instance = new();

		public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

		public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
	}

	private static TModel BindModel<TModel>(string content)
		where TModel : class, new()
		=> Tomlyn.Toml.ToModel<TModel>(content, sourcePath: null, ModelOptions);

	/// <summary>
	/// Tomlyn's typed <c>ToModel</c> for <typeparamref name="T"/>, made once per type, or <see langword="null"/>
	/// when <typeparamref name="T"/> is not a class with a public parameterless constructor, which Tomlyn needs to
	/// create it. A delegate rather than a reflected call each time, so a binding failure arrives as itself rather
	/// than wrapped in a <see cref="TargetInvocationException"/>.
	/// </summary>
	private static class ModelBinder<T>
	{
		internal static readonly Func<string, T?>? Bind = Create();

		private static Func<string, T?>? Create()
		{
			Type type = typeof(T);
			if (!type.IsClass || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
			{
				return null;
			}

			MethodInfo method = typeof(TomlSerializationProvider)
				.GetMethod(nameof(BindModel), BindingFlags.NonPublic | BindingFlags.Static)!
				.MakeGenericMethod(type);
#if NET5_0_OR_GREATER
			return method.CreateDelegate<Func<string, T?>>();
#else
			return (Func<string, T?>)method.CreateDelegate(typeof(Func<string, T?>));
#endif
		}
	}
}
