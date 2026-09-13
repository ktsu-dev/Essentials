// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using ktsu.Essentials;
using ktsu.Essentials.DistributionProviders.Bernoulli;
using ktsu.Essentials.DistributionProviders.Binomial;
using ktsu.Essentials.DistributionProviders.Categorical;
using ktsu.Essentials.DistributionProviders.Exponential;
using ktsu.Essentials.DistributionProviders.Geometric;
using ktsu.Essentials.DistributionProviders.LogNormal;
using ktsu.Essentials.DistributionProviders.Normal;
using ktsu.Essentials.DistributionProviders.Poisson;
using ktsu.Essentials.DistributionProviders.Triangular;
using ktsu.Essentials.DistributionProviders.Uniform;
using ktsu.Essentials.RandomProviders.Xoshiro;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tests every distribution against the contract its interface states, and against values computed
/// independently.
/// </summary>
/// <remarks>
/// Three kinds of check run here. The contract tests apply to every distribution and assert the
/// properties that make a CDF a CDF: bounded, non-decreasing, agreeing with its own inverse, and — for
/// a discrete distribution — agreeing with the sum of its masses. The reference tests compare against
/// values produced outside this codebase, which is the only way to catch a formula that is
/// self-consistently wrong. The sampling tests draw from a seeded generator and check that the empirical
/// moments land where the analytic ones say they should.
/// </remarks>
[TestClass]
public class DistributionProviderTests
{
	/// <summary>Draws per empirical check.</summary>
	private const int SampleCount = 40000;

	/// <summary>Tolerance for a value that should agree to close to full double precision.</summary>
	private const double Tight = 1e-12;

	public static IEnumerable<object[]> ContinuousDistributions =>
	[
		[new UniformDistributionProvider(-2.0, 5.0), "Uniform(-2, 5)"],
		[new NormalDistributionProvider(), "Normal(0, 1)"],
		[new NormalDistributionProvider(3.5, 2.0), "Normal(3.5, 2)"],
		[new ExponentialDistributionProvider(2.0), "Exponential(2)"],
		[new LogNormalDistributionProvider(), "LogNormal(0, 1)"],
		[new TriangularDistributionProvider(1.0, 2.0, 6.0), "Triangular(1, 2, 6)"],
		[new TriangularDistributionProvider(0.0, 0.0, 4.0), "Triangular(0, 0, 4)"],
		[new TriangularDistributionProvider(0.0, 4.0, 4.0), "Triangular(0, 4, 4)"],
	];

	public static IEnumerable<object[]> DiscreteDistributions =>
	[
		[new BernoulliDistributionProvider(0.3), "Bernoulli(0.3)"],
		[new BinomialDistributionProvider(20, 0.3), "Binomial(20, 0.3)"],
		[new BinomialDistributionProvider(5, 0.0), "Binomial(5, 0)"],
		[new BinomialDistributionProvider(5, 1.0), "Binomial(5, 1)"],
		[new PoissonDistributionProvider(4.5), "Poisson(4.5)"],
		[new GeometricDistributionProvider(0.25), "Geometric(0.25)"],
		[new GeometricDistributionProvider(1.0), "Geometric(1)"],
		[new CategoricalDistributionProvider([1.0, 3.0, 0.0, 6.0]), "Categorical(1, 3, 0, 6)"],
	];

	public TestContext TestContext { get; set; } = null!;

	private static IRandomProvider SeededRandom() => new XoshiroRandomProvider(20260913UL);

	// ---- registration ---------------------------------------------------------------------------

