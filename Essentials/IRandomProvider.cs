// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;

/// <summary>
/// Interface for random providers that supply uniformly distributed random values.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NextBytes(Span{byte})"/> is the only member an implementation must supply; everything
/// else has a default implementation built on it. An implementation backed by a word-at-a-time
/// generator should also declare <see cref="NextUInt64"/> and <see cref="NextUInt32"/>, because every
/// other default draws through those two rather than through the byte buffer.
/// </para>
/// <para>
/// Nothing here is asynchronous, and that is deliberate rather than an omission. The other provider
/// interfaces offer async variants because they front I/O; a draw from a generator is a handful of
/// arithmetic instructions over in-memory state, so scheduling one on the thread pool would cost
/// orders of magnitude more than the work itself.
/// </para>
/// <para>
/// Thread safety is an implementation concern. A generator that carries state cannot be shared across
/// threads without external synchronisation and each implementation documents its own guarantee.
/// </para>
/// <para>
/// The range-limited integer methods are free of modulo bias: they reject the values that would make
/// the mapping uneven rather than folding them into the low end of the range.
/// </para>
/// </remarks>
public interface IRandomProvider
{
	/// <summary>
	/// Fills the destination buffer with uniformly distributed random bytes.
	/// </summary>
	/// <param name="destination">The buffer to fill. Every byte in it is overwritten.</param>
	public void NextBytes(Span<byte> destination);

	/// <summary>
	/// Returns a new array of uniformly distributed random bytes.
	/// </summary>
	/// <param name="count">The number of bytes to generate.</param>
	/// <returns>An array of <paramref name="count"/> random bytes.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
	public byte[] NextBytes(int count)
	{
		Ensure.NotNegative(count);

		byte[] buffer = new byte[count];
		NextBytes(buffer);
		return buffer;
	}

	/// <summary>
	/// Returns a uniformly distributed 32-bit unsigned integer.
	/// </summary>
	/// <returns>A value drawn uniformly from the whole range of <see cref="uint"/>.</returns>
	public uint NextUInt32()
	{
		Span<byte> buffer = stackalloc byte[sizeof(uint)];
		NextBytes(buffer);
		return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
	}

	/// <summary>
	/// Returns a uniformly distributed 64-bit unsigned integer.
	/// </summary>
	/// <returns>A value drawn uniformly from the whole range of <see cref="ulong"/>.</returns>
	public ulong NextUInt64()
	{
		Span<byte> buffer = stackalloc byte[sizeof(ulong)];
		NextBytes(buffer);
		return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
	}

	/// <summary>
	/// Returns a non-negative random integer.
	/// </summary>
	/// <returns>A value in the range [0, <see cref="int.MaxValue"/>).</returns>
	public int NextInt32() => NextInt32(0, int.MaxValue);

	/// <summary>
	/// Returns a non-negative random integer below the specified bound.
	/// </summary>
	/// <param name="maxExclusive">The exclusive upper bound. Must not be negative.</param>
	/// <returns>A value in the range [0, <paramref name="maxExclusive"/>), or zero when the bound is zero.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is negative.</exception>
	public int NextInt32(int maxExclusive)
	{
		Ensure.NotNegative(maxExclusive);

		return NextInt32(0, maxExclusive);
	}

	/// <summary>
	/// Returns a random integer within the specified range.
	/// </summary>
	/// <param name="minInclusive">The inclusive lower bound.</param>
	/// <param name="maxExclusive">The exclusive upper bound. Must not be below <paramref name="minInclusive"/>.</param>
	/// <returns>
	/// A value in the range [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>), or
	/// <paramref name="minInclusive"/> when the two bounds are equal.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is below <paramref name="minInclusive"/>.</exception>
	public int NextInt32(int minInclusive, int maxExclusive)
	{
		Ensure.NotLessThan(maxExclusive, minInclusive);

		uint range = (uint)((long)maxExclusive - minInclusive);
		return range == 0 ? minInclusive : minInclusive + (int)RandomHelpers.NextBoundedUInt32(this, range);
	}

