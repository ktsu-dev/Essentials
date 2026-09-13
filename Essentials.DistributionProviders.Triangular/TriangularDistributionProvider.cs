// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Triangular;

using ktsu.Essentials;
using System;

/// <summary>
/// The triangular distribution, defined by a lowest value, a highest value and a most likely value.
/// </summary>
/// <remarks>
/// The distribution to reach for when all that is known about a quantity is a plausible range and a best
/// guess inside it — three-point estimation of task durations and costs is the standard use. It says
/// strictly more than a uniform distribution over the same range, without pretending to the knowledge a
/// fitted distribution would require, and everything about it has a closed form.
/// </remarks>
public sealed class TriangularDistributionProvider : IContinuousDistribution
{
	private readonly double width;
	private readonly double lowerWidth;
	private readonly double upperWidth;
	private readonly double modeProbability;

	/// <summary>
	/// Initializes a triangular distribution.
	/// </summary>
	/// <param name="minimum">The lowest value the quantity can take.</param>
	/// <param name="mode">The most likely value. Must lie between the two bounds, and may equal either.</param>
	/// <param name="maximum">The highest value the quantity can take. Must be greater than <paramref name="minimum"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException">A parameter is not finite, the range is empty, or the mode falls outside it.</exception>
	public TriangularDistributionProvider(double minimum, double mode, double maximum)
	{
		DistributionArguments.Finite(minimum, nameof(minimum));
		DistributionArguments.Finite(mode, nameof(mode));
		DistributionArguments.Finite(maximum, nameof(maximum));
		DistributionArguments.Below(minimum, maximum, nameof(minimum), nameof(maximum));
		DistributionArguments.AtMost(minimum, mode, nameof(minimum), nameof(mode));
		DistributionArguments.AtMost(mode, maximum, nameof(mode), nameof(maximum));

		Minimum = minimum;
		Mode = mode;
		Maximum = maximum;
		width = maximum - minimum;
		lowerWidth = mode - minimum;
		upperWidth = maximum - mode;
		modeProbability = lowerWidth / width;
	}

	/// <inheritdoc />
	public double Minimum { get; }

	/// <inheritdoc />
	public double Maximum { get; }

	/// <summary>
	/// Gets the most likely value, where the density peaks.
	/// </summary>
	public double Mode { get; }

	/// <inheritdoc />
	public double Mean => (Minimum + Mode + Maximum) / 3.0;

	/// <inheritdoc />
	public double Variance
		=> ((Minimum * Minimum) + (Mode * Mode) + (Maximum * Maximum)
			- (Minimum * Mode) - (Minimum * Maximum) - (Mode * Maximum)) / 18.0;

	/// <inheritdoc />
	public double Pdf(double value)
	{
		if (value < Minimum || value > Maximum)
		{
			return 0.0;
		}

		double peak = 2.0 / width;
		if (value == Mode)
		{
			return peak;
		}

		return value < Mode
			? peak * (value - Minimum) / lowerWidth
			: peak * (Maximum - value) / upperWidth;
	}

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

		// Each branch divides by the width of its own side, so the test has to be the one that keeps a
		// zero-width side out of the denominator: a mode sitting on a bound leaves that side empty, and
		// no value can fall strictly inside it.
		return value <= Mode
			? (value - Minimum) * (value - Minimum) / (width * lowerWidth)
			: 1.0 - ((Maximum - value) * (Maximum - value) / (width * upperWidth));
	}

	/// <inheritdoc />
	public double Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		return probability < modeProbability
			? Minimum + Math.Sqrt(probability * width * lowerWidth)
			: Maximum - Math.Sqrt((1.0 - probability) * width * upperWidth);
	}
}