	[TestMethod]
	public void DI_Registers_Every_Distribution_That_Has_A_Standard_Form()
	{
		ServiceCollection services = new();
		services.AddCommon();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();

		string[] expectedContinuous = ["ExponentialDistributionProvider", "LogNormalDistributionProvider", "NormalDistributionProvider", "UniformDistributionProvider"];
		string[] continuous = [.. serviceProvider.GetServices<IContinuousDistribution>().Select(d => d.GetType().Name).OrderBy(n => n, StringComparer.Ordinal)];
		CollectionAssert.AreEqual(expectedContinuous, continuous, "The triangular distribution has no standard form and is deliberately left out of the aggregate registration.");

		string[] expectedDiscrete = ["BernoulliDistributionProvider", "GeometricDistributionProvider", "PoissonDistributionProvider"];
		string[] discrete = [.. serviceProvider.GetServices<IDiscreteDistribution>().Select(d => d.GetType().Name).OrderBy(n => n, StringComparer.Ordinal)];
		CollectionAssert.AreEqual(expectedDiscrete, discrete, "The binomial and categorical distributions have no standard form and are deliberately left out of the aggregate registration.");

		// A distribution is immutable, so sharing one instance is safe and the registration says so.
		Assert.AreSame(serviceProvider.GetRequiredService<NormalDistributionProvider>(), serviceProvider.GetRequiredService<NormalDistributionProvider>());

		// The registered instances are the standard forms of each family.
		Assert.AreEqual(0.0, serviceProvider.GetRequiredService<NormalDistributionProvider>().Mean, Tight);
		Assert.AreEqual(1.0, serviceProvider.GetRequiredService<NormalDistributionProvider>().Variance, Tight);
		Assert.AreEqual(1.0, serviceProvider.GetRequiredService<ExponentialDistributionProvider>().Rate, Tight);
		Assert.AreEqual(0.5, serviceProvider.GetRequiredService<BernoulliDistributionProvider>().Probability, Tight);
	}

	[TestMethod]
	public void DI_Registers_A_Parameterised_Distribution_From_Its_Own_Package()
	{
		ServiceCollection services = new();
		services.AddBinomialDistributionProvider(12, 0.4);
		services.AddTriangularDistributionProvider(1.0, 2.0, 5.0);
		services.AddCategoricalDistributionProvider([2.0, 1.0]);
		using ServiceProvider serviceProvider = services.BuildServiceProvider();

		Assert.AreEqual(12, serviceProvider.GetRequiredService<BinomialDistributionProvider>().Trials);
		Assert.AreEqual(2.0, serviceProvider.GetRequiredService<TriangularDistributionProvider>().Mode, Tight);
		Assert.AreEqual(2.0 / 3.0, serviceProvider.GetRequiredService<CategoricalDistributionProvider>().Probabilities[0], Tight);
		Assert.HasCount(1, serviceProvider.GetServices<IContinuousDistribution>().ToArray());
		Assert.HasCount(2, serviceProvider.GetServices<IDiscreteDistribution>().ToArray());
	}

