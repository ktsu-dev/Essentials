// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Exponential;

using ktsu.Essentials;
using System;

/// <summary>
/// The exponential distribution, the waiting time between events that arrive at a constant average rate.
/// </summary>
/// <remarks>
/// Parameterised by rate, not by mean: a rate of 4 means four events per unit of time and a mean wait of
/// a quarter. It is the only continuous distribution without memory — having waited an hour already tells
/// you nothing about how much longer the wait will be — which is what makes it the arrival-time model in
/// queueing and reliability work, and the continuous counterpart of the geometric distribution.
/// </remarks>
/// <param name="rate">The average number of events per unit of time. Must be greater than zero.</param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="rate"/> is not finite or is not greater than zero.</exception>
public sealed class ExponentialDistributionProvider(double rate) : IContinuousDistribution
{
	/// <summary>
	/// Initializes an exponential distribution with unit rate.
	/// </summary>
	public ExponentialDistributionProvider()
		: this(1.0)
	{
	}

	/// <summary>
	/// Gets the rate, the average number of events per unit of time.
	/// </summary>
	public double Rate { get; } = DistributionArguments.Positive(rate, nameof(rate));

	/// <inheritdoc />
	public double Minimum => 0.0;

	/// <inheritdoc />
	public double Maximum => double.PositiveInfinity;

	/// <inheritdoc />
	public double Mean => 1.0 / Rate;

	/// <inheritdoc />
	public double Variance => 1.0 / (Rate * Rate);

	/// <inheritdoc />
	public double Pdf(double value) => value < 0.0 ? 0.0 : Rate * Math.Exp(-Rate * value);

	/// <inheritdoc />
	public double LogPdf(double value) => value < 0.0 ? double.NegativeInfinity : Math.Log(Rate) - (Rate * value);

	/// <inheritdoc />
	public double Cdf(double value) => value <= 0.0 ? 0.0 : 1.0 - Math.Exp(-Rate * value);

	/// <summary>
	/// Evaluates the survival function, the probability that the wait exceeds <paramref name="value"/>.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one, because the tail is exactly what this
	/// distribution is usually asked about and the subtraction would round it away: a one-in-a-billion
	/// tail computed as <c>1 - 0.999999999</c> keeps only a few significant digits of its answer.
	/// </remarks>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(double value) => value <= 0.0 ? 1.0 : Math.Exp(-Rate * value);

	/// <inheritdoc />
	public double Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		return probability >= 1.0 ? double.PositiveInfinity : -Math.Log(1.0 - probability) / Rate;
	}

	/// <inheritdoc />
	public double Sample(IRandomProvider random)
	{
		Ensure.NotNull(random);

		// Inverting the CDF at u would be -log(1 - u) / rate; a uniform draw on the open unit interval
		// is distributed identically to its own complement, so the subtraction is simply dropped.
		return -Math.Log(random.NextDoubleExclusive()) / Rate;
	}
}
