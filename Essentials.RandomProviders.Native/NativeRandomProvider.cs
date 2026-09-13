// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.RandomProviders.Native;

using ktsu.Essentials;
using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// An <see cref="IRandomProvider"/> backed by the platform's own <see cref="Random"/>.
/// </summary>
/// <remarks>
/// <para>
/// The obvious default when nothing more specific is called for. Its statistical quality and its speed
/// are whatever the running framework provides, and on current versions both are good.
/// </para>
/// <para>
/// A seed makes the sequence repeatable within one framework version, and only within one. The sequence
/// <see cref="Random"/> produces from a given seed is explicitly undocumented and has changed between
/// .NET releases — it changed in .NET 6. Use <c>XoshiroRandomProvider</c> or <c>PcgRandomProvider</c>
/// when a seed has to reproduce the same values on another machine, another runtime or next year.
/// </para>
/// <para>
/// Not thread-safe, matching the type it wraps: concurrent calls corrupt the internal state and can
/// silently degrade the output. Give each thread its own instance, which is what the registration
/// extension arranges.
/// </para>
/// </remarks>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Wrapping System.Random is the entire purpose of this type, and the choice between a fast generator and a secure one is the caller's: CryptoRandomProvider is the implementation of the same interface for work that must be unpredictable, and the remarks above point at it. Suppressed on the type rather than on each member because every member is the same wrap.")]
[SuppressMessage("Security", "S2245:Using pseudorandom number generators (PRNGs) is security-sensitive", Justification = "This provider exists specifically to expose System.Random behind IRandomProvider, which callers select deliberately when they want the platform generator. The generator is fixed by the type's contract and cannot be substituted for a stronger one; CryptoRandomProvider is the implementation of the same interface for work that must be unpredictable")]
public sealed class NativeRandomProvider : IRandomProvider
{
	private readonly Random random;

	/// <summary>
	/// Initializes a new instance seeded from the platform's entropy source.
	/// </summary>
	public NativeRandomProvider() => random = new Random();

	/// <summary>
	/// Initializes a new instance with an explicit seed.
	/// </summary>
	/// <param name="seed">The seed. Two instances built from the same seed produce the same sequence on the same framework version.</param>
	public NativeRandomProvider(int seed) => random = new Random(seed);

	/// <inheritdoc />
	public void NextBytes(Span<byte> destination) => random.NextBytes(destination);

	/// <inheritdoc />
	public int NextInt32() => random.Next();

	/// <inheritdoc />
	public int NextInt32(int maxExclusive) => random.Next(maxExclusive);

	/// <inheritdoc />
	public int NextInt32(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);

	/// <inheritdoc />
	public double NextDouble() => random.NextDouble();
}
