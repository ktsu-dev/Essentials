// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.RandomProviders.Xoshiro;

using ktsu.Essentials;
using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

/// <summary>
/// An <see cref="IRandomProvider"/> implementing xoshiro256**, a fast seedable generator with a
/// sequence that is fixed by this code rather than by the running framework.
/// </summary>
/// <remarks>
/// <para>
/// Use this when a seed has to reproduce the same values everywhere — a simulation whose run must be
/// replayable, a procedurally generated world that has to look the same on every machine, a test that
/// asserts on generated data. The algorithm is written out here, so the sequence is a property of the
/// package version and not of the framework underneath it.
/// </para>
/// <para>
/// Each draw is five exclusive-ors, two rotations and a shift over 256 bits of state: no memory
/// traffic, no branches, and faster than reading the same number of bytes from anywhere else. The
/// period is 2^256 - 1, and the generator passes the standard statistical batteries. It is not
/// cryptographically secure — observing a few outputs is enough to recover the state and predict the
/// rest, so anything that must stay unpredictable belongs to <c>CryptoRandomProvider</c> instead.
/// </para>
/// <para>
/// Not thread-safe. Sharing one instance across threads corrupts the state, and would make the sequence
/// depend on thread scheduling, which defeats the reason to choose a seedable generator at all. Give
/// each thread its own instance with its own seed.
/// </para>
/// </remarks>
public sealed class XoshiroRandomProvider : IRandomProvider
{
	/// <summary>The odd increment of the SplitMix64 mixer used to expand a single seed into the state, the 64-bit golden ratio.</summary>
	private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;

	private ulong state0;
	private ulong state1;
	private ulong state2;
	private ulong state3;

	/// <summary>
	/// Initializes a new instance seeded from the operating system's cryptographic random number generator.
	/// </summary>
	/// <remarks>
	/// Seeded from the system entropy source rather than from a clock, so instances created in the same
	/// tick still diverge.
	/// </remarks>
	public XoshiroRandomProvider()
	{
		Span<byte> seed = stackalloc byte[sizeof(ulong)];
		RandomNumberGenerator.Fill(seed);
		Initialize(BinaryPrimitives.ReadUInt64LittleEndian(seed));
	}

	/// <summary>
	/// Initializes a new instance with an explicit seed.
	/// </summary>
	/// <remarks>
	/// The seed is expanded into the 256-bit state with SplitMix64, as the reference implementation
	/// prescribes. A generator started from consecutive seeds still produces unrelated sequences,
	/// because the mixer decorrelates them before the first draw.
	/// </remarks>
	/// <param name="seed">The seed. Two instances built from the same seed produce the same sequence, on any platform and any framework version.</param>
	public XoshiroRandomProvider(ulong seed) => Initialize(seed);

	/// <inheritdoc />
	public ulong NextUInt64()
	{
		ulong result = RotateLeft(state1 * 5UL, 7) * 9UL;
		ulong shifted = state1 << 17;

		state2 ^= state0;
		state3 ^= state1;
		state1 ^= state2;
		state0 ^= state3;
		state2 ^= shifted;
		state3 = RotateLeft(state3, 45);

		return result;
	}

	/// <inheritdoc />
	public uint NextUInt32() => (uint)(NextUInt64() >> 32);

	/// <inheritdoc />
	public void NextBytes(Span<byte> destination)
	{
		int written = 0;
		while (destination.Length - written >= sizeof(ulong))
		{
			BinaryPrimitives.WriteUInt64LittleEndian(destination[written..], NextUInt64());
			written += sizeof(ulong);
		}

		if (written < destination.Length)
		{
			Span<byte> tail = stackalloc byte[sizeof(ulong)];
			BinaryPrimitives.WriteUInt64LittleEndian(tail, NextUInt64());
			tail[..(destination.Length - written)].CopyTo(destination[written..]);
		}
	}

	/// <summary>
	/// Rotates a value left by the given number of bits.
	/// </summary>
	/// <param name="value">The value to rotate.</param>
	/// <param name="count">The number of bits to rotate by, between 1 and 63.</param>
	/// <returns>The rotated value.</returns>
	private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));

	/// <summary>
	/// Expands a seed into the generator state with the SplitMix64 mixer.
	/// </summary>
	/// <param name="seed">The seed to expand.</param>
	private void Initialize(ulong seed)
	{
		ulong mixer = seed;
		state0 = SplitMix64(ref mixer);
		state1 = SplitMix64(ref mixer);
		state2 = SplitMix64(ref mixer);
		state3 = SplitMix64(ref mixer);
	}

	/// <summary>
	/// Advances the SplitMix64 mixer and returns its next output.
	/// </summary>
	/// <param name="state">The mixer state, advanced in place.</param>
	/// <returns>The next 64 bits of seed material.</returns>
	private static ulong SplitMix64(ref ulong state)
	{
		unchecked
		{
			state += SplitMixIncrement;
			ulong z = state;
			z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
			z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
			return z ^ (z >> 31);
		}
	}
}
