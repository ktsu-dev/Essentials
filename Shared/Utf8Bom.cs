// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// Strips a leading UTF-8 byte order mark, shared by the serialization providers' byte overloads.
/// </summary>
/// <remarks>
/// <c>File.ReadAllBytes</c> on a file saved by Notepad, Visual Studio or PowerShell 5 starts with
/// <c>EF BB BF</c>. System.Text.Json's UTF-8 span overload rejects it, and <c>Encoding.UTF8.GetString</c>
/// keeps it as U+FEFF, which the Newtonsoft and TOML parsers reject and YamlDotNet folds into the first key.
/// Text readers already strip it, so only the byte overloads need this. Linked into each provider project
/// rather than placed in the interfaces package, following <c>DeflateBound</c>.
/// </remarks>
internal static class Utf8Bom
{
	private static ReadOnlySpan<byte> Preamble => [0xEF, 0xBB, 0xBF];

	/// <summary>
	/// Gets <paramref name="data"/> without its leading UTF-8 byte order mark, if it has one.
	/// </summary>
	/// <param name="data">The UTF-8 encoded data.</param>
	/// <returns>The data after the byte order mark, or <paramref name="data"/> unchanged.</returns>
	public static ReadOnlySpan<byte> Skip(ReadOnlySpan<byte> data)
		=> data.StartsWith(Preamble) ? data[Preamble.Length..] : data;
}
