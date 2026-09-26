// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// The worst-case compressed size for the deflate family, shared by the Deflate, Gzip and ZLib providers.
/// </summary>
/// <remarks>
/// Linked into each provider project rather than placed in the interfaces package, following
/// <c>HmacKeyedHashCore</c>, and internal for the same reason: every package compiles its own copy.
/// </remarks>
internal static class DeflateBound
{
	/// <summary>
	/// Gets zlib's <c>deflateBound</c> for the default parameters, plus a container's header and trailer.
	/// </summary>
	/// <remarks>
	/// zlib closes a block each time its literal buffer fills, roughly every 16 KB, so incompressible
	/// input pays a stored-block header far more often than once per 64 KB. Computed in <see cref="long"/>
	/// so a large <paramref name="sourceLength"/> cannot overflow into a small bound.
	/// </remarks>
	/// <param name="sourceLength">The length of the data to compress.</param>
	/// <param name="containerOverhead">The bytes the container adds around the raw deflate stream: 0 for raw deflate, 6 for zlib, 18 for gzip.</param>
	/// <returns>The buffer size that guarantees compression succeeds.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The bound for <paramref name="sourceLength"/> exceeds <see cref="int.MaxValue"/>.</exception>
	public static int GetMaxCompressedLength(int sourceLength, int containerOverhead)
	{
		long length = sourceLength;
		long bound = length + (length >> 12) + (length >> 14) + (length >> 25) + 13 + containerOverhead;
		return bound <= int.MaxValue
			? (int)bound
			: throw new ArgumentOutOfRangeException(nameof(sourceLength), sourceLength, "The compressed length bound exceeds the largest possible buffer.");
	}
}
