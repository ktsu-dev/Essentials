// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.RandomProviders.Pcg;

using ktsu.Essentials;
using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

/// <summary>
/// An <see cref="IRandomProvider"/> implementing PCG-XSH-RR, a seedable generator whose sequence is
/// fixed by this code rather than by the running framework.
/// </summary>
/// <remarks>
/// <para>
/// Like <c>XoshiroRandomProvider</c>, this reproduces a seeded sequence identically everywhere. What it
/// adds is the stream parameter: two generators given the same seed but different stream numbers walk
/// different sequences, which is how a simulation gives each of its actors an independent generator
/// from one seed without them ever colliding.
/// </para>
/// <para>
/// The state is 64 bits advanced by a linear congruential step, and the output is a permutation of the
/// high bits — an xorshift followed by a rotation whose distance is itself taken from the state. That
/// output function is what lifts an LCG, whose low bits are notoriously weak, to passing the standard
/// statistical batteries. Half the state size of xoshiro256**, and correspondingly a shorter period of
/// 2^64 per stream.
/// </para>
/// <para>
/// Not cryptographically secure; use <c>CryptoRandomProvider</c> where the output must be
/// unpredictable. Not thread-safe either: give each thread its own instance, or better, its own stream.
/// </para>
/// </remarks>
public sealed class PcgRandomProvider : IRandomProvider
{
	/// <summary>The multiplier of the underlying 64-bit linear congruential generator, from the reference implementation.</summary>
	private const ulong Multiplier = 6364136223846793005UL;

	/// <summary>The stream used when none is given.</summary>
	private const ulong DefaultStream = 1442695040888963407UL;

	private readonly ulong increment;
	private ulong state;

	/// <summary>
	/// Initializes a new instance seeded from the operating system's cryptographic random number generator.
	/// </summary>
	public PcgRandomProvider()
		: this(RandomSeed(), DefaultStream)
	{
	}

	/// <summary>
	/// Initializes a new instance with an explicit seed on the default stream.
	/// </summary>
	/// <param name="seed">The seed. Two instances built from the same seed produce the same sequence, on any platform and any framework version.</param>
	public PcgRandomProvider(ulong seed)
		: this(seed, DefaultStream)
	{
	}

	/// <summary>
	/// Initializes a new instance with an explicit seed on an explicit stream.
	/// </summary>
	/// <param name="seed">The seed, which fixes the starting point within the stream.</param>
	/// <param name="stream">The stream number, which selects one of 2^63 distinct sequences.</param>
	public PcgRandomProvider(ulong seed, ulong stream)
	{
		// The increment must be odd for the congruential step to cover the whole state space, which is
		// what the shift and set of the low bit guarantees for any stream number.
		increment = (stream << 1) | 1UL;
		state = 0UL;
		Step();
		state = unchecked(state + seed);
		Step();
	}

	/// <inheritdoc />
	public uint NextUInt32()
	{
		ulong previous = state;
		Step();

		uint xorshifted = (uint)(((previous >> 18) ^ previous) >> 27);
		int rotation = (int)(previous >> 59);
		return RotateRight(xorshifted, rotation);
	}

	/// <inheritdoc />
	public ulong NextUInt64()
	{
		ulong high = NextUInt32();
		ulong low = NextUInt32();
		return (high << 32) | low;
	}

	/// <inheritdoc />
	public void NextBytes(Span<byte> destination)
	{
		int written = 0;
		while (destination.Length - written >= sizeof(uint))
		{
			BinaryPrimitives.WriteUInt32LittleEndian(destination[written..], NextUInt32());
			written += sizeof(uint);
		}

		if (written < destination.Length)
		{
			Span<byte> tail = stackalloc byte[sizeof(uint)];
			BinaryPrimitives.WriteUInt32LittleEndian(tail, NextUInt32());
			tail[..(destination.Length - written)].CopyTo(destination[written..]);
		}
	}

	/// <summary>
	/// Draws a seed from the operating system's cryptographic random number generator.
	/// </summary>
	/// <returns>A seed with no relationship to the wall clock, so instances created in the same tick still diverge.</returns>
	private static ulong RandomSeed()
	{
		Span<byte> seed = stackalloc byte[sizeof(ulong)];
		RandomNumberGenerator.Fill(seed);
		return BinaryPrimitives.ReadUInt64LittleEndian(seed);
	}

	/// <summary>
	/// Rotates a value right by the given number of bits.
	/// </summary>
	/// <param name="value">The value to rotate.</param>
	/// <param name="count">The number of bits to rotate by, between 0 and 31.</param>
	/// <returns>The rotated value.</returns>
	private static uint RotateRight(uint value, int count) => (value >> count) | (value << ((-count) & 31));

	/// <summary>
	/// Advances the underlying linear congruential generator by one step.
	/// </summary>
	private void Step() => state = unchecked((state * Multiplier) + increment);
}
