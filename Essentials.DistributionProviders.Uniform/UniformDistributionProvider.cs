// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Uniform;

using ktsu.Essentials;
using System;

/// <summary>
/// The continuous uniform distribution on an interval, where every value in the interval is equally likely.
/// </summary>
/// <remarks>
/// The distribution every other one here is built from: an <see cref="IRandomProvider"/> draw on the
/// unit interval is a sample from the standard uniform, and inverting a quantile function turns it into
/// a sample from anything else.
/// </remarks>
public sealed class UniformDistributionProvider : IContinuousDistribution
{
	private readonly double width;

	/// <summary>
	/// Initializes the standard uniform distribution on the unit interval.
	/// </summary>
	public UniformDistributionProvider()
		: this(0.0, 1.0)
	{
	}

	/// <summary>
	/// Initializes a uniform distribution on the given interval.
	/// </summary>
	/// <param name="minimum">The lower bound of the interval.</param>
	/// <param name="maximum">The upper bound of the interval. Must be greater than <paramref name="minimum"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException">A bound is not finite, or the interval is empty.</exception>
	public UniformDistributionProvider(double minimum, double maximum)
	{
		DistributionArguments.Finite(minimum, nameof(minimum));
		DistributionArguments.Finite(maximum, nameof(maximum));
		DistributionArguments.Below(minimum, maximum, nameof(minimum), nameof(maximum));

		Minimum = minimum;
		Maximum = maximum;
		width = maximum - minimum;
	}

	/// <inheritdoc />
	public double Minimum { get; }

	/// <inheritdoc />
	public double Maximum { get; }

	/// <inheritdoc />
	public double Mean => Minimum + (width / 2.0);

	/// <inheritdoc />
	public double Variance => width * width / 12.0;

	/// <inheritdoc />
	public double Pdf(double value) => value >= Minimum && value <= Maximum ? 1.0 / width : 0.0;

	/// <inheritdoc />
	public double Cdf(double value)
	{
		if (value <= Minimum)
		{
			return 0.0;
		}

		return value >= Maximum ? 1.0 : (value - Minimum) / width;
	}

	/// <inheritdoc />
	public double Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		return Minimum + (probability * width);
	}

	/// <inheritdoc />
	public double Sample(IRandomProvider random)
	{
		Ensure.NotNull(random);

		return random.NextDouble(Minimum, Maximum);
	}
}
