// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;
using System.Linq;

/// <summary>
/// Utility methods shared across persistence provider implementations.
/// </summary>
public static class PersistenceProviderUtilities
{
	/// <summary>
	/// Converts a string to a safe filename by replacing invalid characters with underscores.
	/// </summary>
	/// <param name="input">The input string to make safe for use as a filename.</param>
	/// <returns>A safe filename string.</returns>
	public static string GetSafeFileName(string input)
	{
		// Using predefined invalid characters that are consistent across platforms
		char[] invalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
		return string.Concat(input.Select(c => invalidChars.Contains(c) ? '_' : c));
	}

	/// <summary>
	/// Attempts to convert a string value to the specified key type.
	/// </summary>
	/// <typeparam name="TKey">The target key type.</typeparam>
	/// <param name="value">The string value to convert.</param>
	/// <returns>The converted key value, or default if conversion fails.</returns>
	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally catching all exceptions for robust conversion")]
	public static TKey? ConvertToKey<TKey>(string value) where TKey : notnull
	{
		try
		{
			if (typeof(TKey) == typeof(string))
			{
				return (TKey)(object)value;
			}
			if (typeof(TKey) == typeof(Guid))
			{
				return Guid.TryParse(value, out Guid guid) ? (TKey)(object)guid : default;
			}
			if (typeof(TKey) == typeof(int))
			{
				return int.TryParse(value, out int intValue) ? (TKey)(object)intValue : default;
			}

			// For other types, try using Convert.ChangeType
			return (TKey)Convert.ChangeType(value, typeof(TKey));
		}
		catch (Exception)
		{
			// Intentionally catching all exceptions for robust key conversion
			return default;
		}
	}
}
