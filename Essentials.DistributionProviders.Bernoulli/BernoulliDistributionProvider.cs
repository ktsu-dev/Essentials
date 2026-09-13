// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Bernoulli;

using ktsu.Essentials;
using System;

/// <summary>
/// The Bernoulli distribution: a single trial with two outcomes, reported as 1 for success and 0 for failure.
/// </summary>
/// <remarks>
/// The simplest distribution there is, and the building block of two others here — a binomial is a fixed
/// number of these added up, and a geometric counts how many it takes to get a success.
/// </remarks>
/// <param name="probability">The probability of success, in the range [0, 1].</param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is outside [0, 1] or is not a number.</exception>
public sealed class BernoulliDistributionProvider(double probability) : IDiscreteDistribution
{
	/// <summary>
	/// Initializes a fair Bernoulli distribution, with equal probability of success and failure.
	/// </summary>
	public BernoulliDistributionProvider()
		: this(0.5)
	{
	}

	/// <summary>
	/// Gets the probability of success.
	/// </summary>
	public double Probability { get; } = DistributionArguments.Probability(probability, nameof(probability));

	/// <inheritdoc />
	public int Minimum => 0;

	/// <inheritdoc />
	public int Maximum => 1;

	/// <inheritdoc />
	public double Mean => Probability;

	/// <inheritdoc />
	public double Variance => Probability * (1.0 - Probability);

	/// <inheritdoc />
	public double Pmf(int value)
	{
		if (value == 0)
		{
			return 1.0 - Probability;
		}

		return value == 1 ? Probability : 0.0;
	}

	/// <inheritdoc />
	public double Cdf(int value)
	{
		if (value < 0)
		{
			return 0.0;
		}

		return value == 0 ? 1.0 - Probability : 1.0;
	}

	/// <inheritdoc />
	public int Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		return probability <= 1.0 - Probability ? 0 : 1;
	}

	/// <inheritdoc />
	public int Sample(IRandomProvider random)
	{
		Ensure.NotNull(random);

		return random.NextBoolean(Probability) ? 1 : 0;
	}
}
