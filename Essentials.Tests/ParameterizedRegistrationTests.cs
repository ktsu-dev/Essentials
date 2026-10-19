// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Linq;
using ktsu.Essentials;
using ktsu.Essentials.All;
using ktsu.Essentials.DistributionProviders.Normal;
using ktsu.Essentials.DistributionProviders.Poisson;
using ktsu.Essentials.DistributionProviders.Triangular;
using ktsu.Essentials.EncodingProviders.Base64;
using ktsu.Essentials.EncodingProviders.Hex;
using ktsu.Essentials.ObfuscationProviders.Base64;
using ktsu.Essentials.ObfuscationProviders.BitRotate;
using ktsu.Essentials.ObfuscationProviders.Caesar;
using ktsu.Essentials.ObfuscationProviders.Hex;
using ktsu.Essentials.ObfuscationProviders.Xor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A registration that carries arguments must take effect whichever side of <c>AddEssentials()</c> it is
/// called on. Before issue #132 a call after it was silently discarded, because <c>TryAdd…</c> kept the
/// default instance <c>AddEssentials()</c> had already registered.
/// </summary>
[TestClass]
public class ParameterizedRegistrationTests
{
	private static readonly byte[] Key = [1, 2, 3, 4];
	private static readonly byte[] Plaintext = [10, 20, 30, 40];

	[TestMethod]
	public void Xor_Key_Registered_After_AddEssentials_Takes_Effect()
	{
		ServiceCollection services = new();
		services.AddEssentials();
		services.AddXorObfuscationProvider(Key);

		AssertResolvesXorKey(services);
	}

	[TestMethod]
	public void Xor_Key_Registered_Before_AddEssentials_Survives_It()
	{
		ServiceCollection services = new();
		services.AddXorObfuscationProvider(Key);
		services.AddEssentials();

		AssertResolvesXorKey(services);
	}

	[TestMethod]
	public void Normal_Parameters_Take_Effect_On_Either_Side_Of_AddEssentials()
	{
		ServiceCollection after = new();
		after.AddEssentials();
		after.AddNormalDistributionProvider(100.0, 15.0);
		AssertResolvesNormal(after, 100.0, 15.0);

		ServiceCollection before = new();
		before.AddNormalDistributionProvider(100.0, 15.0);
		before.AddEssentials();
		AssertResolvesNormal(before, 100.0, 15.0);
	}

	[TestMethod]
	public void Replacing_A_Registration_Keeps_Its_Place_And_Lists_It_Once()
	{
		ServiceCollection services = new();
		services.AddEssentials();
		services.AddPoissonDistributionProvider(7.0);
		services.AddCaesarObfuscationProvider(3);
		services.AddBitRotateObfuscationProvider(5);

		using ServiceProvider provider = services.BuildServiceProvider();

		// Poisson stays the default discrete distribution and Xor the default obfuscator, so replacing
		// one in place leaves the order AddEssentials() chose for the single-service resolution.
		IDiscreteDistribution discrete = provider.GetRequiredService<IDiscreteDistribution>();
		Assert.IsInstanceOfType<PoissonDistributionProvider>(discrete);
		Assert.AreEqual(7.0, ((PoissonDistributionProvider)discrete).Rate);
		Assert.IsInstanceOfType<XorObfuscationProvider>(provider.GetRequiredService<IObfuscationProvider>());

		Assert.AreEqual(1, provider.GetServices<IDiscreteDistribution>().Count(d => d is PoissonDistributionProvider));
		Assert.AreEqual(1, provider.GetServices<IObfuscationProvider>().Count(o => o is CaesarObfuscationProvider));
		Assert.AreEqual(1, provider.GetServices<IObfuscationProvider>().Count(o => o is BitRotateObfuscationProvider));

		CollectionAssert.AreEqual(
			Obfuscate(new CaesarObfuscationProvider(3)),
			Obfuscate(provider.GetRequiredService<CaesarObfuscationProvider>()));
		CollectionAssert.AreEqual(
			Obfuscate(new BitRotateObfuscationProvider(5)),
			Obfuscate(provider.GetServices<IObfuscationProvider>().OfType<BitRotateObfuscationProvider>().Single()));
	}

	[TestMethod]
	public void Hex_Encoder_Takes_Effect_On_Either_Side_Of_AddEssentials()
	{
		// Before issue #151 the Hex and Base64 overloads still used TryAdd…, so an encoder passed after
		// AddEssentials() was dropped and data was obfuscated with the default encoder instead.
		ServiceCollection after = new();
		after.AddEssentials();
		after.AddHexObfuscationProvider(new Base64EncodingProvider());
		AssertResolvesEncoder<HexObfuscationProvider>(after, new HexObfuscationProvider(new Base64EncodingProvider()));

		ServiceCollection before = new();
		before.AddHexObfuscationProvider(new Base64EncodingProvider());
		before.AddEssentials();
		AssertResolvesEncoder<HexObfuscationProvider>(before, new HexObfuscationProvider(new Base64EncodingProvider()));
	}

	[TestMethod]
	public void Base64_Encoder_Takes_Effect_On_Either_Side_Of_AddEssentials()
	{
		ServiceCollection after = new();
		after.AddEssentials();
		after.AddBase64ObfuscationProvider(new HexEncodingProvider());
		AssertResolvesEncoder<Base64ObfuscationProvider>(after, new Base64ObfuscationProvider(new HexEncodingProvider()));

		ServiceCollection before = new();
		before.AddBase64ObfuscationProvider(new HexEncodingProvider());
		before.AddEssentials();
		AssertResolvesEncoder<Base64ObfuscationProvider>(before, new Base64ObfuscationProvider(new HexEncodingProvider()));
	}

