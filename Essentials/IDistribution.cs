// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// Interface for univariate probability distributions over a sample space of <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// <para>
/// An implementation must supply <see cref="Cdf(T)"/>, <see cref="Quantile(double)"/>, the support
/// bounds and the first two moments. Sampling, the median and the survival function come free from
/// those, and an implementation is free to declare any of them when it has a faster or more accurate
/// route.
/// </para>
/// <para>
/// The default sampler inverts the CDF, which consumes exactly one draw per sample and reproduces the
/// distribution exactly as accurately as <see cref="Quantile(double)"/> does. Distributions with a
/// cheaper closed-form sampler override it.
/// </para>
/// <para>
/// Instances are immutable: the parameters are fixed at construction and no state changes while
/// sampling, so one instance is safe to share across threads provided the
/// <see cref="IRandomProvider"/> passed to it is.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of a single outcome — <see cref="double"/> for a continuous distribution, <see cref="int"/> for a discrete one.</typeparam>
public interface IDistribution<T> where T : struct
{
	/// <summary>
	/// Gets the expected value of the distribution.
	/// </summary>
	public double Mean { get; }

	/// <summary>
	/// Gets the variance of the distribution.
	/// </summary>
	public double Variance { get; }

	/// <summary>
	/// Gets the standard deviation of the distribution.
	/// </summary>
	public double StandardDeviation => Math.Sqrt(Variance);

	/// <summary>
	/// Gets the lowest value the distribution can produce, or negative infinity when it is unbounded below.
	/// </summary>
	public T Minimum { get; }

	/// <summary>
	/// Gets the highest value the distribution can produce, or positive infinity when it is unbounded above.
	/// </summary>
	public T Maximum { get; }

	/// <summary>
	/// Evaluates the cumulative distribution function: the probability that a draw is at most <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double Cdf(T value);

	/// <summary>
	/// Evaluates the survival function: the probability that a draw exceeds <paramref name="value"/>.
	/// </summary>
	/// <remarks>
	/// The complement of <see cref="Cdf(T)"/>. The default subtracts from one, which loses precision far
	/// out in the upper tail; a distribution with a directly computable tail should declare this member.
	/// </remarks>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>A probability in the range [0, 1].</returns>
	public double SurvivalFunction(T value) => 1.0 - Cdf(value);

	/// <summary>
	/// Evaluates the quantile function, the inverse of <see cref="Cdf(T)"/>.
	/// </summary>
	/// <param name="probability">The probability to invert, in the range [0, 1].</param>
	/// <returns>The smallest value whose cumulative probability is at least <paramref name="probability"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is outside [0, 1] or is not a number.</exception>
	public T Quantile(double probability);

	/// <summary>
	/// Gets the median of the distribution.
	/// </summary>
	public T Median => Quantile(0.5);

	/// <summary>
	/// Draws one value from the distribution.
	/// </summary>
	/// <param name="random">The source of randomness to draw from.</param>
	/// <returns>The drawn value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="random"/> is null.</exception>
	public T Sample(IRandomProvider random)
	{
		Ensure.NotNull(random);

		return Quantile(random.NextDoubleExclusive());
	}

	/// <summary>
	/// Fills the destination buffer with independent draws from the distribution.
	/// </summary>
	/// <param name="random">The source of randomness to draw from.</param>
	/// <param name="destination">The buffer to fill. Every element in it is overwritten.</param>
	/// <exception cref="ArgumentNullException"><paramref name="random"/> is null.</exception>
	public void Sample(IRandomProvider random, Span<T> destination)
	{
		Ensure.NotNull(random);

		for (int i = 0; i < destination.Length; i++)
		{
			destination[i] = Sample(random);
		}
	}

	/// <summary>
	/// Returns a new array of independent draws from the distribution.
	/// </summary>
	/// <param name="random">The source of randomness to draw from.</param>
	/// <param name="count">The number of values to draw.</param>
	/// <returns>An array of <paramref name="count"/> draws.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="random"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
	public T[] Sample(IRandomProvider random, int count)
	{
		Ensure.NotNegative(count);

		T[] result = new T[count];
		Sample(random, result.AsSpan());
		return result;
	}
}
