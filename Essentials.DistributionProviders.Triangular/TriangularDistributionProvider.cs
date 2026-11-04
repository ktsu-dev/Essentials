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
	// The widths are held at half size when the full range overflows a double, as the uniform distribution's
	// are (issue #142): halving is exact, and every ratio below is the same at either size.
	private readonly bool halved;
	private readonly double scale;
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
		halved = double.IsInfinity(maximum - minimum);
		scale = halved ? 0.5 : 1.0;
		width = Scaled(maximum) - Scaled(minimum);
		lowerWidth = Scaled(mode) - Scaled(minimum);
		upperWidth = Scaled(maximum) - Scaled(mode);
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
	public double Mean => halved
		? (Minimum / 3.0) + (Mode / 3.0) + (Maximum / 3.0)
		: Minimum + ((lowerWidth + width) / 3.0);

	/// <inheritdoc />
	/// <remarks>
	/// The textbook form, <c>(a² + b² + c² − ab − ac − bc) / 18</c>, is algebraically identical but squares
	/// the raw bounds. For bounds that are large next to their spread — timestamps, prices in cents, file
	/// offsets — those squares cancel down to an O(1) result and every significant digit is lost, which can
	/// even leave the variance negative. Written in the two side widths the variance depends only on the
	/// shape of the triangle, not on where it sits.
	/// </remarks>
	public double Variance
		=> ((lowerWidth * lowerWidth) + (upperWidth * upperWidth) + (lowerWidth * upperWidth)) / 18.0 / (scale * scale);

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
		double peak = 2.0 * scale / width;
		if (value < Mode)
		{
			return peak * (Scaled(value) - Scaled(Minimum)) / lowerWidth;
		}

		return upperWidth > 0.0 ? peak * (Scaled(Maximum) - Scaled(value)) / upperWidth : peak;
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
			? LowerTail(value)
			: 1.0 - UpperTail(value);
	}

	/// <summary>
	/// Evaluates the survival function, the probability of a value greater than <paramref name="value"/>.
	/// </summary>
	/// <remarks>
	/// Declared rather than left to subtract the CDF from one: above the mode the tail is the closed form
	/// <c>(max - v)² / (width × upper width)</c>, which subtraction would round to zero near the upper
	/// bound. Below the mode the tail is at least the upper side's share of the mass, so one minus the CDF
	/// is well conditioned there.
	/// </remarks>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(double value)
	{
		if (value <= Minimum)
		{
			return 1.0;
		}

		if (value >= Maximum)
		{
			return 0.0;
		}

		// The same branch test as Cdf, which keeps a zero-width side out of the denominator.
		return value <= Mode
			? 1.0 - LowerTail(value)
			: UpperTail(value);
	}

	/// <inheritdoc />
	public double Quantile(double probability)
	{
		DistributionArguments.QuantileProbability(probability, nameof(probability));

		// Each root is taken of its two factors separately, since their product overflows for wide bounds, and
		// the offset from the bound is added once per halving rather than doubled, which could overflow too.
		if (probability < modeProbability)
		{
			double offset = Math.Sqrt(probability * width) * Math.Sqrt(lowerWidth);
			return halved ? Minimum + offset + offset : Minimum + offset;
		}

		double fromTop = Math.Sqrt((1.0 - probability) * width) * Math.Sqrt(upperWidth);
		return halved ? Maximum - fromTop - fromTop : Maximum - fromTop;
	}

	// The CDF below the mode, (v - min)² / (width × lower width), as a product of two ratios no larger than one,
	// so neither the square nor the product of widths can overflow.
	private double LowerTail(double value)
	{
		double fromBottom = Scaled(value) - Scaled(Minimum);
		return fromBottom / width * (fromBottom / lowerWidth);
	}

	// The survival function above the mode, (max - v)² / (width × upper width), in the same form.
	private double UpperTail(double value)
	{
		double fromTop = Scaled(Maximum) - Scaled(value);
		return fromTop / width * (fromTop / upperWidth);
	}

	private double Scaled(double value) => value * scale;
}
