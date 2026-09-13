// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// The special functions the distribution providers evaluate their CDFs and quantiles with.
/// </summary>
/// <remarks>
/// <para>
/// Linked into each distribution project that needs it rather than placed in the interfaces package,
/// following <c>NonCryptoIncrementalHash</c> and <c>HmacKeyedHashCore</c>: ktsu.Essentials carries the
/// contracts, and a numerics library is not one. It is internal because every package compiles its own
/// copy, so a public type would collide for a consumer referencing more than one distribution package.
/// </para>
/// <para>
/// The incomplete gamma and incomplete beta functions are evaluated by the series and continued-fraction
/// pair given in Numerical Recipes, each used on the side of the crossover where it converges quickly.
/// The error function is derived from the incomplete gamma rather than approximated separately, because
/// <c>erf(x)</c> is exactly <c>P(1/2, x²)</c> — one implementation, one set of accuracy characteristics,
/// and no second approximation to keep honest.
/// </para>
/// </remarks>
internal static class SpecialFunctions
{
	/// <summary>The square root of 2π, the normalising constant of the standard normal density.</summary>
	internal const double SqrtTwoPi = 2.5066282746310005;

	/// <summary>The reciprocal of the square root of two, for converting a z-score into an erfc argument.</summary>
	internal const double InverseSqrtTwo = 0.70710678118654752440;

	/// <summary>The relative accuracy the iterative expansions are driven to.</summary>
	private const double Epsilon = 2.22e-16;

	/// <summary>A number near the smallest representable double, used to keep a continued fraction off zero.</summary>
	private const double Tiny = 1e-300;

	/// <summary>The iteration cap. Both expansions converge well inside it over the range they are used on.</summary>
	private const int MaxIterations = 300;

	/// <summary>The probability at which the quantile approximation switches from its central branch to a tail branch.</summary>
	private const double TailBoundary = 0.02425;

	/// <summary>Lanczos series coefficients for the log gamma function, with g = 671/128 and n = 14.</summary>
	private static readonly double[] LanczosCoefficients =
	[
		57.1562356658629235,
		-59.5979603554754912,
		14.1360979747417471,
		-0.491913816097620199,
		0.339946499848118887e-4,
		0.465236289270485756e-4,
		-0.983744753048795646e-4,
		0.158088703224912494e-3,
		-0.210264441724104883e-3,
		0.217439618115212643e-3,
		-0.164318106536763890e-3,
		0.844182239838527433e-4,
		-0.261908384015814087e-4,
		0.368991826595316234e-5,
	];

	/// <summary>Numerator coefficients of the central branch of the normal quantile approximation.</summary>
	private static readonly double[] CentralNumerator =
	[
		-3.969683028665376e+01,
		2.209460984245205e+02,
		-2.759285104469687e+02,
		1.383577518672690e+02,
		-3.066479806614716e+01,
		2.506628277459239e+00,
	];

	/// <summary>Denominator coefficients of the central branch of the normal quantile approximation.</summary>
	private static readonly double[] CentralDenominator =
	[
		-5.447609879822406e+01,
		1.615858368580409e+02,
		-1.556989798598866e+02,
		6.680131188771972e+01,
		-1.328068155288572e+01,
	];

	/// <summary>Numerator coefficients of the tail branch of the normal quantile approximation.</summary>
	private static readonly double[] TailNumerator =
	[
		-7.784894002430293e-03,
		-3.223964580411365e-01,
		-2.400758277161838e+00,
		-2.549732539343734e+00,
		4.374664141464968e+00,
		2.938163982698783e+00,
	];

	/// <summary>Denominator coefficients of the tail branch of the normal quantile approximation.</summary>
	private static readonly double[] TailDenominator =
	[
		7.784695709041462e-03,
		3.224671290700398e-01,
		2.445134137142996e+00,
		3.754408661907416e+00,
	];

