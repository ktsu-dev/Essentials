// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Normal;

using ktsu.Essentials;
using System;

/// <summary>
/// The normal (Gaussian) distribution.
/// </summary>
/// <remarks>
/// <para>
/// The default model for a quantity pushed around by many small independent influences, because the
/// central limit theorem says a sum of those tends to this shape whatever the influences themselves
/// look like: measurement error, aggregated noise, the sample mean of almost anything.
/// </para>
/// <para>
/// Neither the CDF nor its inverse has a closed form. The CDF is evaluated through the complementary
/// error function and the quantile through a rational approximation refined against it, both to within
/// a few parts in 10^13 — see <c>SpecialFunctions</c> for how.
/// </para>
/// </remarks>
/// <param name="mean">The centre of the distribution.</param>
/// <param name="standardDeviation">The spread of the distribution. Must be greater than zero.</param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="mean"/> is not finite, or <paramref name="standardDeviation"/> is not greater than zero.</exception>
public sealed class NormalDistributionProvider(double mean, double standardDeviation) : IContinuousDistribution
{
	/// <inheritdoc />
	public double Mean { get; } = DistributionArguments.Finite(mean, nameof(mean));

	/// <inheritdoc />
	public double StandardDeviation { get; } = DistributionArguments.Positive(standardDeviation, nameof(standardDeviation));

	private readonly double logNormalisation = Math.Log(standardDeviation * SpecialFunctions.SqrtTwoPi);

	/// <summary>
	/// Initializes the standard normal distribution, with mean zero and unit standard deviation.
	/// </summary>
	public NormalDistributionProvider()
		: this(0.0, 1.0)
	{
	}

	/// <inheritdoc />
	public double Variance => StandardDeviation * StandardDeviation;

	/// <inheritdoc />
	public double Minimum => double.NegativeInfinity;

	/// <inheritdoc />
	public double Maximum => double.PositiveInfinity;

	/// <inheritdoc />
	public double Median => Mean;

	/// <inheritdoc />
	public double Pdf(double value)
	{
		double z = ZScore(value);
		return Math.Exp(-0.5 * z * z) / (StandardDeviation * SpecialFunctions.SqrtTwoPi);
	}

	/// <inheritdoc />
	public double LogPdf(double value)
	{
		double z = ZScore(value);
		return (-0.5 * z * z) - logNormalisation;
	}

	/// <inheritdoc />
	public double Cdf(double value) => SpecialFunctions.StandardNormalCdf(ZScore(value));

	/// <summary>
	/// Evaluates the survival function, the probability that a draw exceeds <paramref name="value"/>.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one. The normal distribution is symmetric, so
	/// the upper tail at <c>z</c> equals the lower tail at <c>-z</c>, which the CDF computes directly and
	/// accurately; the subtraction would round a small tail down to whatever survives cancellation
	/// against one.
	/// </remarks>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(double value) => SpecialFunctions.StandardNormalCdf(-ZScore(value));

	/// <inheritdoc />
	public double Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		return Mean + (StandardDeviation * SpecialFunctions.StandardNormalQuantile(probability));
	}

	/// <summary>
	/// Converts a value into the number of standard deviations it sits from the mean.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <returns>The z-score of <paramref name="value"/>.</returns>
	private double ZScore(double value) => (value - Mean) / StandardDeviation;
}