	// ---- contract: continuous ------------------------------------------------------------------

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Cdf_Runs_From_Zero_To_One_Without_Going_Backwards(IContinuousDistribution distribution, string name)
	{
		double low = Finite(distribution.Minimum, distribution.Quantile(1e-9));
		double high = Finite(distribution.Maximum, distribution.Quantile(1.0 - 1e-9));
		double previous = 0.0;

		for (int i = 0; i <= 200; i++)
		{
			double x = low + ((high - low) * i / 200.0);
			double cdf = distribution.Cdf(x);
			Assert.IsTrue(cdf is >= 0.0 and <= 1.0, $"{name}: Cdf({x}) = {cdf}");
			Assert.IsTrue(cdf >= previous - Tight, $"{name}: Cdf decreased at {x}");
			previous = cdf;
		}

		Assert.AreEqual(0.0, distribution.Cdf(Finite(distribution.Minimum, -1e12) - 1.0), Tight, name);
		Assert.AreEqual(1.0, distribution.Cdf(Finite(distribution.Maximum, 1e12) + 1.0), Tight, name);
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Quantile_Inverts_The_Cdf(IContinuousDistribution distribution, string name)
	{
		foreach (double probability in new[] { 0.001, 0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99, 0.999 })
		{
			double x = distribution.Quantile(probability);
			Assert.AreEqual(probability, distribution.Cdf(x), 1e-9, $"{name}: round trip at {probability} landed on {x}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Density_Is_Non_Negative_And_Integrates_To_One(IContinuousDistribution distribution, string name)
	{
		// The trapezoid rule over the central 99.98% of the mass; the tolerance covers both the missing
		// tails and the error of the rule itself.
		const int Steps = 20000;
		double low = distribution.Quantile(0.0001);
		double high = distribution.Quantile(0.9999);
		double step = (high - low) / Steps;
		double total = 0.0;

		for (int i = 0; i <= Steps; i++)
		{
			double x = low + (i * step);
			double density = distribution.Pdf(x);
			Assert.IsTrue(density >= 0.0, $"{name}: Pdf({x}) = {density}");
			total += (i is 0 or Steps ? 0.5 : 1.0) * density * step;
		}

		Assert.AreEqual(1.0, total, 1e-3, $"{name}: density integrated to {total}");
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_LogDensity_Agrees_With_The_Density(IContinuousDistribution distribution, string name)
	{
		foreach (double probability in new[] { 0.05, 0.3, 0.5, 0.8, 0.95 })
		{
			double x = distribution.Quantile(probability);
			Assert.AreEqual(Math.Log(distribution.Pdf(x)), distribution.LogPdf(x), 1e-9, $"{name} at {x}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Survival_Function_Complements_The_Cdf(IContinuousDistribution distribution, string name)
	{
		foreach (double probability in new[] { 0.01, 0.25, 0.5, 0.75, 0.99 })
		{
			double x = distribution.Quantile(probability);
			Assert.AreEqual(1.0, distribution.Cdf(x) + distribution.SurvivalFunction(x), 1e-12, $"{name} at {x}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Samples_Match_The_Analytic_Moments(IContinuousDistribution distribution, string name)
	{
		IRandomProvider random = SeededRandom();
		double[] draws = distribution.Sample(random, SampleCount);

		Assert.HasCount(SampleCount, draws, name);

		double mean = draws.Average();
		double variance = draws.Sum(d => (d - mean) * (d - mean)) / (SampleCount - 1);

		// The sample mean has a standard error of sigma / sqrt(n); five of those is a bound that a
		// correct sampler clears essentially always and a wrong one does not.
		double standardError = distribution.StandardDeviation / Math.Sqrt(SampleCount);
		Assert.AreEqual(distribution.Mean, mean, 5.0 * standardError, $"{name}: sample mean {mean}");
		Assert.AreEqual(distribution.Variance, variance, distribution.Variance * 0.15, $"{name}: sample variance {variance}");
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Samples_Spread_Across_The_Quantiles(IContinuousDistribution distribution, string name)
	{
		// A sampler that is right on average can still be wrong in shape. Counting how many draws fall
		// into each decile of the distribution checks the shape rather than the first two moments.
		IRandomProvider random = SeededRandom();
		double[] bounds = [.. Enumerable.Range(1, 9).Select(i => distribution.Quantile(i / 10.0))];
		int[] counts = new int[10];

		for (int i = 0; i < SampleCount; i++)
		{
			double draw = distribution.Sample(random);
			int bucket = 0;
			while (bucket < bounds.Length && draw > bounds[bucket])
			{
				bucket++;
			}

			counts[bucket]++;
		}

		double expected = SampleCount / 10.0;
		foreach (int count in counts)
		{
			Assert.IsTrue(Math.Abs(count - expected) < expected * 0.12, $"{name}: deciles came out as {string.Join(", ", counts)}");
		}
	}

	// ---- contract: discrete --------------------------------------------------------------------

	[TestMethod]
	[DynamicData(nameof(DiscreteDistributions))]
	public void Discrete_Cdf_Accumulates_The_Masses(IDiscreteDistribution distribution, string name)
	{
		int top = Math.Min(distribution.Maximum, 200);
		double running = 0.0;

		for (int k = distribution.Minimum; k <= top; k++)
		{
			double mass = distribution.Pmf(k);
			Assert.IsTrue(mass is >= 0.0 and <= 1.0, $"{name}: Pmf({k}) = {mass}");
			running += mass;
			Assert.AreEqual(running, distribution.Cdf(k), 1e-10, $"{name}: Cdf({k}) disagreed with the accumulated mass");
		}

		Assert.AreEqual(1.0, running, 1e-9, $"{name}: masses summed to {running}");
		Assert.AreEqual(0.0, distribution.Cdf(distribution.Minimum - 1), Tight, name);
		Assert.AreEqual(0.0, distribution.Pmf(distribution.Minimum - 1), Tight, name);
	}

	[TestMethod]
	[DynamicData(nameof(DiscreteDistributions))]
	public void Discrete_Quantile_Inverts_The_Cdf(IDiscreteDistribution distribution, string name)
	{
		int top = Math.Min(distribution.Maximum, 40);
		for (int k = distribution.Minimum; k <= top; k++)
		{
			double cdf = distribution.Cdf(k);

			// An outcome with no mass shares its CDF value with the one before it, and the quantile of a
			// shared value is by definition the first outcome to reach it, so it is not this one.
			if (cdf is <= 0.0 or >= 1.0 || distribution.Pmf(k) <= 0.0)
			{
				continue;
			}

			// The quantile of a CDF value is the outcome that produced it, and nudging the target below
			// that value has to land at or below it.
			Assert.AreEqual(k, distribution.Quantile(cdf), $"{name}: Quantile(Cdf({k})) missed");
			Assert.IsTrue(distribution.Quantile(cdf * (1.0 - 1e-12)) <= k, $"{name}: Quantile overshot just below Cdf({k})");
		}

		Assert.AreEqual(distribution.Minimum, distribution.Quantile(0.0), name);

		// Not necessarily the declared maximum: a family's support bound is not always reachable — a
		// geometric distribution with certain success puts all of its mass on zero — so what a quantile
		// of one has to satisfy is that the whole distribution sits at or below it.
		Assert.AreEqual(1.0, distribution.Cdf(distribution.Quantile(1.0)), Tight, name);
	}

	[TestMethod]
	[DynamicData(nameof(DiscreteDistributions))]
	public void Discrete_LogMass_Agrees_With_The_Mass(IDiscreteDistribution distribution, string name)
	{
		int top = Math.Min(distribution.Maximum, 30);
		for (int k = distribution.Minimum; k <= top; k++)
		{
			double mass = distribution.Pmf(k);
			if (mass <= 0.0)
			{
				continue;
			}

			Assert.AreEqual(Math.Log(mass), distribution.LogPmf(k), 1e-9, $"{name} at {k}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(DiscreteDistributions))]
	public void Discrete_Survival_Function_Complements_The_Cdf(IDiscreteDistribution distribution, string name)
	{
		int top = Math.Min(distribution.Maximum, 40);
		for (int k = distribution.Minimum; k <= top; k++)
		{
			Assert.AreEqual(1.0, distribution.Cdf(k) + distribution.SurvivalFunction(k), 1e-10, $"{name} at {k}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(DiscreteDistributions))]
	public void Discrete_Samples_Match_The_Analytic_Moments(IDiscreteDistribution distribution, string name)
	{
		IRandomProvider random = SeededRandom();
		int[] draws = distribution.Sample(random, SampleCount);

		Assert.IsTrue(draws.All(d => d >= distribution.Minimum && d <= distribution.Maximum), $"{name} drew outside its support");

		double mean = draws.Average();
		double standardError = distribution.StandardDeviation / Math.Sqrt(SampleCount);
		Assert.AreEqual(distribution.Mean, mean, (5.0 * standardError) + 1e-9, $"{name}: sample mean {mean}");

		if (distribution.Variance > 0.0)
		{
			double variance = draws.Sum(d => (d - mean) * (d - mean)) / (SampleCount - 1);
			Assert.AreEqual(distribution.Variance, variance, distribution.Variance * 0.15, $"{name}: sample variance {variance}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(DiscreteDistributions))]
	public void Discrete_Samples_Land_On_Each_Outcome_As_Often_As_Its_Mass_Says(IDiscreteDistribution distribution, string name)
	{
		IRandomProvider random = SeededRandom();
		Dictionary<int, int> counts = [];
		for (int i = 0; i < SampleCount; i++)
		{
			int draw = distribution.Sample(random);
			counts[draw] = counts.TryGetValue(draw, out int seen) ? seen + 1 : 1;
		}

		int top = Math.Min(distribution.Maximum, 60);
		for (int k = distribution.Minimum; k <= top; k++)
		{
			double mass = distribution.Pmf(k);
			if (mass < 0.01)
			{
				continue;
			}

			double observed = counts.TryGetValue(k, out int count) ? count / (double)SampleCount : 0.0;
			Assert.AreEqual(mass, observed, (0.1 * mass) + 0.002, $"{name}: outcome {k} came up {observed:P2} of the time against a mass of {mass:P2}");
		}
	}

	// ---- reference values ----------------------------------------------------------------------

	[TestMethod]
	public void Normal_Matches_Published_Values()
	{
		NormalDistributionProvider standard = new();

		Assert.AreEqual(0.5, standard.Cdf(0.0), Tight);
		Assert.AreEqual(0.84134474606854293, standard.Cdf(1.0), 1e-14);
		Assert.AreEqual(0.15865525393145707, standard.Cdf(-1.0), 1e-14);
		Assert.AreEqual(0.97500210485177963, standard.Cdf(1.96), 1e-14);
		Assert.AreEqual(0.0013498980316301035, standard.Cdf(-3.0), 1e-15);
		Assert.AreEqual(1.9599639845400536, standard.Quantile(0.975), 1e-12);
		Assert.AreEqual(-3.0902323061678132, standard.Quantile(0.001), 1e-12);
		Assert.AreEqual(0.3989422804014327, standard.Pdf(0.0), 1e-15);

		Assert.AreEqual(0.0, standard.Mean, Tight);
		Assert.AreEqual(1.0, standard.Variance, Tight);
		Assert.AreEqual(0.0, standard.Median, Tight);

		NormalDistributionProvider shifted = new(3.5, 2.0);
		Assert.AreEqual(0.5, shifted.Cdf(3.5), Tight);
		Assert.AreEqual(0.97500210485177963, shifted.Cdf(3.5 + (1.96 * 2.0)), 1e-14);
		Assert.AreEqual(4.0, shifted.Variance, Tight);
	}

	[TestMethod]
	public void Normal_Keeps_Its_Precision_Far_Out_In_The_Tail()
	{
		// Subtracting the CDF from one would leave roughly 6.1e-16 here, having thrown away all but a
		// digit of the answer to rounding. The survival function computes the tail directly, which is
		// the reason it is declared rather than defaulted.
		NormalDistributionProvider standard = new();

		// Tolerances are relative: the incomplete gamma route carries the tail to a few parts in 10^13,
		// which is thirteen digits more than subtraction would have left.
		Assert.AreEqual(9.8658764503770119e-10, standard.SurvivalFunction(6.0), 9.8658764503770119e-10 * 1e-12);
		Assert.AreEqual(6.2209605742718194e-16, standard.SurvivalFunction(8.0), 6.2209605742718194e-16 * 1e-12);
		Assert.AreEqual(6.2209605742718194e-16, standard.Cdf(-8.0), 6.2209605742718194e-16 * 1e-12);
	}

	[TestMethod]
	public void Uniform_Matches_Its_Closed_Form()
	{
		UniformDistributionProvider unit = new();
		Assert.AreEqual(0.5, unit.Mean, Tight);
		Assert.AreEqual(1.0 / 12.0, unit.Variance, Tight);
		Assert.AreEqual(0.25, unit.Cdf(0.25), Tight);
		Assert.AreEqual(1.0, unit.Pdf(0.5), Tight);

		UniformDistributionProvider wide = new(-2.0, 5.0);
		Assert.AreEqual(1.5, wide.Mean, Tight);
		Assert.AreEqual(49.0 / 12.0, wide.Variance, Tight);
		Assert.AreEqual(0.0, wide.Cdf(-2.0), Tight);
		Assert.AreEqual(1.0, wide.Cdf(5.0), Tight);
		Assert.AreEqual(1.0 / 7.0, wide.Pdf(0.0), Tight);
		Assert.AreEqual(0.0, wide.Pdf(6.0), Tight);
		Assert.AreEqual(1.5, wide.Quantile(0.5), Tight);
	}

	[TestMethod]
	public void Exponential_Matches_Its_Closed_Form()
	{
		// Interface-typed because Median comes from a default interface implementation, which is only
		// reachable through the interface.
		IContinuousDistribution distribution = new ExponentialDistributionProvider(2.0);

		Assert.AreEqual(0.5, distribution.Mean, Tight);
		Assert.AreEqual(0.25, distribution.Variance, Tight);
		Assert.AreEqual(0.6321205588285577, distribution.Cdf(0.5), 1e-15);
		Assert.AreEqual(1.0 - 0.6321205588285577, distribution.SurvivalFunction(0.5), 1e-15);
		Assert.AreEqual(Math.Log(2.0) / 2.0, distribution.Median, 1e-12);
		Assert.AreEqual(0.0, distribution.Cdf(-1.0), Tight);
		Assert.AreEqual(0.0, distribution.Pdf(-1.0), Tight);
		Assert.AreEqual(2.0, distribution.Pdf(0.0), Tight);
		Assert.AreEqual(double.PositiveInfinity, distribution.Quantile(1.0));

		// Memorylessness: having waited a while says nothing about the wait remaining.
		Assert.AreEqual(distribution.SurvivalFunction(1.0), distribution.SurvivalFunction(3.0) / distribution.SurvivalFunction(2.0), 1e-14);
	}

	[TestMethod]
	public void LogNormal_Matches_Its_Closed_Form()
	{
		LogNormalDistributionProvider standard = new();

		Assert.AreEqual(0.5, standard.Cdf(1.0), Tight);
		Assert.AreEqual(0.82024278610421453, standard.Cdf(2.5), 1e-14);
		Assert.AreEqual(1.6487212707001282, standard.Mean, 1e-14);
		Assert.AreEqual(1.0, standard.Median, 1e-12);
		Assert.AreEqual(0.0, standard.Cdf(0.0), Tight);
		Assert.AreEqual(0.0, standard.Pdf(0.0), Tight);
		Assert.AreEqual(0.0, standard.Quantile(0.0), Tight);

		// The mean sits above the median because the right tail is long: the defining asymmetry.
		Assert.IsTrue(standard.Mean > standard.Median);
	}

	[TestMethod]
	public void Triangular_Matches_Its_Closed_Form()
	{
		TriangularDistributionProvider distribution = new(1.0, 2.0, 6.0);

		Assert.AreEqual(3.0, distribution.Mean, Tight);
		Assert.AreEqual(0.4, distribution.Pdf(2.0), Tight);
		Assert.AreEqual(0.2, distribution.Cdf(2.0), Tight);
		Assert.AreEqual(0.0, distribution.Cdf(1.0), Tight);
		Assert.AreEqual(1.0, distribution.Cdf(6.0), Tight);
		Assert.AreEqual(0.0, distribution.Pdf(0.5), Tight);
		Assert.AreEqual(2.0, distribution.Quantile(0.2), 1e-12);

		// A mode on either bound leaves one side of the triangle empty, which the branch conditions have
		// to keep out of a denominator.
		TriangularDistributionProvider rightAngled = new(0.0, 0.0, 4.0);
		Assert.AreEqual(0.0, rightAngled.Cdf(0.0), Tight);
		Assert.AreEqual(0.75, rightAngled.Cdf(2.0), Tight);
		Assert.AreEqual(0.5, rightAngled.Pdf(0.0), Tight);

		TriangularDistributionProvider leftAngled = new(0.0, 4.0, 4.0);
		Assert.AreEqual(0.25, leftAngled.Cdf(2.0), Tight);
		Assert.AreEqual(0.5, leftAngled.Pdf(4.0), Tight);
		Assert.AreEqual(4.0, leftAngled.Quantile(1.0), Tight);
	}

	[TestMethod]
	public void Bernoulli_Matches_Its_Closed_Form()
	{
		BernoulliDistributionProvider distribution = new(0.3);

		Assert.AreEqual(0.7, distribution.Pmf(0), Tight);
		Assert.AreEqual(0.3, distribution.Pmf(1), Tight);
		Assert.AreEqual(0.0, distribution.Pmf(2), Tight);
		Assert.AreEqual(0.7, distribution.Cdf(0), Tight);
		Assert.AreEqual(1.0, distribution.Cdf(1), Tight);
		Assert.AreEqual(0.3, distribution.Mean, Tight);
		Assert.AreEqual(0.21, distribution.Variance, Tight);
		Assert.AreEqual(0, distribution.Quantile(0.7));
		Assert.AreEqual(1, distribution.Quantile(0.71));
	}

	[TestMethod]
	public void Binomial_Matches_Values_Summed_Independently()
	{
		BinomialDistributionProvider distribution = new(20, 0.3);

		Assert.AreEqual(0.00079792266297611894, distribution.Cdf(0), 1e-15);
		Assert.AreEqual(0.10708680450373086, distribution.Cdf(3), 1e-14);
		Assert.AreEqual(0.60800981220092321, distribution.Cdf(6), 1e-14);
		Assert.AreEqual(0.98285518356874046, distribution.Cdf(10), 1e-14);
		Assert.AreEqual(0.19163898275344238, distribution.Pmf(6), 1e-14);
		Assert.AreEqual(6.0, distribution.Mean, Tight);
		Assert.AreEqual(4.2, distribution.Variance, Tight);
		Assert.AreEqual(1.0, distribution.Cdf(20), Tight);
		Assert.AreEqual(0.0, distribution.Pmf(21), Tight);

		// A degenerate probability puts every outcome on one value, where the general formula would ask
		// for the logarithm of zero.
		BinomialDistributionProvider never = new(5, 0.0);
		Assert.AreEqual(1.0, never.Pmf(0), Tight);
		Assert.AreEqual(0.0, never.Pmf(1), Tight);
		BinomialDistributionProvider always = new(5, 1.0);
		Assert.AreEqual(1.0, always.Pmf(5), Tight);
		Assert.AreEqual(0.0, always.Pmf(4), Tight);
	}

	[TestMethod]
	public void Binomial_Stays_Finite_Where_The_Coefficient_Would_Overflow()
	{
		// 2000 choose 1000 is about 1e600, far past what a double can hold, while the mass it belongs to
		// is an ordinary 0.018. Going through logarithms is what keeps this computable at all, and it is
		// not free: the log coefficient is a difference of two numbers around 10^4, so a few digits go to
		// cancellation before the result is exponentiated. Hence tolerances of a part in 10^11 rather
		// than the part in 10^14 the small-count cases hold to.
		BinomialDistributionProvider distribution = new(2000, 0.5);

		// Both values were computed exactly, as rationals over 2^2000, outside this codebase.
		Assert.AreEqual(1000.0, distribution.Mean, Tight);
		Assert.AreEqual(0.49108049442707286, distribution.Cdf(999), 0.49108049442707286 * 1e-11);
		Assert.AreEqual(0.01783901114585432, distribution.Pmf(1000), 0.01783901114585432 * 1e-11);

		// The distribution is symmetric about 1000, so the two tails and the peak account for all of it.
		Assert.AreEqual(1.0, (2.0 * distribution.Cdf(999)) + distribution.Pmf(1000), 1e-11);
	}

	[TestMethod]
	public void Poisson_Matches_Values_Summed_Independently()
	{
		PoissonDistributionProvider distribution = new(4.5);

		Assert.AreEqual(0.011108996538242306, distribution.Cdf(0), 1e-15);
		Assert.AreEqual(0.34229595583459105, distribution.Cdf(3), 1e-14);
		Assert.AreEqual(0.70293043486082774, distribution.Cdf(5), 1e-14);
		Assert.AreEqual(0.99919486211967901, distribution.Cdf(12), 1e-14);
		Assert.AreEqual(0.011108996538242306, distribution.Pmf(0), 1e-15);
		Assert.AreEqual(4.5, distribution.Mean, Tight);
		Assert.AreEqual(4.5, distribution.Variance, Tight);
	}

	[TestMethod]
	public void Poisson_Samples_Correctly_Above_Its_Direct_Sampling_Limit()
	{
		// Above the limit the sampler switches from multiplying uniforms to inverting the CDF, so the
		// crossover needs its own coverage.
		IDiscreteDistribution distribution = new PoissonDistributionProvider(120.0);
		IRandomProvider random = SeededRandom();

		int[] draws = distribution.Sample(random, 20000);
		double mean = draws.Average();

		Assert.AreEqual(120.0, mean, 1.0, $"sample mean {mean}");
		Assert.IsTrue(draws.All(d => d >= 0));
	}

	[TestMethod]
	public void Geometric_Matches_Its_Closed_Form()
	{
		GeometricDistributionProvider distribution = new(0.25);

		Assert.AreEqual(0.25, distribution.Pmf(0), Tight);
		Assert.AreEqual(0.75 * 0.25, distribution.Pmf(1), Tight);
		Assert.AreEqual(0.68359375, distribution.Cdf(3), 1e-15);
		Assert.AreEqual(3.0, distribution.Mean, Tight);
		Assert.AreEqual(12.0, distribution.Variance, Tight);
		Assert.AreEqual(0, distribution.Quantile(0.25));
		Assert.AreEqual(1, distribution.Quantile(0.26));
		Assert.AreEqual(int.MaxValue, distribution.Quantile(1.0));

		// A certain success never fails first.
		GeometricDistributionProvider certain = new(1.0);
		Assert.AreEqual(1.0, certain.Pmf(0), Tight);
		Assert.AreEqual(0.0, certain.Mean, Tight);
		Assert.AreEqual(0, certain.Quantile(1.0));
	}

	[TestMethod]
	public void Categorical_Normalises_Its_Weights()
	{
		CategoricalDistributionProvider distribution = new([1.0, 3.0, 0.0, 6.0]);

		Assert.AreEqual(0.1, distribution.Pmf(0), Tight);
		Assert.AreEqual(0.3, distribution.Pmf(1), Tight);
		Assert.AreEqual(0.0, distribution.Pmf(2), Tight);
		Assert.AreEqual(0.6, distribution.Pmf(3), Tight);
		Assert.AreEqual(0.0, distribution.Pmf(4), Tight);
		Assert.AreEqual(0.4, distribution.Cdf(1), Tight);
		Assert.AreEqual(1.0, distribution.Cdf(3), Tight);
		Assert.AreEqual(3, distribution.Maximum);
		Assert.AreEqual((0 * 0.1) + (1 * 0.3) + (3 * 0.6), distribution.Mean, Tight);

		// Weights are relative, so scaling them all leaves the distribution unchanged.
		CategoricalDistributionProvider scaled = new([100.0, 300.0, 0.0, 600.0]);
		for (int k = 0; k <= 3; k++)
		{
			Assert.AreEqual(distribution.Pmf(k), scaled.Pmf(k), Tight);
		}
	}

	[TestMethod]
	public void Categorical_Never_Draws_A_Zero_Weighted_Outcome()
	{
		IDiscreteDistribution distribution = new CategoricalDistributionProvider([1.0, 0.0, 1.0]);
		IRandomProvider random = SeededRandom();

		for (int i = 0; i < 20000; i++)
		{
			Assert.AreNotEqual(1, distribution.Sample(random));
		}
	}

	// ---- argument validation -------------------------------------------------------------------

	[TestMethod]
	public void Constructors_Reject_Parameters_That_Would_Not_Describe_A_Distribution()
	{
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new UniformDistributionProvider(1.0, 1.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new UniformDistributionProvider(2.0, 1.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new UniformDistributionProvider(double.NaN, 1.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new UniformDistributionProvider(0.0, double.PositiveInfinity));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NormalDistributionProvider(0.0, 0.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NormalDistributionProvider(0.0, -1.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NormalDistributionProvider(double.NaN, 1.0));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExponentialDistributionProvider(0.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ExponentialDistributionProvider(-1.0));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LogNormalDistributionProvider(0.0, 0.0));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TriangularDistributionProvider(0.0, 0.0, 0.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TriangularDistributionProvider(0.0, 5.0, 4.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TriangularDistributionProvider(0.0, -1.0, 4.0));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BernoulliDistributionProvider(1.5));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BernoulliDistributionProvider(double.NaN));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BinomialDistributionProvider(-1, 0.5));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BinomialDistributionProvider(10, 1.5));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PoissonDistributionProvider(0.0));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GeometricDistributionProvider(0.0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GeometricDistributionProvider(1.5));

		Assert.ThrowsExactly<ArgumentNullException>(() => new CategoricalDistributionProvider(null!));
		Assert.ThrowsExactly<ArgumentException>(() => new CategoricalDistributionProvider([]));
		Assert.ThrowsExactly<ArgumentException>(() => new CategoricalDistributionProvider([0.0, 0.0]));
		Assert.ThrowsExactly<ArgumentException>(() => new CategoricalDistributionProvider([1.0, -1.0]));
		Assert.ThrowsExactly<ArgumentException>(() => new CategoricalDistributionProvider([1.0, double.PositiveInfinity]));
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Rejects_A_Probability_Outside_The_Unit_Interval(IContinuousDistribution distribution, string name)
	{
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distribution.Quantile(-0.1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distribution.Quantile(1.1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distribution.Quantile(double.NaN), name);
	}

	[TestMethod]
	[DynamicData(nameof(DiscreteDistributions))]
	public void Discrete_Rejects_A_Probability_Outside_The_Unit_Interval(IDiscreteDistribution distribution, string name)
	{
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distribution.Quantile(-0.1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distribution.Quantile(1.1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distribution.Quantile(double.NaN), name);
	}

	[TestMethod]
	[DynamicData(nameof(ContinuousDistributions))]
	public void Continuous_Sampling_Rejects_A_Missing_Generator(IContinuousDistribution distribution, string name)
	{
		Assert.ThrowsExactly<ArgumentNullException>(() => distribution.Sample(null!), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => distribution.Sample(SeededRandom(), -1), name);
	}

	/// <summary>
	/// Substitutes a workable value for an unbounded support bound.
	/// </summary>
	/// <param name="bound">The support bound, which may be infinite.</param>
	/// <param name="fallback">The value to use when it is.</param>
	/// <returns>A finite value to sweep from or to.</returns>
	private static double Finite(double bound, double fallback) => double.IsInfinity(bound) ? fallback : bound;
}
