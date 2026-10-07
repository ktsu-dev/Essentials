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
	/// <summary>
	/// How far either way the closed-form quantile may be walked before giving up on it and bisecting.
	/// </summary>
	private const int MaxQuantileCorrection = 2;

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

		// Taken as log1p(-p) rather than the log of 1 - p, which rounds to exactly one below p of about
		// 1e-16 and leaves a logarithm of zero: every CDF value would then be zero and every quantile a
		// walk to int.MaxValue. Above that it still costs digits in proportion to how small p is.
		logFailureProbability = SpecialFunctions.Log1P(-probability);
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
	public double Pmf(int value)
	{
		if (value < 0)
		{
			return 0.0;
		}

		// Zero failures is the success probability itself. Taking it through the logarithm would multiply a
		// zero count by the infinite logarithm of a certain success, which is a NaN rather than one.
		if (value == 0)
		{
			return Probability;
		}

		return Math.Exp(value * logFailureProbability) * Probability;
	}

	/// <inheritdoc />
	/// <remarks>
	/// One minus <c>(1 - p)^(k + 1)</c>, written as <c>-expm1((k + 1) log1p(-p))</c> so that neither the
	/// failure probability nor the subtraction from one throws away the digits a small CDF lives in.
	/// </remarks>
	public double Cdf(int value) => value < 0 ? 0.0 : -SpecialFunctions.ExpM1((value + 1.0) * logFailureProbability);

	/// <summary>
	/// Evaluates the survival function, the probability of more than <paramref name="value"/> failures.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one: the tail is exactly <c>(1 - p)</c> raised
	/// to the power of one more than the count, which is both simpler and accurate however small it gets.
	/// </remarks>
	/// <param name="value">The failure count to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(int value) => value < 0 ? 1.0 : Math.Exp((value + 1.0) * logFailureProbability);

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
		double raw = Math.Ceiling(SpecialFunctions.Log1P(-probability) / logFailureProbability) - 1.0;
		int count = raw <= 0.0 ? 0 : raw >= int.MaxValue ? int.MaxValue : (int)raw;

		// That form is exact in real arithmetic, but where the target is itself a CDF value the ratio of
		// the two logarithms lands a rounding step either side of a whole number, and the ceiling turns
		// a step into a whole outcome. A comparison against the CDF in each direction puts it back. The
		// estimate should never be off by more than one, but the walks are bounded all the same: an
		// unbounded one is what turned a bad estimate into two billion steps, and bisecting the whole
		// support costs only 31 CDF evaluations.
		for (int step = 0; step < MaxQuantileCorrection && count > 0 && Cdf(count - 1) >= probability; step++)
		{
			count--;
		}

		for (int step = 0; step < MaxQuantileCorrection && count < int.MaxValue && Cdf(count) < probability; step++)
		{
			count++;
		}

		bool settled = (count == int.MaxValue || Cdf(count) >= probability) && (count == 0 || Cdf(count - 1) < probability);
		return settled ? count : Bisect(probability);
	}

	/// <summary>
	/// Finds the smallest count whose CDF reaches <paramref name="probability"/> by bisecting the support.
	/// </summary>
	/// <param name="probability">The target probability.</param>
	/// <returns>The quantile.</returns>
	internal int Bisect(double probability)
	{
		int low = 0;
		int high = int.MaxValue;
		while (low < high)
		{
			int mid = low + ((high - low) / 2);
			if (Cdf(mid) >= probability)
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