	/// <summary>
	/// Evaluates the natural logarithm of the gamma function.
	/// </summary>
	/// <remarks>
	/// The Lanczos approximation, accurate to within a few parts in 10^15 for positive arguments.
	/// Taking the logarithm rather than the gamma function itself is what keeps a factorial usable:
	/// 171! already overflows a double, while its logarithm does not.
	/// </remarks>
	/// <param name="value">The argument. Must be greater than zero.</param>
	/// <returns>The natural logarithm of the gamma function at <paramref name="value"/>.</returns>
	internal static double LogGamma(double value)
	{
		double y = value;
		double tmp = value + 5.2421875;
		tmp = ((value + 0.5) * Math.Log(tmp)) - tmp;
		double series = 0.999999999999997092;
		for (int i = 0; i < LanczosCoefficients.Length; i++)
		{
			series += LanczosCoefficients[i] / ++y;
		}

		return tmp + Math.Log(SqrtTwoPi * series / value);
	}

	/// <summary>
	/// Evaluates the natural logarithm of a factorial.
	/// </summary>
	/// <param name="value">The argument. Must not be negative.</param>
	/// <returns>The natural logarithm of <paramref name="value"/> factorial.</returns>
	internal static double LogFactorial(int value) => LogGamma(value + 1.0);

	/// <summary>
	/// Evaluates the natural logarithm of a binomial coefficient.
	/// </summary>
	/// <remarks>
	/// Computed through <see cref="LogFactorial(int)"/> rather than by multiplying the coefficient out,
	/// so the intermediate values stay in range for counts where the coefficient itself would not.
	/// </remarks>
	/// <param name="total">The size of the set. Must not be negative.</param>
	/// <param name="chosen">The size of the subset. Must be between zero and <paramref name="total"/>.</param>
	/// <returns>The natural logarithm of <paramref name="total"/> choose <paramref name="chosen"/>.</returns>
	internal static double LogBinomialCoefficient(int total, int chosen)
		=> LogFactorial(total) - LogFactorial(chosen) - LogFactorial(total - chosen);

	/// <summary>
	/// Evaluates the regularized lower incomplete gamma function.
	/// </summary>
	/// <param name="shape">The shape parameter. Must be greater than zero.</param>
	/// <param name="value">The upper limit of integration. Must not be negative.</param>
	/// <returns>A value in the range [0, 1].</returns>
	internal static double RegularizedGammaP(double shape, double value)
	{
		if (double.IsPositiveInfinity(value))
		{
			return 1.0;
		}

		return value <= 0.0
			? 0.0
			: value < shape + 1.0 ? GammaSeries(shape, value) : 1.0 - GammaContinuedFraction(shape, value);
	}

	/// <summary>
	/// Evaluates the regularized upper incomplete gamma function, the complement of <see cref="RegularizedGammaP"/>.
	/// </summary>
	/// <remarks>
	/// Kept as its own entry point rather than computed as one minus the lower function, because the
	/// continued fraction gives the small upper tail directly and subtracting a near-one value from one
	/// would throw away every significant digit of it.
	/// </remarks>
	/// <param name="shape">The shape parameter. Must be greater than zero.</param>
	/// <param name="value">The lower limit of integration. Must not be negative.</param>
	/// <returns>A value in the range [0, 1].</returns>
	internal static double RegularizedGammaQ(double shape, double value)
	{
		if (double.IsPositiveInfinity(value))
		{
			return 0.0;
		}

		return value <= 0.0
			? 1.0
			: value < shape + 1.0 ? 1.0 - GammaSeries(shape, value) : GammaContinuedFraction(shape, value);
	}

	/// <summary>
	/// Evaluates the error function.
	/// </summary>
	/// <param name="value">The argument.</param>
	/// <returns>A value in the range (-1, 1).</returns>
	internal static double Erf(double value)
		=> value < 0.0 ? -RegularizedGammaP(0.5, value * value) : RegularizedGammaP(0.5, value * value);

	/// <summary>
	/// Evaluates the complementary error function.
	/// </summary>
	/// <param name="value">The argument.</param>
	/// <returns>A value in the range (0, 2).</returns>
	internal static double Erfc(double value)
		=> value < 0.0 ? 1.0 + RegularizedGammaP(0.5, value * value) : RegularizedGammaQ(0.5, value * value);

	/// <summary>
	/// Evaluates the cumulative distribution function of the standard normal distribution.
	/// </summary>
	/// <param name="zScore">The number of standard deviations from the mean.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	internal static double StandardNormalCdf(double zScore)
	{
		if (double.IsNegativeInfinity(zScore))
		{
			return 0.0;
		}

		return double.IsPositiveInfinity(zScore) ? 1.0 : 0.5 * Erfc(-zScore * InverseSqrtTwo);
	}

