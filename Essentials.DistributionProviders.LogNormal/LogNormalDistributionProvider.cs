// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.LogNormal;

using ktsu.Essentials;
using System;

/// <summary>
/// The log-normal distribution: the distribution of a quantity whose logarithm is normally distributed.
/// </summary>
/// <remarks>
/// <para>
/// Where the normal distribution arises from influences that add, this one arises from influences that
/// multiply — each step scales the last by some factor rather than shifting it. That makes it the usual
/// model for quantities that cannot go negative and whose spread grows with their size: request
/// latencies, file sizes, incomes, particle sizes, compounding returns.
/// </para>
/// <para>
/// The parameters are the mean and standard deviation of the underlying normal, not of the distribution
/// itself. They are not the same numbers — <see cref="IDistribution{T}.Mean"/> reports the actual mean,
/// which is larger than <c>exp(logMean)</c> because the long right tail pulls it above the median.
/// </para>
/// </remarks>
/// <param name="logMean">The mean of the underlying normal, which is the logarithm of this distribution's median.</param>
/// <param name="logStandardDeviation">The standard deviation of the underlying normal. Must be greater than zero.</param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="logMean"/> is not finite, or <paramref name="logStandardDeviation"/> is not greater than zero.</exception>
public sealed class LogNormalDistributionProvider(double logMean, double logStandardDeviation) : IContinuousDistribution
{
	/// <summary>
	/// Gets the mean of the underlying normal distribution.
	/// </summary>
	public double LogMean { get; } = DistributionArguments.Finite(logMean, nameof(logMean));

	/// <summary>
	/// Gets the standard deviation of the underlying normal distribution.
	/// </summary>
	public double LogStandardDeviation { get; } = DistributionArguments.Positive(logStandardDeviation, nameof(logStandardDeviation));

	private readonly double logNormalisation = Math.Log(logStandardDeviation * SpecialFunctions.SqrtTwoPi);

	/// <summary>
	/// Initializes the standard log-normal distribution, the exponential of a standard normal.
	/// </summary>
	public LogNormalDistributionProvider()
		: this(0.0, 1.0)
	{
	}

	/// <inheritdoc />
	public double Minimum => 0.0;

	/// <inheritdoc />
	public double Maximum => double.PositiveInfinity;

	/// <inheritdoc />
	public double Mean => Math.Exp(LogMean + (LogStandardDeviation * LogStandardDeviation / 2.0));

	/// <inheritdoc />
	public double Variance
	{
		get
		{
			double logVariance = LogStandardDeviation * LogStandardDeviation;
			return (Math.Exp(logVariance) - 1.0) * Math.Exp((2.0 * LogMean) + logVariance);
		}
	}

	/// <inheritdoc />
	public double Median => Math.Exp(LogMean);

	/// <inheritdoc />
	public double Pdf(double value)
	{
		if (value <= 0.0)
		{
			return 0.0;
		}

		double z = ZScore(value);
		return Math.Exp(-0.5 * z * z) / (value * LogStandardDeviation * SpecialFunctions.SqrtTwoPi);
	}

	/// <inheritdoc />
	public double LogPdf(double value)
	{
		if (value <= 0.0)
		{
			return double.NegativeInfinity;
		}

		double z = ZScore(value);
		return (-0.5 * z * z) - logNormalisation - Math.Log(value);
	}

	/// <inheritdoc />
	public double Cdf(double value) => value <= 0.0 ? 0.0 : SpecialFunctions.StandardNormalCdf(ZScore(value));

	/// <summary>
	/// Evaluates the survival function, the probability that a draw exceeds <paramref name="value"/>.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one, so the upper tail — the part of a
	/// heavy-tailed distribution most often asked about, as a latency percentile or a risk of overrun —
	/// is computed directly instead of surviving a cancellation against one.
	/// </remarks>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(double value) => value <= 0.0 ? 1.0 : SpecialFunctions.StandardNormalCdf(-ZScore(value));

	/// <inheritdoc />
	public double Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		return probability <= 0.0
			? 0.0
			: Math.Exp(LogMean + (LogStandardDeviation * SpecialFunctions.StandardNormalQuantile(probability)));
	}

	/// <summary>
	/// Converts a value into the z-score of its logarithm under the underlying normal.
	/// </summary>
	/// <param name="value">The value to convert. Must be greater than zero.</param>
	/// <returns>The z-score of the logarithm of <paramref name="value"/>.</returns>
	private double ZScore(double value) => (Math.Log(value) - LogMean) / LogStandardDeviation;
}
