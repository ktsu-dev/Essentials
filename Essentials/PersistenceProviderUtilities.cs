// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Utility methods shared across persistence provider implementations.
/// </summary>
public static class PersistenceProviderUtilities
{
	/// <summary>
	/// Characters that cannot appear in a filename on at least one supported platform, plus the escape
	/// character itself so encoding stays unambiguous.
	/// </summary>
	private static readonly char[] ReservedCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '%'];

	/// <summary>Device names Windows reserves regardless of extension.</summary>
	private static readonly string[] ReservedDeviceNames =
	[
		"CON", "PRN", "AUX", "NUL",
		"COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
		"LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
	];

	/// <summary>
	/// The longest encoded name kept verbatim. Longer names are truncated and disambiguated with a hash,
	/// which keeps full paths clear of platform length limits.
	/// </summary>
	private const int MaxEncodedLength = 100;

	/// <summary>Separates the truncated prefix from its hash. Never produced by encoding.</summary>
	private const char TruncationMarker = '~';

	/// <summary>
	/// Converts a key to a filename that is safe on every supported platform.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Reserved characters are percent-encoded rather than replaced with a single substitute, so distinct
	/// keys always produce distinct filenames. Replacing them all with <c>_</c> — as earlier versions did —
	/// mapped <c>a/b</c>, <c>a\b</c> and <c>a_b</c> onto one file, silently overwriting unrelated entries.
	/// </para>
	/// <para>
	/// Encoding is reversible via <see cref="GetKeyFromFileName"/>, except for keys long enough to be
	/// truncated. Those get a hash suffix to stay distinct, but cannot be read back from the filename.
	/// </para>
	/// </remarks>
	/// <param name="input">The key text to convert.</param>
	/// <returns>A filename-safe representation of <paramref name="input"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="input"/> is null, empty, or whitespace.</exception>
	public static string GetSafeFileName(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			throw new ArgumentException("Key cannot be null, empty, or whitespace.", nameof(input));
		}

		// Windows also reserves a set of device names, and rejects names ending in a dot or space.
		// Both are handled positionally in the same pass rather than by patching the result afterwards.
		bool escapeFirst = IsReservedDeviceName(input);

		StringBuilder builder = new(input.Length + 8);
		for (int i = 0; i < input.Length; i++)
		{
			char c = input[i];
			bool isLast = i == input.Length - 1;

			if (Array.IndexOf(ReservedCharacters, c) >= 0
				|| char.IsControl(c)
				|| (isLast && c is '.' or ' ')
				|| (i == 0 && escapeFirst))
			{
				builder.Append(Escape(c));
			}
			else
			{
				builder.Append(c);
			}
		}

		string encoded = builder.ToString();
		return encoded.Length > MaxEncodedLength ? Truncate(encoded) : encoded;
	}

	/// <summary>
	/// Recovers the original key text from a filename produced by <see cref="GetSafeFileName"/>.
	/// </summary>
	/// <param name="fileName">The filename, without extension.</param>
	/// <returns>The original key text, or null if the name was truncated or is not valid encoded output.</returns>
	public static string? GetKeyFromFileName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName) || fileName.Contains(TruncationMarker))
		{
			// Truncated names intentionally discard part of the key and cannot be recovered.
			return null;
		}

		StringBuilder builder = new(fileName.Length);
		for (int i = 0; i < fileName.Length; i++)
		{
			if (fileName[i] != '%')
			{
				builder.Append(fileName[i]);
				continue;
			}

			if (i + 2 >= fileName.Length
				|| !int.TryParse(fileName.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
			{
				return null;
			}

			builder.Append((char)value);
			i += 2;
		}

		return builder.ToString();
	}

	/// <summary>
	/// Attempts to convert a string value to the specified key type.
	/// </summary>
	/// <remarks>
	/// Replaces the earlier method that returned <c>default</c> on failure, which turned an unreadable
	/// filename into a valid-looking key such as <c>0</c> or <see cref="Guid.Empty"/>.
	/// </remarks>
	/// <typeparam name="TKey">The target key type.</typeparam>
	/// <param name="value">The string value to convert.</param>
	/// <param name="key">The converted key, when conversion succeeds.</param>
	/// <returns>True if the value was converted, false otherwise.</returns>
	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Conversion failure of arbitrary key types must be reported as false, not thrown")]
	public static bool TryConvertToKey<TKey>(string value, out TKey key) where TKey : notnull
	{
		key = default!;

		if (value is null)
		{
			return false;
		}

		try
		{
			if (typeof(TKey) == typeof(string))
			{
				key = (TKey)(object)value;
				return true;
			}

			if (typeof(TKey) == typeof(Guid))
			{
				if (!Guid.TryParse(value, out Guid guid))
				{
					return false;
				}

				key = (TKey)(object)guid;
				return true;
			}

			if (typeof(TKey) == typeof(int))
			{
				if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
				{
					return false;
				}

				key = (TKey)(object)intValue;
				return true;
			}

			key = (TKey)Convert.ChangeType(value, typeof(TKey), CultureInfo.InvariantCulture);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string Escape(char c) => string.Format(CultureInfo.InvariantCulture, "%{0:X2}", (int)c);

	private static bool IsReservedDeviceName(string value)
	{
		// Windows treats "CON.json" as the console device, so the check ignores any extension.
		int dot = value.IndexOf('.');
		string stem = dot < 0 ? value : value[..dot];

		return Array.Exists(ReservedDeviceNames, name => string.Equals(name, stem, StringComparison.OrdinalIgnoreCase));
	}

	private static string Truncate(string encoded)
	{
		string hash = Fnv1a64(encoded).ToString("x16", CultureInfo.InvariantCulture);
		int prefixLength = MaxEncodedLength - hash.Length - 1;

		// Never split a percent-escape across the truncation boundary.
		while (prefixLength > 0 && IsInsideEscape(encoded, prefixLength))
		{
			prefixLength--;
		}

		return $"{encoded[..prefixLength]}{TruncationMarker}{hash}";
	}

	private static bool IsInsideEscape(string encoded, int cut)
		=> (cut >= 1 && encoded[cut - 1] == '%') || (cut >= 2 && encoded[cut - 2] == '%');

	/// <summary>
	/// FNV-1a, chosen because it is stable across processes and runtimes.
	/// <see cref="string.GetHashCode()"/> is randomised per process, so it cannot name a file.
	/// </summary>
	/// <param name="value">The value to hash.</param>
	/// <returns>A stable 64-bit hash.</returns>
	private static ulong Fnv1a64(string value)
	{
		const ulong offsetBasis = 14695981039346656037;
		const ulong prime = 1099511628211;

		ulong hash = offsetBasis;
		foreach (byte b in Encoding.UTF8.GetBytes(value))
		{
			hash ^= b;
			hash *= prime;
		}

		return hash;
	}
}