	/// <summary>
	/// Evaluates the quantile function of the standard normal distribution.
	/// </summary>
	/// <remarks>
	/// Acklam's rational approximation, which is good to about nine significant figures, followed by one
	/// step of Halley's method against <see cref="StandardNormalCdf(double)"/> that takes it to the
	/// accuracy of the CDF itself, a few parts in 10^13. The refinement is skipped where the density
	/// underflows, far out in either
	/// tail, because the correction term there is a zero multiplied by an infinity rather than a number.
	/// </remarks>
	/// <param name="probability">The probability to invert, in the range [0, 1].</param>
	/// <returns>The z-score whose cumulative probability is <paramref name="probability"/>.</returns>
	internal static double StandardNormalQuantile(double probability)
	{
		if (probability <= 0.0)
		{
			return double.NegativeInfinity;
		}

		if (probability >= 1.0)
		{
			return double.PositiveInfinity;
		}

		double estimate;
		if (probability < TailBoundary)
		{
			estimate = TailBranch(Math.Sqrt(-2.0 * Math.Log(probability)));
		}
		else if (probability > 1.0 - TailBoundary)
		{
			estimate = -TailBranch(Math.Sqrt(-2.0 * Math.Log(1.0 - probability)));
		}
		else
		{
			estimate = CentralBranch(probability);
		}

		// A residual of zero needs no special case. Where the density is representable the correction
		// works out to zero and the estimate is returned unchanged; where it has underflowed the product
		// is a zero times an infinity, and the guard below catches that along with every other way the
		// far tail can produce a correction that is not a number.
		double error = StandardNormalCdf(estimate) - probability;
		double scaled = error * SqrtTwoPi * Math.Exp(estimate * estimate / 2.0);
		double correction = scaled / (1.0 + (estimate * scaled / 2.0));
		return double.IsNaN(correction) || double.IsInfinity(correction) ? estimate : estimate - correction;
	}

