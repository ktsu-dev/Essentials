// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using ktsu.Essentials;
using ktsu.Essentials.RandomProviders.Crypto;
using ktsu.Essentials.RandomProviders.Native;
using ktsu.Essentials.RandomProviders.Pcg;
using ktsu.Essentials.RandomProviders.Xoshiro;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Contract tests applied to every registered <see cref="IRandomProvider"/>, plus the guarantees each
/// implementation makes on its own.
/// </summary>
/// <remarks>
/// A generator cannot be tested by comparing it against an expected answer, because there isn't one. The
/// tests here instead pin the three things that can be checked: the bounds every method promises, the
/// invariants of the collection helpers, and — for the two seedable generators — a reference sequence
/// produced by an independent implementation of the same published algorithm.
/// </remarks>
[TestClass]
public class RandomProviderTests
{
	/// <summary>Draws per statistical check. Large enough for the bounds below to be tight, small enough to stay fast.</summary>
	private const int SampleCount = 20000;

	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddCommon();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> RandomProviders => BuildProvider().EnumerateProviders<IRandomProvider>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public void DI_Registers_All_Random_Providers()
	{
		using ServiceProvider serviceProvider = BuildProvider();

		string[] actual = [.. serviceProvider.GetServices<IRandomProvider>().Select(p => p.GetType().Name).OrderBy(n => n, StringComparer.Ordinal)];

		string[] expected = ["CryptoRandomProvider", "NativeRandomProvider", "PcgRandomProvider", "XoshiroRandomProvider"];
		CollectionAssert.AreEqual(expected, actual);
	}