	/// <summary>
	/// Returns a non-negative random 64-bit integer.
	/// </summary>
	/// <returns>A value in the range [0, <see cref="long.MaxValue"/>).</returns>
	public long NextInt64() => NextInt64(0, long.MaxValue);

	/// <summary>
	/// Returns a non-negative random 64-bit integer below the specified bound.
	/// </summary>
	/// <param name="maxExclusive">The exclusive upper bound. Must not be negative.</param>
	/// <returns>A value in the range [0, <paramref name="maxExclusive"/>), or zero when the bound is zero.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is negative.</exception>
	public long NextInt64(long maxExclusive)
	{
		Ensure.NotNegative(maxExclusive);

		return NextInt64(0, maxExclusive);
	}

	/// <summary>
	/// Returns a random 64-bit integer within the specified range.
	/// </summary>
	/// <param name="minInclusive">The inclusive lower bound.</param>
	/// <param name="maxExclusive">The exclusive upper bound. Must not be below <paramref name="minInclusive"/>.</param>
	/// <returns>
	/// A value in the range [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>), or
	/// <paramref name="minInclusive"/> when the two bounds are equal.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is below <paramref name="minInclusive"/>.</exception>
	public long NextInt64(long minInclusive, long maxExclusive)
	{
		Ensure.NotLessThan(maxExclusive, minInclusive);

		ulong range = unchecked((ulong)maxExclusive - (ulong)minInclusive);
		return range == 0 ? minInclusive : unchecked(minInclusive + (long)RandomHelpers.NextBoundedUInt64(this, range));
	}

	/// <summary>
	/// Returns a uniformly distributed double in the half-open unit interval.
	/// </summary>
	/// <remarks>
	/// Built from the top 53 bits of a 64-bit draw, which is the full precision a <see cref="double"/>
	/// can carry on [0, 1).
	/// </remarks>
	/// <returns>A value in the range [0, 1).</returns>
	public double NextDouble() => (NextUInt64() >> 11) * RandomHelpers.UnitScale;

	/// <summary>
	/// Returns a uniformly distributed double in the open unit interval, excluding both endpoints.
	/// </summary>
	/// <remarks>
	/// Inverse-transform sampling needs this: the quantile function of an unbounded distribution is
	/// infinite at 0 and at 1, so a draw that can return either endpoint would produce an infinity
	/// rather than a sample. The 53-bit draw is offset by half a step, which excludes both ends
	/// without distorting the spacing of the values in between.
	/// </remarks>
	/// <returns>A value in the range (0, 1).</returns>
	public double NextDoubleExclusive() => ((NextUInt64() >> 11) + 0.5) * RandomHelpers.UnitScale;

	/// <summary>
	/// Returns a uniformly distributed double within the specified range.
	/// </summary>
	/// <param name="minInclusive">The inclusive lower bound. Must be finite.</param>
	/// <param name="maxExclusive">The exclusive upper bound. Must be finite and not below <paramref name="minInclusive"/>.</param>
	/// <returns>A value in the range [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">A bound is not finite, or <paramref name="maxExclusive"/> is below <paramref name="minInclusive"/>.</exception>
	public double NextDouble(double minInclusive, double maxExclusive)
	{
		RandomHelpers.EnsureFinite(minInclusive, nameof(minInclusive));
		RandomHelpers.EnsureFinite(maxExclusive, nameof(maxExclusive));
		Ensure.NotLessThan(maxExclusive, minInclusive);

		return minInclusive + (NextDouble() * (maxExclusive - minInclusive));
	}

	/// <summary>
	/// Returns a uniformly distributed single in the half-open unit interval.
	/// </summary>
	/// <returns>A value in the range [0, 1).</returns>
	public float NextSingle() => (NextUInt32() >> 8) * RandomHelpers.UnitScaleSingle;

	/// <summary>
	/// Returns true or false with equal probability.
	/// </summary>
	/// <returns>True or false, each with probability one half.</returns>
	public bool NextBoolean() => (NextUInt32() & 1u) != 0u;

