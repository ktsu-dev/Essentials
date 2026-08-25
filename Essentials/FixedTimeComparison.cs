// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Security.Cryptography;

/// <summary>
/// Compares byte sequences in an amount of time that does not depend on their contents.
/// </summary>
/// <remarks>
/// Comparing an authentication tag with <c>==</c>, <c>SequenceEqual</c>, or any comparison that
/// returns early on the first differing byte leaks where the difference is. An attacker who can
/// measure that can recover a valid tag one byte at a time. Prefer
/// <see cref="IKeyedHashProvider.Verify"/>, which computes and compares in one step; use this only
/// when the tag to compare against was produced elsewhere.
/// <para>
/// The member is named <see cref="FixedTimeEquals"/> rather than <c>Equals</c> because a static
/// class inherits <see cref="object.Equals(object, object)"/>. That overload accepts any two
/// arguments, so a call spelled <c>FixedTimeComparison.Equals(a, b)</c> would compile and silently
/// resolve to boxed reference equality for anything that does not implicitly convert to
/// <see cref="ReadOnlySpan{T}"/>, never reaching the fixed-time path.
/// </para>
/// </remarks>
public static class FixedTimeComparison
{
	/// <summary>
	/// Determines whether two byte sequences are equal, in a time that does not vary with their contents.
	/// </summary>
	/// <param name="left">The first sequence.</param>
	/// <param name="right">The second sequence.</param>
	/// <returns>True if the sequences have the same length and contents, false otherwise.</returns>
	public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		=> CryptographicOperations.FixedTimeEquals(left, right);
}
