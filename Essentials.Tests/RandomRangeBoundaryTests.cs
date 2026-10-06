// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using ktsu.Essentials;
using ktsu.Essentials.DistributionProviders.Uniform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

/// <summary>
/// Pins <see cref="IRandomProvider.NextDouble(double, double)"/> and the uniform distribution at the edges
/// a random sample is unlikely to reach: the top unit draw, and finite bounds whose width overflows.
/// </summary>
[TestClass]
public class RandomRangeBoundaryTests
{
	[TestMethod]
	[DataRow(1.0, 1.0000000000000002)]
	[DataRow(1.0, 2.0)]
	[DataRow(0.3, 0.7)]
	[DataRow(5.0, 5.5)]
	[DataRow(0.9, 1.0)]
	public void NextDouble_Never_Returns_The_Exclusive_Bound_At_The_Top_Draw(double minInclusive, double maxExclusive)
	{
		IRandomProvider random = new ConstantRandomProvider(ulong.MaxValue);

		double value = random.NextDouble(minInclusive, maxExclusive);

		Assert.IsTrue(value >= minInclusive && value < maxExclusive, $"NextDouble({minInclusive:R}, {maxExclusive:R}) returned {value:R}");
	}

	[TestMethod]
	[DataRow(ulong.MaxValue)]
	[DataRow(0UL)]
	[DataRow(0x8000000000000000UL)]
	public void NextDouble_Stays_Finite_When_The_Width_Overflows(ulong word)
	{
		IRandomProvider random = new ConstantRandomProvider(word);

		double value = random.NextDouble(double.MinValue, double.MaxValue);

		Assert.IsTrue(double.IsFinite(value), $"NextDouble(MinValue, MaxValue) returned {value} for 0x{word:X16}");
		Assert.IsTrue(value < double.MaxValue, $"NextDouble(MinValue, MaxValue) returned the exclusive bound for 0x{word:X16}");
	}

	[TestMethod]
	public void Uniform_Handles_Bounds_Whose_Width_Overflows()
	{
		IContinuousDistribution uniform = new UniformDistributionProvider(-1e308, 1e308);

		Assert.AreEqual(0.0, uniform.Mean);
		Assert.AreEqual(0.0, uniform.Median);
		Assert.AreEqual(0.5, uniform.Cdf(0.0));
		Assert.AreEqual(0.75, uniform.Cdf(1e308 / 2.0), 1e-12);
		Assert.AreEqual(0.0, uniform.Quantile(0.5));
		Assert.AreEqual(-1e308, uniform.Quantile(0.0));
		Assert.AreEqual(1e308, uniform.Quantile(1.0));
		Assert.IsTrue(uniform.Pdf(0.0) > 0.0, "Pdf underflowed to 0 inside the support");
		Assert.IsTrue(double.IsFinite(uniform.Sample(new ConstantRandomProvider(ulong.MaxValue))));
		Assert.IsTrue(double.IsFinite(uniform.Sample(new ConstantRandomProvider(0UL))));
	}

	[TestMethod]
	[DataRow(1.0)]
	[DataRow(-1.0)]
	[DataRow(0.0)]
	[DataRow(-0.0)]
	[DataRow(double.Epsilon)]
	[DataRow(-double.Epsilon)]
	[DataRow(double.MaxValue)]
	[DataRow(double.MinValue + 1e292)]
	public void NextDown_Matches_Math_BitDecrement(double value)
	{
		Assert.AreEqual(Math.BitDecrement(value), RandomHelpers.NextDown(value), $"NextDown({value:R})");
	}

	/// <summary>A provider that fills every buffer with the same 64-bit word, for pinning boundary draws.</summary>
	private sealed class ConstantRandomProvider(ulong word) : IRandomProvider
	{
		public void NextBytes(Span<byte> destination)
		{
			byte[] bytes = BitConverter.GetBytes(word);
			for (int i = 0; i < destination.Length; i++)
			{
				destination[i] = bytes[i % bytes.Length];
			}
		}
	}
}
