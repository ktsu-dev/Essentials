// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Geometric;

using ktsu.Essentials;
using System;

/// <summary>
/// The geometric distribution: the number of failures before the first success in a sequence of
/// independent trials.
/// </summary>
/// <remarks>
/// <para>
/// The counting convention matters, because both are in circulation. This one counts <em>failures</em>,
/// so the support starts at zero and the mean is <c>(1 - p) / p</c>. The other convention counts trials
/// including the successful one, starting at one; add one to a draw from this to convert.
/// </para>
/// <para>
/// Like the exponential distribution it is memoryless: a run of failures says nothing about how many
/// more are coming. That makes it the model for retry counts, and for the number of attempts a
/// rejection sampler needs.
/// </para>
/// </remarks>
public sealed class GeometricDistributionProvider : IDiscreteDistribution
{
	private readonly double failureProbability;
	private readonly double logFailureProbability;

	/// <summary>
	/// Initializes a geometric distribution with an even chance of success on each trial.
	/// </summary>
	public GeometricDistributionProvider()
		: this(0.5)
	{
	}

	/// <summary>
	/// Initializes a geometric distribution with the given probability of success per trial.
	/// </summary>
	/// <param name="probability">The probability of success on each trial, in the range (0, 1].</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is outside (0, 1] or is not a number.</exception>
	public GeometricDistributionProvider(double probability)
	{
		DistributionArguments.Probability(probability, nameof(probability));
		if (probability <= 0.0)
		{
			// A success that never arrives has no waiting time to describe, finite or otherwise: every
			// moment of the distribution diverges, so the parameter is rejected rather than admitted and
			// left to produce infinities downstream.
			throw new ArgumentOutOfRangeException(nameof(probability), probability, "probability must be greater than zero.");
		}

		Probability = probability;
		failureProbability = 1.0 - probability;
		logFailureProbability = Math.Log(failureProbability);
	}

	/// <summary>
	/// Gets the probability of success on each trial.
	/// </summary>
	public double Probability { get; }

	/// <inheritdoc />
	public int Minimum => 0;

	/// <inheritdoc />
	public int Maximum => int.MaxValue;

	/// <inheritdoc />
	public double Mean => failureProbability / Probability;

	/// <inheritdoc />
	public double Variance => failureProbability / (Probability * Probability);

	/// <inheritdoc />
	public double Pmf(int value) => value < 0 ? 0.0 : Math.Pow(failureProbability, value) * Probability;

	/// <inheritdoc />
	public double Cdf(int value) => value < 0 ? 0.0 : 1.0 - Math.Pow(failureProbability, value + 1.0);

	/// <summary>
	/// Evaluates the survival function, the probability of more than <paramref name="value"/> failures.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one: the tail is exactly <c>(1 - p)</c> raised
	/// to the power of one more than the count, which is both simpler and accurate however small it gets.
	/// </remarks>
	/// <param name="value">The failure count to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(int value) => value < 0 ? 1.0 : Math.Pow(failureProbability, value + 1.0);

	/// <inheritdoc />
	public int Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		if (Probability >= 1.0)
		{
			return 0;
		}

		if (probability >= 1.0)
		{
			return int.MaxValue;
		}

		// The smallest count k whose CDF reaches the target satisfies (k + 1) >= log(1 - p) / log(1 - q),
		// with the inequality flipping because the logarithm of the failure probability is negative.
		double raw = Math.Ceiling(Math.Log(1.0 - probability) / logFailureProbability) - 1.0;
		int count = raw <= 0.0 ? 0 : raw >= int.MaxValue ? int.MaxValue : (int)raw;

		// That form is exact in real arithmetic, but where the target is itself a CDF value the ratio of
		// the two logarithms lands a rounding step either side of a whole number, and the ceiling turns
		// a step into a whole outcome. One comparison against the CDF in each direction puts it back, and
		// neither loop can run more than once because the estimate is never off by more than one.
		while (count > 0 && Cdf(count - 1) >= probability)
		{
			count--;
		}

		while (count < int.MaxValue && Cdf(count) < probability)
		{
			count++;
		}

		return count;
	}
}
