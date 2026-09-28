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
	/// <remarks>
	/// Measured from the lower bound rather than as the average of the three raw parameters, so bounds
	/// near ±<see cref="double.MaxValue"/> cannot overflow the sum.
	/// </remarks>
	public double Mean => Minimum + ((lowerWidth + width) / 3.0);

	/// <inheritdoc />
	/// <remarks>
	/// The textbook form, <c>(a² + b² + c² − ab − ac − bc) / 18</c>, is algebraically identical but squares
	/// the raw bounds. For bounds that are large next to their spread — timestamps, prices in cents, file
	/// offsets — those squares cancel down to an O(1) result and every significant digit is lost, which can
	/// even leave the variance negative. Written in the two side widths the variance depends only on the
	/// shape of the triangle, not on where it sits.
	/// </remarks>
	public double Variance
		=> ((lowerWidth * lowerWidth) + (upperWidth * upperWidth) + (lowerWidth * upperWidth)) / 18.0;

	/// <inheritdoc />
	public double Pdf(double value)
	{
		if (value < Minimum || value > Maximum)
		{
			return 0.0;
		}

		// Each branch divides by the width of its own side, so the test has to keep a zero-width side out
		// of the denominator. Below the mode is safe unguarded: if the lower side is empty then the mode
		// is the lower bound and no value in range is below it. Above needs the guard, because the mode
		// itself reaches it, and where the mode is the upper bound that leaves 0/0 rather than the peak.
		double peak = 2.0 / width;
		if (value < Mode)
		{
			return peak * (value - Minimum) / lowerWidth;
		}

		return upperWidth > 0.0 ? peak * (Maximum - value) / upperWidth : peak;
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