	[TestMethod]
	public void Xor_Registered_Only_As_The_Interface_Before_AddEssentials_Keeps_Its_Key()
	{
		// Before issue #156 AddEssentials() saw no concrete-type registration and overwrote this one
		// in place with the default-key instance.
		ServiceCollection services = new();
		services.AddSingleton<IObfuscationProvider>(new XorObfuscationProvider(Key));
		services.AddEssentials();

		using ServiceProvider provider = services.BuildServiceProvider();

		byte[] expected = Obfuscate(new XorObfuscationProvider(Key));
		Assert.AreSequenceEqual(expected, Obfuscate(provider.GetRequiredService<IObfuscationProvider>()));
		XorObfuscationProvider xor = provider.GetServices<IObfuscationProvider>().OfType<XorObfuscationProvider>().Single();
		Assert.AreSequenceEqual(expected, Obfuscate(xor));
	}

	[TestMethod]
	public void Xor_Registered_Only_As_Its_Concrete_Type_Before_AddEssentials_Joins_The_Set()
	{
		// Before issue #156 this counted as registered but had no interface registration to move,
		// so Xor was missing from IEnumerable<IObfuscationProvider> altogether.
		XorObfuscationProvider configured = new(Key);
		ServiceCollection services = new();
		services.AddSingleton(configured);
		services.AddEssentials();

		using ServiceProvider provider = services.BuildServiceProvider();

		Assert.AreSame(configured, provider.GetServices<IObfuscationProvider>().OfType<XorObfuscationProvider>().Single());
		Assert.AreSame(configured, provider.GetRequiredService<IObfuscationProvider>());
		Assert.AreSame(configured, provider.GetRequiredService<XorObfuscationProvider>());
	}

	[TestMethod]
	public void Xor_Registered_Only_As_Its_Concrete_Type_Is_Still_Replaced_By_A_Later_Call()
	{
		ServiceCollection services = new();
		services.AddSingleton(new XorObfuscationProvider([9, 9]));
		services.AddEssentials();
		services.AddXorObfuscationProvider(Key);

		AssertResolvesXorKey(services);
	}

	[TestMethod]
	public void Normal_Registered_Only_As_The_Interface_Before_AddEssentials_Keeps_Its_Parameters()
	{
		ServiceCollection services = new();
		services.AddSingleton<IContinuousDistribution>(new NormalDistributionProvider(100.0, 15.0));
		services.AddEssentials();

		using ServiceProvider provider = services.BuildServiceProvider();

		IContinuousDistribution distribution = provider.GetRequiredService<IContinuousDistribution>();
		Assert.IsInstanceOfType<NormalDistributionProvider>(distribution);
		Assert.AreEqual(100.0, distribution.Mean, 1e-12);
		Assert.ContainsSingle(d => d is NormalDistributionProvider, provider.GetServices<IContinuousDistribution>());
	}

	[TestMethod]
	public void A_Second_Call_Replaces_A_Distribution_Outside_AddEssentials()
	{
		ServiceCollection services = new();
		services.AddTriangularDistributionProvider(0.0, 1.0, 2.0);
		services.AddTriangularDistributionProvider(2.0, 5.0, 14.0);

		using ServiceProvider provider = services.BuildServiceProvider();

		TriangularDistributionProvider triangular = provider.GetRequiredService<TriangularDistributionProvider>();
		Assert.AreEqual(14.0, triangular.Maximum);
		Assert.AreSame(triangular, provider.GetServices<IContinuousDistribution>().Single());
	}

	private static byte[] Obfuscate(IObfuscationProvider provider) => provider.Obfuscate(Plaintext);

	private static void AssertResolvesXorKey(ServiceCollection services)
	{
		using ServiceProvider provider = services.BuildServiceProvider();

		byte[] expected = Obfuscate(new XorObfuscationProvider(Key));
		CollectionAssert.AreEqual(expected, Obfuscate(provider.GetRequiredService<IObfuscationProvider>()));
		CollectionAssert.AreEqual(expected, Obfuscate(provider.GetRequiredService<XorObfuscationProvider>()));
		Assert.AreEqual(1, provider.GetServices<IObfuscationProvider>().Count(o => o is XorObfuscationProvider));
	}

	private static void AssertResolvesEncoder<TProvider>(ServiceCollection services, TProvider configured)
		where TProvider : class, IObfuscationProvider
	{
		using ServiceProvider provider = services.BuildServiceProvider();

		byte[] expected = Obfuscate(configured);
		CollectionAssert.AreEqual(expected, Obfuscate(provider.GetRequiredService<TProvider>()));
		CollectionAssert.AreEqual(expected, Obfuscate(provider.GetServices<IObfuscationProvider>().OfType<TProvider>().Single()));

		// Xor stays the default obfuscator, so replacing this one leaves the order AddEssentials() chose.
		Assert.IsInstanceOfType<XorObfuscationProvider>(provider.GetRequiredService<IObfuscationProvider>());
	}

	private static void AssertResolvesNormal(ServiceCollection services, double mean, double standardDeviation)
	{
		using ServiceProvider provider = services.BuildServiceProvider();

		IContinuousDistribution distribution = provider.GetRequiredService<IContinuousDistribution>();
		Assert.IsInstanceOfType<NormalDistributionProvider>(distribution);
		Assert.AreEqual(mean, distribution.Mean, 1e-12);
		Assert.AreEqual(standardDeviation * standardDeviation, distribution.Variance, 1e-9);
		Assert.AreSame(distribution, provider.GetRequiredService<NormalDistributionProvider>());
	}
}
