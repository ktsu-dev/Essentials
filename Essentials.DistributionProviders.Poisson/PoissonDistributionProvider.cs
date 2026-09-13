// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Poisson;

using ktsu.Essentials;
using System;

/// <summary>
/// The Poisson distribution: the number of events in a fixed interval, when events arrive independently
/// at a constant average rate.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of the exponential distribution — that one gives the gap between arrivals, this one
/// gives how many arrive in a window. Requests per second, defects per unit, calls per hour. Its mean
/// and its variance are the same number, which is the usual way of noticing that real data is not
/// Poisson.
/// </para>
/// <para>
/// The CDF is the regularized upper incomplete gamma function, evaluated in one step whatever the count,
/// rather than a sum of masses whose length grows with it.
/// </para>
/// </remarks>
/// <param name="rate">The average number of events per interval. Must be greater than zero.</param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="rate"/> is not finite or is not greater than zero.</exception>
public sealed class PoissonDistributionProvider(double rate) : IDiscreteDistribution
{
	/// <summary>
	/// The rate up to which sampling multiplies uniforms rather than inverting the CDF.
	/// </summary>
	/// <remarks>
	/// Knuth's method takes about one draw per event, so its cost grows with the rate while inversion's
	/// does not. It also compares against exp(-rate), which underflows to zero above about 745 and would
	/// turn the loop into an infinite one; the limit here sits far below that.
	/// </remarks>
	private const double DirectSamplingLimit = 30.0;

	/// <summary>
	/// Gets the rate, the average number of events per interval.
	/// </summary>
	public double Rate { get; } = DistributionArguments.Positive(rate, nameof(rate));

	private readonly double logRate = Math.Log(rate);

	/// <summary>
	/// Initializes a Poisson distribution with unit rate.
	/// </summary>
	public PoissonDistributionProvider()
		: this(1.0)
	{
	}

	/// <inheritdoc />
	public int Minimum => 0;

	/// <inheritdoc />
	public int Maximum => int.MaxValue;

	/// <inheritdoc />
	public double Mean => Rate;

	/// <inheritdoc />
	public double Variance => Rate;

	/// <inheritdoc />
	public double Pmf(int value) => value < 0 ? 0.0 : Math.Exp(LogPmf(value));

	/// <inheritdoc />
	public double LogPmf(int value)
		=> value < 0
			? double.NegativeInfinity
			: (value * logRate) - Rate - SpecialFunctions.LogFactorial(value);

	/// <inheritdoc />
	public double Cdf(int value) => value < 0 ? 0.0 : SpecialFunctions.RegularizedGammaQ(value + 1.0, Rate);

	/// <summary>
	/// Evaluates the survival function, the probability of more than <paramref name="value"/> events.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one, because it is the other half of the same
	/// identity: the two incomplete gamma functions are complements, and taking the one that is small
	/// directly keeps every digit of a tail that subtraction would round away.
	/// </remarks>
	/// <param name="value">The event count to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(int value) => value < 0 ? 1.0 : SpecialFunctions.RegularizedGammaP(value + 1.0, Rate);

	/// <inheritdoc />
	public int Sample(IRandomProvider random)
	{
		Ensure.NotNull(random);

		if (Rate > DirectSamplingLimit)
		{
			return ((IDiscreteDistribution)this).Quantile(random.NextDoubleExclusive());
		}

		// Knuth's method: the gaps between Poisson events are exponential, so multiplying uniforms until
		// their product drops below exp(-rate) counts the events that fit inside one interval.
		double limit = Math.Exp(-Rate);
		double product = random.NextDoubleExclusive();
		int count = 0;
		while (product > limit)
		{
			count++;
			product *= random.NextDoubleExclusive();
		}

		return count;
	}
}
