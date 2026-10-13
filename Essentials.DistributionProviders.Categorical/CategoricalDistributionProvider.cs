// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Categorical;

using ktsu.Essentials;
using System;
using System.Collections.Generic;

/// <summary>
/// The categorical distribution: a weighted choice among a fixed set of outcomes, numbered from zero.
/// </summary>
/// <remarks>
/// <para>
/// The general-purpose discrete distribution — a loot table, a weighted A/B split, a language model's
/// next token, a loaded die. Every other discrete distribution here has a formula behind it; this one
/// is given its masses outright.
/// </para>
/// <para>
/// The weights need not sum to one and are normalised on construction, so relative sizes are all that
/// matter. The cumulative sums are computed once, which makes both the CDF and a draw a lookup rather
/// than a walk over the outcomes.
/// </para>
/// </remarks>
public sealed class CategoricalDistributionProvider : IDiscreteDistribution
{
	private readonly double[] probabilities;
	private readonly double[] cumulative;

	/// <summary>The mass above each outcome, summed from the top down so a small tail keeps its digits.</summary>
	private readonly double[] upper;

	/// <summary>
	/// Initializes a categorical distribution from a set of relative weights.
	/// </summary>
	/// <param name="weights">
	/// The weight of each outcome, where the outcome is its index. Must not be empty, must be finite and
	/// non-negative, and must not all be zero. They are normalised, so they need not sum to one.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="weights"/> is null.</exception>
	/// <exception cref="ArgumentException"><paramref name="weights"/> is empty, holds a negative or non-finite weight, or sums to zero.</exception>
	public CategoricalDistributionProvider(IReadOnlyList<double> weights)
	{
		Ensure.NotNull(weights);
		if (weights.Count == 0)
		{
			throw new ArgumentException("A categorical distribution needs at least one outcome.", nameof(weights));
		}

		double total = 0.0;
		for (int i = 0; i < weights.Count; i++)
		{
			double weight = weights[i];
			if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0.0)
			{
				throw new ArgumentException($"Weight at index {i} must be a finite, non-negative number, but was {weight}.", nameof(weights));
			}

			total += weight;
		}

		if (total <= 0.0)
		{
			throw new ArgumentException("At least one weight must be greater than zero.", nameof(weights));
		}

		probabilities = new double[weights.Count];
		cumulative = new double[weights.Count];
		double running = 0.0;
		double mean = 0.0;
		double secondMoment = 0.0;
		for (int i = 0; i < weights.Count; i++)
		{
			double probability = weights[i] / total;
			probabilities[i] = probability;
			running += probability;
			cumulative[i] = running;
			mean += i * probability;
			secondMoment += (double)i * i * probability;
		}

		// The running total lands a rounding step either side of one; pinning the last entry keeps the
		// CDF from ever reporting a value below one at the top of the support.
		cumulative[^1] = 1.0;

		upper = new double[weights.Count];
		double above = 0.0;
		for (int i = probabilities.Length - 1; i >= 0; i--)
		{
			upper[i] = above;
			above += probabilities[i];
		}

		Mean = mean;
		Variance = Math.Max(0.0, secondMoment - (mean * mean));
	}

	/// <summary>
	/// Gets the normalised probability of each outcome, indexed by outcome.
	/// </summary>
	public IReadOnlyList<double> Probabilities => probabilities;

	/// <inheritdoc />
	public int Minimum => 0;

	/// <inheritdoc />
	public int Maximum => probabilities.Length - 1;

	/// <inheritdoc />
	public double Mean { get; }

	/// <inheritdoc />
	public double Variance { get; }

	/// <inheritdoc />
	public double Pmf(int value) => value < 0 || value > Maximum ? 0.0 : probabilities[value];

	/// <inheritdoc />
	public double Cdf(int value)
	{
		if (value < 0)
		{
			return 0.0;
		}

		return value >= Maximum ? 1.0 : cumulative[value];
	}

	/// <summary>
	/// Evaluates the survival function, the probability of an outcome greater than <paramref name="value"/>.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one: the tail is looked up in sums taken from the
	/// last outcome down, so rare outcomes at the top keep their digits instead of vanishing next to one.
	/// </remarks>
	/// <param name="value">The outcome to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(int value)
	{
		if (value < 0)
		{
			return 1.0;
		}

		return value >= Maximum ? 0.0 : upper[value];
	}

	/// <inheritdoc />
	public int Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		// The cumulative sums are non-decreasing, so the first entry to reach the target is the outcome
		// the quantile names, and bisection finds it without walking the outcomes.
		int low = 0;
		int high = Maximum;
		while (low < high)
		{
			int mid = low + ((high - low) / 2);
			if (cumulative[mid] >= probability)
			{
				high = mid;
			}
			else
			{
				low = mid + 1;
			}
		}

		return low;
	}
}