	/// <summary>
	/// Evaluates the regularized incomplete beta function.
	/// </summary>
	/// <param name="a">The first shape parameter. Must be greater than zero.</param>
	/// <param name="b">The second shape parameter. Must be greater than zero.</param>
	/// <param name="value">The upper limit of integration, in the range [0, 1].</param>
	/// <returns>A value in the range [0, 1].</returns>
	internal static double RegularizedIncompleteBeta(double a, double b, double value)
	{
		if (value <= 0.0)
		{
			return 0.0;
		}

		if (value >= 1.0)
		{
			return 1.0;
		}

		double front = Math.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b) + (a * Math.Log(value)) + (b * Math.Log(1.0 - value)));

		// The continued fraction converges quickly only below its crossover point, so the larger side is
		// evaluated as the complement of the smaller one.
		return value < (a + 1.0) / (a + b + 2.0)
			? front * BetaContinuedFraction(a, b, value) / a
			: 1.0 - (front * BetaContinuedFraction(b, a, 1.0 - value) / b);
	}

	/// <summary>
	/// Evaluates the central branch of the normal quantile approximation.
	/// </summary>
	/// <param name="probability">The probability to invert.</param>
	/// <returns>The approximate z-score.</returns>
	private static double CentralBranch(double probability)
	{
		double offset = probability - 0.5;
		double squared = offset * offset;
		double numerator = 0.0;
		for (int i = 0; i < CentralNumerator.Length; i++)
		{
			numerator = (numerator * squared) + CentralNumerator[i];
		}

		double denominator = 0.0;
		for (int i = 0; i < CentralDenominator.Length; i++)
		{
			denominator = (denominator * squared) + CentralDenominator[i];
		}

		return numerator * offset / ((denominator * squared) + 1.0);
	}

	/// <summary>
	/// Evaluates the tail branch of the normal quantile approximation.
	/// </summary>
	/// <param name="distance">The square root of minus twice the log of the tail probability.</param>
	/// <returns>The approximate z-score for the lower tail.</returns>
	private static double TailBranch(double distance)
	{
		double numerator = 0.0;
		for (int i = 0; i < TailNumerator.Length; i++)
		{
			numerator = (numerator * distance) + TailNumerator[i];
		}

		double denominator = 0.0;
		for (int i = 0; i < TailDenominator.Length; i++)
		{
			denominator = (denominator * distance) + TailDenominator[i];
		}

		return numerator / ((denominator * distance) + 1.0);
	}

	/// <summary>
	/// Evaluates the regularized lower incomplete gamma function by its power series, which converges
	/// quickly below the crossover point.
	/// </summary>
	/// <param name="shape">The shape parameter.</param>
	/// <param name="value">The upper limit of integration.</param>
	/// <returns>A value in the range [0, 1].</returns>
	private static double GammaSeries(double shape, double value)
	{
		double term = 1.0 / shape;
		double sum = term;
		double denominator = shape;
		for (int i = 0; i < MaxIterations; i++)
		{
			denominator += 1.0;
			term *= value / denominator;
			sum += term;
			if (Math.Abs(term) < Math.Abs(sum) * Epsilon)
			{
				break;
			}
		}

		return sum * Math.Exp(-value + (shape * Math.Log(value)) - LogGamma(shape));
	}

	/// <summary>
	/// Evaluates the regularized upper incomplete gamma function by its continued fraction, which
	/// converges quickly above the crossover point.
	/// </summary>
	/// <remarks>Evaluated with the modified Lentz algorithm, working on the reciprocals to avoid division by a term that can vanish.</remarks>
	/// <param name="shape">The shape parameter.</param>
	/// <param name="value">The lower limit of integration.</param>
	/// <returns>A value in the range [0, 1].</returns>
	private static double GammaContinuedFraction(double shape, double value)
	{
		double b = value + 1.0 - shape;
		double c = 1.0 / Tiny;
		double d = 1.0 / b;
		double result = d;
		for (int i = 1; i <= MaxIterations; i++)
		{
			double a = -i * (i - shape);
			b += 2.0;
			d = (a * d) + b;
			if (Math.Abs(d) < Tiny)
			{
				d = Tiny;
			}

			c = b + (a / c);
			if (Math.Abs(c) < Tiny)
			{
				c = Tiny;
			}

			d = 1.0 / d;
			double delta = d * c;
			result *= delta;
			if (Math.Abs(delta - 1.0) <= Epsilon)
			{
				break;
			}
		}

		return Math.Exp(-value + (shape * Math.Log(value)) - LogGamma(shape)) * result;
	}

	/// <summary>
	/// Evaluates the continued fraction behind <see cref="RegularizedIncompleteBeta"/>.
	/// </summary>
	/// <remarks>Evaluated with the modified Lentz algorithm, taking the even and odd steps in one pass.</remarks>
	/// <param name="a">The first shape parameter.</param>
	/// <param name="b">The second shape parameter.</param>
	/// <param name="value">The upper limit of integration.</param>
	/// <returns>The value of the continued fraction.</returns>
	private static double BetaContinuedFraction(double a, double b, double value)
	{
		double sum = a + b;
		double next = a + 1.0;
		double previous = a - 1.0;
		double c = 1.0;
		double d = 1.0 - (sum * value / next);
		if (Math.Abs(d) < Tiny)
		{
			d = Tiny;
		}

		d = 1.0 / d;
		double result = d;
		for (int i = 1; i <= MaxIterations; i++)
		{
			int even = 2 * i;

			double term = i * (b - i) * value / ((previous + even) * (a + even));
			d = 1.0 + (term * d);
			if (Math.Abs(d) < Tiny)
			{
				d = Tiny;
			}

			c = 1.0 + (term / c);
			if (Math.Abs(c) < Tiny)
			{
				c = Tiny;
			}

			d = 1.0 / d;
			result *= d * c;

			term = -(a + i) * (sum + i) * value / ((a + even) * (next + even));
			d = 1.0 + (term * d);
			if (Math.Abs(d) < Tiny)
			{
				d = Tiny;
			}

			c = 1.0 + (term / c);
			if (Math.Abs(c) < Tiny)
			{
				c = Tiny;
			}

			d = 1.0 / d;
			double delta = d * c;
			result *= delta;
			if (Math.Abs(delta - 1.0) <= Epsilon)
			{
				break;
			}
		}

		return result;
	}
}
