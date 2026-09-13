// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Binomial;

using ktsu.Essentials;
using System;

/// <summary>
/// The binomial distribution: the number of successes in a fixed number of independent trials that each
/// succeed with the same probability.
/// </summary>
/// <remarks>
/// <para>
/// The model for a count out of a known total — conversions out of visits, defects out of a batch,
/// heads out of a hundred flips. With one trial it is the Bernoulli distribution; with many it
/// approaches the normal, which is where the rule about polling margins of error comes from.
/// </para>
/// <para>
/// The mass function is evaluated through logarithms rather than by multiplying the binomial coefficient
/// out, because the coefficient overflows a double at around 1030 trials while the mass it contributes
/// to stays perfectly ordinary. The CDF is the regularized incomplete beta function, which costs the
/// same whether the count is 3 or 3 million; adding the masses up would not.
/// </para>
/// </remarks>
public sealed class BinomialDistributionProvider : IDiscreteDistribution
{
	/// <summary>
	/// The trial count up to which sampling adds up individual trials rather than inverting the CDF.
	/// </summary>
	/// <remarks>
	/// Counting trials costs one draw each and inverting costs about 31 incomplete beta evaluations, so
	/// the crossover sits where a trial is cheaper than a thirtieth of that. Anywhere near the boundary
	/// the two are within a hair of each other, which is why the exact value does not matter much.
	/// </remarks>
	private const int DirectSamplingLimit = 64;

	private readonly double failureProbability;

	/// <summary>
	/// Initializes a binomial distribution.
	/// </summary>
	/// <param name="trials">The number of trials. Must not be negative.</param>
	/// <param name="probability">The probability that each trial succeeds, in the range [0, 1].</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="trials"/> is negative, or <paramref name="probability"/> is outside [0, 1].</exception>
	public BinomialDistributionProvider(int trials, double probability)
	{
		Ensure.NotNegative(trials);
		DistributionArguments.Probability(probability, nameof(probability));

		Trials = trials;
		Probability = probability;
		failureProbability = 1.0 - probability;
	}

	/// <summary>
	/// Gets the number of trials.
	/// </summary>
	public int Trials { get; }

	/// <summary>
	/// Gets the probability that each individual trial succeeds.
	/// </summary>
	public double Probability { get; }

	/// <inheritdoc />
	public int Minimum => 0;

	/// <inheritdoc />
	public int Maximum => Trials;

	/// <inheritdoc />
	public double Mean => Trials * Probability;

	/// <inheritdoc />
	public double Variance => Trials * Probability * failureProbability;

	/// <inheritdoc />
	public double Pmf(int value) => value < 0 || value > Trials ? 0.0 : Math.Exp(LogPmf(value));

	/// <inheritdoc />
	public double LogPmf(int value)
	{
		if (value < 0 || value > Trials)
		{
			return double.NegativeInfinity;
		}

		// A degenerate probability puts all the mass on one outcome. Taking the general route would ask
		// for the logarithm of zero and multiply it by a zero count, which is a NaN rather than the
		// certainty it should be.
		if (Probability <= 0.0)
		{
			return value == 0 ? 0.0 : double.NegativeInfinity;
		}

		if (Probability >= 1.0)
		{
			return value == Trials ? 0.0 : double.NegativeInfinity;
		}

		return SpecialFunctions.LogBinomialCoefficient(Trials, value)
			+ (value * Math.Log(Probability))
			+ ((Trials - value) * Math.Log(failureProbability));
	}

	/// <inheritdoc />
	public double Cdf(int value)
	{
		if (value < 0)
		{
			return 0.0;
		}

		// The identity is P(X <= k) = I(1 - p; n - k, k + 1), which needs both shape parameters positive
		// and so does not cover the top of the support; there the answer is one by definition anyway.
		return value >= Trials
			? 1.0
			: SpecialFunctions.RegularizedIncompleteBeta(Trials - value, value + 1.0, failureProbability);
	}

	/// <inheritdoc />
	public int Sample(IRandomProvider random)
	{
		Ensure.NotNull(random);

		if (Trials > DirectSamplingLimit)
		{
			return ((IDiscreteDistribution)this).Quantile(random.NextDoubleExclusive());
		}

		int successes = 0;
		for (int i = 0; i < Trials; i++)
		{
			if (random.NextBoolean(Probability))
			{
				successes++;
			}
		}

		return successes;
	}
}
