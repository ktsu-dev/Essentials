// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;

/// <summary>
/// Interface for continuous probability distributions over the real line.
/// </summary>
/// <remarks>
/// The density is not a probability: it is a probability per unit of <c>x</c>, so it integrates to one
/// rather than summing to one and may exceed one where the distribution is narrow. The probability of
/// any single exact value is zero; use <see cref="IDistribution{T}.Cdf(T)"/> for the probability of an
/// interval.
/// </remarks>
public interface IContinuousDistribution : IDistribution<double>
{
	/// <summary>
	/// Evaluates the probability density function.
	/// </summary>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>The density at <paramref name="value"/>, or zero outside the support.</returns>
	public double Pdf(double value);

	/// <summary>
	/// Evaluates the natural logarithm of the probability density function.
	/// </summary>
	/// <remarks>
	/// The default takes the logarithm of <see cref="Pdf(double)"/>, which underflows to negative
	/// infinity once the density falls below the smallest representable double. A distribution whose
	/// log-density has a closed form should declare this member so far-tail work stays accurate.
	/// </remarks>
	/// <param name="value">The value to evaluate at.</param>
	/// <returns>The log density at <paramref name="value"/>, or negative infinity outside the support.</returns>
	public double LogPdf(double value) => Math.Log(Pdf(value));
}