	[TestMethod]
	public void DI_Hands_Stateful_Providers_Their_Own_Instance()
	{
		using ServiceProvider serviceProvider = BuildProvider();

		// The three stateful generators are transients precisely so that two consumers never share one,
		// while the stateless cryptographic provider is a singleton.
		Assert.AreNotSame(serviceProvider.GetRequiredService<XoshiroRandomProvider>(), serviceProvider.GetRequiredService<XoshiroRandomProvider>());
		Assert.AreNotSame(serviceProvider.GetRequiredService<PcgRandomProvider>(), serviceProvider.GetRequiredService<PcgRandomProvider>());
		Assert.AreNotSame(serviceProvider.GetRequiredService<NativeRandomProvider>(), serviceProvider.GetRequiredService<NativeRandomProvider>());
		Assert.AreSame(serviceProvider.GetRequiredService<CryptoRandomProvider>(), serviceProvider.GetRequiredService<CryptoRandomProvider>());
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextBytes_Fills_Buffers_Of_Every_Length(IRandomProvider random, string name)
	{
		// Lengths either side of the word size each generator fills in, so a partial tail is covered.
		foreach (int length in new[] { 0, 1, 3, 4, 7, 8, 9, 15, 16, 17, 64, 1000 })
		{
			byte[] buffer = random.NextBytes(length);
			Assert.HasCount(length, buffer, $"{name} returned the wrong length for {length}");
		}

		byte[] large = random.NextBytes(4096);
		Assert.IsTrue(large.Distinct().Count() > 200, $"{name} produced suspiciously few distinct byte values");
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextBytes_Rejects_A_Negative_Count(IRandomProvider random, string name)
	{
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextBytes(-1), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextInt32_Stays_Within_Its_Bounds(IRandomProvider random, string name)
	{
		for (int i = 0; i < SampleCount; i++)
		{
			int value = random.NextInt32();
			Assert.IsTrue(value is >= 0 and < int.MaxValue, $"{name} produced {value}");

			int bounded = random.NextInt32(10);
			Assert.IsTrue(bounded is >= 0 and < 10, $"{name} produced {bounded}");

			int ranged = random.NextInt32(-5, 5);
			Assert.IsTrue(ranged is >= -5 and < 5, $"{name} produced {ranged}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextInt32_Treats_An_Empty_Range_As_Its_Lower_Bound(IRandomProvider random, string name)
	{
		Assert.AreEqual(7, random.NextInt32(7, 7), name);
		Assert.AreEqual(0, random.NextInt32(0), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextInt32_Rejects_Inverted_And_Negative_Bounds(IRandomProvider random, string name)
	{
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextInt32(-1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextInt32(5, 4), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextInt64_Stays_Within_Its_Bounds(IRandomProvider random, string name)
	{
		for (int i = 0; i < 2000; i++)
		{
			long value = random.NextInt64();
			Assert.IsTrue(value is >= 0 and < long.MaxValue, $"{name} produced {value}");

			long ranged = random.NextInt64(long.MinValue, long.MaxValue);
			Assert.IsTrue(ranged < long.MaxValue, $"{name} produced {ranged}");

			long small = random.NextInt64(-3, 4);
			Assert.IsTrue(small is >= -3 and < 4, $"{name} produced {small}");
		}

		Assert.AreEqual(9L, random.NextInt64(9, 9), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextInt64(-1L), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextInt64(5L, 4L), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextDouble_Stays_Inside_The_Unit_Interval(IRandomProvider random, string name)
	{
		for (int i = 0; i < SampleCount; i++)
		{
			double value = random.NextDouble();
			Assert.IsTrue(value is >= 0.0 and < 1.0, $"{name} produced {value}");

			double exclusive = random.NextDoubleExclusive();
			Assert.IsTrue(exclusive is > 0.0 and < 1.0, $"{name} produced {exclusive}");

			float single = random.NextSingle();
			Assert.IsTrue(single is >= 0.0f and < 1.0f, $"{name} produced {single}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextDouble_Stays_Within_An_Explicit_Range(IRandomProvider random, string name)
	{
		for (int i = 0; i < 2000; i++)
		{
			double value = random.NextDouble(-2.5, 7.5);
			Assert.IsTrue(value is >= -2.5 and < 7.5, $"{name} produced {value}");
		}

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextDouble(1.0, 0.0), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextDouble(double.NaN, 1.0), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextDouble(0.0, double.PositiveInfinity), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextBoolean_Honours_A_Certain_Probability(IRandomProvider random, string name)
	{
		for (int i = 0; i < 1000; i++)
		{
			Assert.IsFalse(random.NextBoolean(0.0), name);
			Assert.IsTrue(random.NextBoolean(1.0), name);
		}

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextBoolean(-0.1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextBoolean(1.1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextBoolean(double.NaN), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextBoolean_Lands_Near_Its_Stated_Probability(IRandomProvider random, string name)
	{
		int successes = 0;
		for (int i = 0; i < SampleCount; i++)
		{
			if (random.NextBoolean(0.25))
			{
				successes++;
			}
		}

		// The standard error of the count is sqrt(n p q) ≈ 61, so five thousand plus or minus four
		// hundred is roughly six-and-a-half sigma: wide enough never to flake, narrow enough that a
		// generator returning the wrong proportion cannot slip through.
		Assert.IsTrue(Math.Abs(successes - 5000) < 400, $"{name} produced {successes} successes out of {SampleCount}, expected about 5000");
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void NextInt32_Covers_A_Small_Range_Evenly(IRandomProvider random, string name)
	{
		// A range that does not divide 2^32 evenly, so a generator that folded the leftovers back into
		// the low end rather than rejecting them would show up as a skew towards the first buckets.
		const int Buckets = 7;
		int[] counts = new int[Buckets];
		for (int i = 0; i < SampleCount; i++)
		{
			counts[random.NextInt32(Buckets)]++;
		}

		double expected = (double)SampleCount / Buckets;
		foreach (int count in counts)
		{
			Assert.IsTrue(Math.Abs(count - expected) < expected * 0.15, $"{name} produced an uneven spread: {string.Join(", ", counts)}");
		}
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void Shuffle_Keeps_Every_Element(IRandomProvider random, string name)
	{
		List<int> items = [.. Enumerable.Range(0, 200)];
		random.Shuffle(items);

		CollectionAssert.AreEquivalent(Enumerable.Range(0, 200).ToArray(), items, name);
		Assert.IsFalse(items.SequenceEqual(Enumerable.Range(0, 200)), $"{name} left a 200 element list in its original order");
		Assert.ThrowsExactly<ArgumentNullException>(() => random.Shuffle<int>(null!), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void Shuffle_Reaches_Every_Ordering(IRandomProvider random, string name)
	{
		// Three elements have six orderings. A shuffle that only rotated, or that never moved the first
		// element, would produce fewer.
		HashSet<string> orderings = [];
		for (int i = 0; i < 2000; i++)
		{
			List<int> items = [1, 2, 3];
			random.Shuffle(items);
			orderings.Add(string.Join(",", items));
		}

		Assert.HasCount(6, orderings, $"{name} reached only {orderings.Count} of the 6 orderings");
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void Choose_Returns_A_Member_Of_The_Collection(IRandomProvider random, string name)
	{
		string[] items = ["a", "b", "c"];
		HashSet<string> seen = [];
		for (int i = 0; i < 500; i++)
		{
			seen.Add(random.Choose(items));
		}

		CollectionAssert.AreEquivalent(items, seen.ToArray(), name);
		Assert.ThrowsExactly<ArgumentNullException>(() => random.Choose<string>(null!), name);
		Assert.ThrowsExactly<ArgumentException>(() => random.Choose(Array.Empty<string>()), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void Choose_Never_Returns_A_Zero_Weighted_Element(IRandomProvider random, string name)
	{
		string[] items = ["never", "sometimes", "often"];
		double[] weights = [0.0, 1.0, 9.0];

		int often = 0;
		for (int i = 0; i < SampleCount; i++)
		{
			string chosen = random.Choose(items, weights);
			Assert.AreNotEqual("never", chosen, name);
			if (chosen == "often")
			{
				often++;
			}
		}

		Assert.IsTrue(Math.Abs(often - (SampleCount * 0.9)) < SampleCount * 0.02, $"{name} chose 'often' {often} times out of {SampleCount}");
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void Choose_Rejects_Malformed_Weights(IRandomProvider random, string name)
	{
		string[] items = ["a", "b"];

		Assert.ThrowsExactly<ArgumentNullException>(() => random.Choose(items, null!), name);
		Assert.ThrowsExactly<ArgumentException>(() => random.Choose(items, [1.0]), name);
		Assert.ThrowsExactly<ArgumentException>(() => random.Choose(items, [1.0, -1.0]), name);
		Assert.ThrowsExactly<ArgumentException>(() => random.Choose(items, [0.0, 0.0]), name);
		Assert.ThrowsExactly<ArgumentException>(() => random.Choose(items, [1.0, double.NaN]), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void Sample_Draws_Distinct_Elements(IRandomProvider random, string name)
	{
		int[] population = [.. Enumerable.Range(0, 50)];

		for (int i = 0; i < 200; i++)
		{
			IReadOnlyList<int> drawn = random.Sample(population, 10);
			Assert.HasCount(10, drawn, name);
			Assert.HasCount(10, drawn.Distinct().ToArray(), $"{name} drew a duplicate: {string.Join(", ", drawn)}");
			Assert.IsTrue(drawn.All(population.Contains), name);
		}

		CollectionAssert.AreEqual(Enumerable.Range(0, 50).ToArray(), population, $"{name} modified the population it was given");
		Assert.IsEmpty(random.Sample(population, 0), name);
		Assert.HasCount(50, random.Sample(population, 50), name);
	}

	[TestMethod]
	[DynamicData(nameof(RandomProviders))]
	public void Sample_Rejects_Impossible_Draws(IRandomProvider random, string name)
	{
		int[] population = [1, 2, 3];

		Assert.ThrowsExactly<ArgumentNullException>(() => random.Sample<int>(null!, 1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.Sample(population, -1), name);
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.Sample(population, 4), name);
	}

	[TestMethod]
	public void Xoshiro_Reproduces_Its_Reference_Sequence()
	{
		// Produced by an independent transcription of the published xoshiro256** reference, seeded
		// through SplitMix64 exactly as that reference prescribes. This is what makes the sequence a
		// property of the package rather than of the machine it runs on, so a change to it is a
		// breaking change and this test is how it gets noticed.
		ulong[] expected =
		[
			0x15780B2E0C2EC716UL,
			0x6104D9866D113A7EUL,
			0xAE17533239E499A1UL,
			0xECB8AD4703B360A1UL,
			0xFDE6DC7FE2EC5E64UL,
			0xC50DA53101795238UL,
			0xB82154855A65DDB2UL,
			0xD99A2743EBE60087UL,
		];

		XoshiroRandomProvider random = new(42UL);
		ulong[] actual = [.. Enumerable.Range(0, expected.Length).Select(_ => random.NextUInt64())];

		CollectionAssert.AreEqual(expected, actual);
	}

	[TestMethod]
	public void Pcg_Reproduces_Its_Reference_Sequence()
	{
		// As above, from an independent transcription of the published PCG-XSH-RR reference.
		uint[] expected =
		[
			0x1D5DDCB9u,
			0x726C11A4u,
			0xD44FB6C0u,
			0x28B658C3u,
			0x468DEE08u,
			0x18ABF114u,
			0x0D43FD0Au,
			0x19FF01A7u,
		];

		PcgRandomProvider random = new(42UL);
		uint[] actual = [.. Enumerable.Range(0, expected.Length).Select(_ => random.NextUInt32())];

		CollectionAssert.AreEqual(expected, actual);
	}

	[TestMethod]
	public void Pcg_Streams_Diverge_From_The_Same_Seed()
	{
		uint[] expected = [0x7499DA3Fu, 0x3C421650u, 0xA50E7598u, 0xB7647F77u];

		PcgRandomProvider stream7 = new(42UL, 7UL);
		uint[] actual = [.. Enumerable.Range(0, expected.Length).Select(_ => stream7.NextUInt32())];
		CollectionAssert.AreEqual(expected, actual);

		PcgRandomProvider defaultStream = new(42UL);
		Assert.AreNotEqual(defaultStream.NextUInt32(), new PcgRandomProvider(42UL, 7UL).NextUInt32());
	}

	[TestMethod]
	public void Seeded_Providers_Repeat_Themselves_And_Differ_By_Seed()
	{
		AssertSeedBehaviour(seed => new XoshiroRandomProvider(seed), nameof(XoshiroRandomProvider));
		AssertSeedBehaviour(seed => new PcgRandomProvider(seed), nameof(PcgRandomProvider));
	}

	[TestMethod]
	public void Native_Repeats_Itself_For_A_Given_Seed()
	{
		// System.Random makes no promise about which values a seed produces, only that it produces the
		// same ones twice in one process, so that is all this asserts.
		ulong[] first = Draw(new NativeRandomProvider(1234));
		ulong[] second = Draw(new NativeRandomProvider(1234));
		ulong[] other = Draw(new NativeRandomProvider(4321));

		CollectionAssert.AreEqual(first, second);
		CollectionAssert.AreNotEqual(first, other);
	}

	[TestMethod]
	public void Word_Sized_Draws_Agree_With_The_Byte_Buffer()
	{
		// NextUInt32 and NextUInt64 are declared by the seedable providers rather than defaulted, so
		// this checks the two routes still describe the same little-endian stream.
		XoshiroRandomProvider words = new(7UL);
		IRandomProvider bytes = new XoshiroRandomProvider(7UL);
		for (int i = 0; i < 16; i++)
		{
			Assert.AreEqual(words.NextUInt64(), BitConverter.ToUInt64(bytes.NextBytes(sizeof(ulong)), 0));
		}

		PcgRandomProvider pcgWords = new(7UL);
		IRandomProvider pcgBytes = new PcgRandomProvider(7UL);
		for (int i = 0; i < 16; i++)
		{
			Assert.AreEqual(pcgWords.NextUInt32(), BitConverter.ToUInt32(pcgBytes.NextBytes(sizeof(uint)), 0));
		}
	}

	private static void AssertSeedBehaviour(Func<ulong, IRandomProvider> factory, string name)
	{
		ulong[] first = Draw(factory(99UL));
		ulong[] second = Draw(factory(99UL));
		ulong[] other = Draw(factory(100UL));

		CollectionAssert.AreEqual(first, second, $"{name} did not repeat itself for the same seed");
		CollectionAssert.AreNotEqual(first, other, $"{name} produced the same sequence from a different seed");
	}

	private static ulong[] Draw(IRandomProvider random) => [.. Enumerable.Range(0, 32).Select(_ => random.NextUInt64())];
}
