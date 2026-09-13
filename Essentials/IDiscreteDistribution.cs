// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// Interface for discrete probability distributions over the integers.
/// </summary>
/// <remarks>
/// Unlike a density, the mass at a point is a probability in its own right: the masses over the
/// support sum to one, and each is the probability of that exact outcome.
/// </remarks>
public interface IDiscreteDistribution : IDistribution<int>
{
	/// <summary>
	/// Evaluates the probability mass function: the probability of drawing exactly <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The outcome to evaluate at.</param>
	/// <returns>A probability in the range [0, 1], or zero outside the support.</returns>
	public double Pmf(int value);

	/// <summary>
	/// Evaluates the natural logarithm of the probability mass function.
	/// </summary>
	/// <remarks>
	/// The default takes the logarithm of <see cref="Pmf(int)"/>, which underflows to negative infinity
	/// once the mass falls below the smallest representable double. A distribution whose log-mass has a
	/// closed form should declare this member so far-tail work stays accurate.
	/// </remarks>
	/// <param name="value">The outcome to evaluate at.</param>
	/// <returns>The log mass at <paramref name="value"/>, or negative infinity outside the support.</returns>
	public double LogPmf(int value) => Math.Log(Pmf(value));

	/// <summary>
	/// Evaluates the quantile function by bisecting the support.
	/// </summary>
	/// <remarks>
	/// A discrete quantile is the smallest outcome whose cumulative probability reaches the target, and
	/// because <see cref="IDistribution{T}.Cdf(T)"/> is non-decreasing that outcome can be found by
	/// bisection — at most 31 evaluations even over an unbounded support, and no reliance on the tail
	/// masses staying above the underflow threshold. A distribution with a closed-form inverse should
	/// declare this member instead.
	/// </remarks>
	/// <param name="probability">The probability to invert, in the range [0, 1].</param>
	/// <returns>The smallest outcome whose cumulative probability is at least <paramref name="probability"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is outside [0, 1] or is not a number.</exception>
	[SuppressMessage("Design", "CA1033:Interface methods should be callable by child types", Justification = "This is a default interface implementation supplying a body for an inherited member, not an explicit implementation on a class hiding one. An implementer that wants its own inverse declares Quantile publicly, which takes precedence.")]
	int IDistribution<int>.Quantile(double probability)
	{
		RandomHelpers.EnsureProbability(probability, nameof(probability));

		int low = Minimum;
		int high = Maximum;
		if (probability >= 1.0)
		{
			return high;
		}

		while (low < high)
		{
			int mid = low + ((high - low) / 2);
			if (Cdf(mid) >= probability)
			{
				high = mid;
			}
			else
			{
				low = mid + 1;
			}
		}

		return low;
	}
}