	/// <summary>
	/// Performs a Bernoulli trial: returns true with the given probability.
	/// </summary>
	/// <param name="probability">The probability of returning true, in the range [0, 1].</param>
	/// <returns>True with probability <paramref name="probability"/>, false otherwise.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is outside [0, 1] or is not a number.</exception>
	public bool NextBoolean(double probability)
	{
		RandomHelpers.EnsureProbability(probability, nameof(probability));

		return NextDouble() < probability;
	}

	/// <summary>
	/// Shuffles the list in place, so that every ordering of its elements is equally likely.
	/// </summary>
	/// <remarks>Uses the Fisher-Yates algorithm, which touches each element exactly once.</remarks>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="items">The list to shuffle.</param>
	/// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
	public void Shuffle<T>(IList<T> items)
	{
		Ensure.NotNull(items);

		for (int i = items.Count - 1; i > 0; i--)
		{
			int j = NextInt32(i + 1);
			(items[i], items[j]) = (items[j], items[i]);
		}
	}

	/// <summary>
	/// Returns one element of the list, chosen uniformly at random.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="items">The list to choose from. Must not be empty.</param>
	/// <returns>The chosen element.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
	/// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
	public T Choose<T>(IReadOnlyList<T> items)
	{
		Ensure.NotNull(items);
		if (items.Count == 0)
		{
			throw new ArgumentException("Cannot choose from an empty collection.", nameof(items));
		}

		return items[NextInt32(items.Count)];
	}

	/// <summary>
	/// Returns one element of the list, chosen with probability proportional to its weight.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="items">The list to choose from. Must not be empty.</param>
	/// <param name="weights">The relative weight of each element. Must be the same length as <paramref name="items"/>, non-negative, and not all zero.</param>
	/// <returns>The chosen element.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="weights"/> is null.</exception>
	/// <exception cref="ArgumentException">The lists differ in length, are empty, or the weights are invalid.</exception>
	public T Choose<T>(IReadOnlyList<T> items, IReadOnlyList<double> weights)
	{
		Ensure.NotNull(items);
		Ensure.NotNull(weights);
		if (items.Count == 0)
		{
			throw new ArgumentException("Cannot choose from an empty collection.", nameof(items));
		}

		if (items.Count != weights.Count)
		{
			throw new ArgumentException($"Expected {items.Count} weights to match the collection, but got {weights.Count}.", nameof(weights));
		}

		double total = RandomHelpers.SumWeights(weights, nameof(weights));
		double target = NextDouble() * total;
		double cumulative = 0.0;
		for (int i = 0; i < items.Count; i++)
		{
			cumulative += weights[i];
			if (target < cumulative)
			{
				return items[i];
			}
		}

		// Only reachable when rounding leaves the target at or above the accumulated total, which the
		// last element with a non-zero weight owns.
		return items[RandomHelpers.LastNonZeroWeightIndex(weights)];
	}

	/// <summary>
	/// Returns a random sample of distinct elements drawn without replacement.
	/// </summary>
	/// <remarks>
	/// Every subset of the requested size is equally likely, and so is every ordering of the one
	/// returned. The source is not modified; the draw runs over a private copy.
	/// </remarks>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="source">The population to draw from.</param>
	/// <param name="count">The number of elements to draw. Must not exceed the size of the population.</param>
	/// <returns>The drawn elements, in the order they were drawn.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or larger than the population.</exception>
	public IReadOnlyList<T> Sample<T>(IReadOnlyList<T> source, int count)
	{
		Ensure.NotNull(source);
		Ensure.NotNegative(count);
		if (count > source.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(count), count, $"Cannot draw {count} elements without replacement from a population of {source.Count}.");
		}

		T[] pool = new T[source.Count];
		for (int i = 0; i < pool.Length; i++)
		{
			pool[i] = source[i];
		}

		T[] result = new T[count];
		for (int i = 0; i < count; i++)
		{
			int j = i + NextInt32(pool.Length - i);
			(pool[i], pool[j]) = (pool[j], pool[i]);
			result[i] = pool[i];
		}

		return result;
	}
}
