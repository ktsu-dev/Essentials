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

	/// <summary>Half the width, which stays finite when the full width overflows.</summary>
	private readonly double halfWidth;

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
		halfWidth = (maximum / 2.0) - (minimum / 2.0);
	}

	/// <inheritdoc />
	public double Minimum { get; }

	/// <inheritdoc />
	public double Maximum { get; }

	/// <inheritdoc />
	public double Mean => (Minimum / 2.0) + (Maximum / 2.0);

	/// <inheritdoc />
	public double Variance => width * width / 12.0;

	/// <inheritdoc />
	public double Pdf(double value) => value >= Minimum && value <= Maximum ? 0.5 / halfWidth : 0.0;

	/// <inheritdoc />
	public double Cdf(double value)
	{
		if (value <= Minimum)
		{
			return 0.0;
		}

		if (value >= Maximum)
		{
			return 1.0;
		}

		// Bounds of opposite sign can be far enough apart that the width overflows; halving both
		// sides of the ratio keeps every term finite.
		return double.IsInfinity(width) ? ((value / 2.0) - (Minimum / 2.0)) / halfWidth : (value - Minimum) / width;
	}

	/// <inheritdoc />
	public double Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		return double.IsInfinity(width)
			? (Minimum * (1.0 - probability)) + (Maximum * probability)
			: Minimum + (probability * width);
	}

	/// <inheritdoc />
	public double Sample(IRandomProvider random)
	{
		Ensure.NotNull(random);

		return random.NextDouble(Minimum, Maximum);
	}
}
