// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Collections.Generic;

/// <summary>
/// Internal utilities behind the default implementations of <see cref="IRandomProvider"/> and
/// <see cref="IDistribution{T}"/>, kept out of the interfaces themselves so the contracts stay readable.
/// </summary>
internal static class RandomHelpers
{
	/// <summary>The step between adjacent 53-bit fixed-point values on the unit interval.</summary>
	internal const double UnitScale = 1.0 / 9007199254740992.0;

	/// <summary>The step between adjacent 24-bit fixed-point values on the unit interval.</summary>
	internal const float UnitScaleSingle = 1.0f / 16777216.0f;

	/// <summary>
	/// Draws a 32-bit value uniformly from [0, <paramref name="range"/>) without modulo bias.
	/// </summary>
	/// <remarks>
	/// Lemire's multiply-and-shift method. Multiplying a full-width draw by the range and taking the
	/// high word divides the 2^32 draws into <paramref name="range"/> buckets whose sizes differ by at
	/// most one. The correction discards the first 2^32 mod <paramref name="range"/> draws, which are
	/// exactly the ones that make the buckets uneven, so the result is exactly uniform. The rejection
	/// test is skipped entirely unless the low word falls below the range, so the common case costs
	/// one multiply.
	/// </remarks>
	/// <param name="random">The source of randomness.</param>
	/// <param name="range">The size of the range. Must be greater than zero.</param>
	/// <returns>A value in [0, <paramref name="range"/>).</returns>
	internal static uint NextBoundedUInt32(IRandomProvider random, uint range)
	{
		ulong product = (ulong)random.NextUInt32() * range;
		uint low = unchecked((uint)product);
		if (low < range)
		{
			uint threshold = unchecked(0u - range) % range;
			while (low < threshold)
			{
				product = (ulong)random.NextUInt32() * range;
				low = unchecked((uint)product);
			}
		}

		return (uint)(product >> 32);
	}

	/// <summary>
	/// Draws a 64-bit value uniformly from [0, <paramref name="range"/>) without modulo bias.
	/// </summary>
	/// <remarks>
	/// Lemire's method needs a 128-bit product, which is not available on every target framework here,
	/// so this uses rejection instead: draws at or above the largest multiple of the range that fits in
	/// 64 bits are discarded, leaving a count divisible by the range for the modulo to split evenly.
	/// The expected number of extra draws is below one for any range.
	/// </remarks>
	/// <param name="random">The source of randomness.</param>
	/// <param name="range">The size of the range. Must be greater than zero.</param>
	/// <returns>A value in [0, <paramref name="range"/>).</returns>
	internal static ulong NextBoundedUInt64(IRandomProvider random, ulong range)
	{
		ulong threshold = unchecked(0UL - range) % range;
		ulong value;
		do
		{
			value = random.NextUInt64();
		}
		while (value < threshold);

		return value % range;
	}

	/// <summary>
	/// Throws when a value is infinite or not a number.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="name">The parameter name to report.</param>
	/// <exception cref="ArgumentOutOfRangeException">The value is not finite.</exception>
	internal static void EnsureFinite(double value, string name)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			throw new ArgumentOutOfRangeException(name, value, $"{name} must be a finite number.");
		}
	}

	/// <summary>
	/// Throws when a value is not a probability in [0, 1].
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="name">The parameter name to report.</param>
	/// <exception cref="ArgumentOutOfRangeException">The value is outside [0, 1] or is not a number.</exception>
	internal static void EnsureProbability(double value, string name)
	{
		if (double.IsNaN(value) || value < 0.0 || value > 1.0)
		{
			throw new ArgumentOutOfRangeException(name, value, $"{name} must be a probability in the range [0, 1].");
		}
	}

	/// <summary>
	/// Adds up a weight list, rejecting negative, non-finite, and all-zero weights.
	/// </summary>
	/// <param name="weights">The weights to total.</param>
	/// <param name="name">The parameter name to report.</param>
	/// <returns>The sum of the weights.</returns>
	/// <exception cref="ArgumentException">A weight is negative or not finite, or they sum to zero.</exception>
	internal static double SumWeights(IReadOnlyList<double> weights, string name)
	{
		double total = 0.0;
		for (int i = 0; i < weights.Count; i++)
		{
			double weight = weights[i];
			if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0.0)
			{
				throw new ArgumentException($"Weight at index {i} must be a finite, non-negative number, but was {weight}.", name);
			}

			total += weight;
		}

		return total > 0.0
			? total
			: throw new ArgumentException("At least one weight must be greater than zero.", name);
	}

	/// <summary>
	/// Finds the last index carrying a non-zero weight, used as the fallback when rounding pushes a
	/// weighted draw past the accumulated total.
	/// </summary>
	/// <param name="weights">The weights to scan. At least one must be greater than zero.</param>
	/// <returns>The index of the last non-zero weight.</returns>
	internal static int LastNonZeroWeightIndex(IReadOnlyList<double> weights)
	{
		for (int i = weights.Count - 1; i >= 0; i--)
		{
			if (weights[i] > 0.0)
			{
				return i;
			}
		}

		// SumWeights has already rejected an all-zero list, so this is unreachable for validated input.
		return weights.Count - 1;
	}
}
