// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// Parameter validation shared by the distribution providers.
/// </summary>
/// <remarks>
/// Linked into each distribution project rather than placed in the interfaces package, following
/// <c>SpecialFunctions</c>. Every distribution rejects the same handful of malformed parameters — a
/// non-finite bound, a scale at or below zero, a probability outside the unit interval — and rejects
/// them in the constructor, so an instance that exists is one whose CDF and quantile are meaningful.
/// </remarks>
internal static class DistributionArguments
{
	/// <summary>
	/// Validates that a parameter is a finite number.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="name">The parameter name to report.</param>
	/// <returns>The validated value, so the check can wrap an assignment.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The value is infinite or not a number.</exception>
	internal static double Finite(double value, string name)
		=> double.IsNaN(value) || double.IsInfinity(value)
			? throw new ArgumentOutOfRangeException(name, value, $"{name} must be a finite number.")
			: value;

	/// <summary>
	/// Validates that a parameter is a finite number greater than zero.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="name">The parameter name to report.</param>
	/// <returns>The validated value, so the check can wrap an assignment.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The value is not finite, or is not greater than zero.</exception>
	internal static double Positive(double value, string name)
		=> Finite(value, name) <= 0.0
			? throw new ArgumentOutOfRangeException(name, value, $"{name} must be greater than zero.")
			: value;

	/// <summary>
	/// Validates that a parameter is a probability in the unit interval.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="name">The parameter name to report.</param>
	/// <returns>The validated value, so the check can wrap an assignment.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The value is outside [0, 1] or is not a number.</exception>
	internal static double Probability(double value, string name)
		=> double.IsNaN(value) || value < 0.0 || value > 1.0
			? throw new ArgumentOutOfRangeException(name, value, $"{name} must be a probability in the range [0, 1].")
			: value;

	/// <summary>
	/// Validates that one parameter is strictly below another.
	/// </summary>
	/// <param name="lower">The value that must be smaller.</param>
	/// <param name="upper">The value that must be larger.</param>
	/// <param name="lowerName">The parameter name of <paramref name="lower"/>.</param>
	/// <param name="upperName">The parameter name of <paramref name="upper"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="upper"/> is not greater than <paramref name="lower"/>.</exception>
	internal static void Below(double lower, double upper, string lowerName, string upperName)
	{
		if (upper <= lower)
		{
			throw new ArgumentOutOfRangeException(upperName, upper, $"{upperName} must be greater than {lowerName} ({lower}).");
		}
	}

	/// <summary>
	/// Validates that one parameter does not exceed another.
	/// </summary>
	/// <param name="lower">The value that must not be larger.</param>
	/// <param name="upper">The value that must not be smaller.</param>
	/// <param name="lowerName">The parameter name of <paramref name="lower"/>.</param>
	/// <param name="upperName">The parameter name of <paramref name="upper"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="upper"/> is below <paramref name="lower"/>.</exception>
	internal static void AtMost(double lower, double upper, string lowerName, string upperName)
	{
		if (upper < lower)
		{
			throw new ArgumentOutOfRangeException(upperName, upper, $"{upperName} must not be below {lowerName} ({lower}).");
		}
	}

	/// <summary>
	/// Validates the probability handed to a quantile function.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="name">The parameter name to report.</param>
	/// <exception cref="ArgumentOutOfRangeException">The value is outside [0, 1] or is not a number.</exception>
	internal static void QuantileProbability(double value, string name) => Probability(value, name);
}
