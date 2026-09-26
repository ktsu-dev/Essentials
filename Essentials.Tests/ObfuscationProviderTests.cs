// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Text;
using ktsu.Essentials;
using ktsu.Essentials.ObfuscationProviders.Base64;
using ktsu.Essentials.ObfuscationProviders.Composite;
using ktsu.Essentials.ObfuscationProviders.Hex;
using ktsu.Essentials.ObfuscationProviders.Xor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ObfuscationProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddObfuscationProvidersWithComposite();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> ObfuscationProviders => BuildProvider().EnumerateProviders<IObfuscationProvider>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Roundtrip_Bytes(IObfuscationProvider provider, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("obfuscate me with " + providerName);

		byte[] obfuscated = provider.Obfuscate(original);
		byte[] restored = provider.Deobfuscate(obfuscated);

		CollectionAssert.AreEqual(original, restored, $"{providerName} should restore original bytes");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Roundtrip_Stream(IObfuscationProvider provider, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("stream obfuscate with " + providerName);

		using MemoryStream input = new(original);
		using MemoryStream obfuscated = new();
		Assert.IsTrue(provider.TryObfuscate(input, obfuscated), $"{providerName} should obfuscate stream");

		obfuscated.Position = 0;
		using MemoryStream restored = new();
		Assert.IsTrue(provider.TryDeobfuscate(obfuscated, restored), $"{providerName} should deobfuscate stream");

		CollectionAssert.AreEqual(original, restored.ToArray(), $"{providerName} should restore original from stream");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Async_Roundtrip(IObfuscationProvider provider, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("async obfuscate with " + providerName);

		using MemoryStream input = new(original);
		using MemoryStream obfuscated = new();
		Assert.IsTrue(provider.TryObfuscateAsync(input, obfuscated, TestContext.CancellationToken).Result, $"{providerName} async obfuscate");

		obfuscated.Position = 0;
		using MemoryStream restored = new();
		Assert.IsTrue(provider.TryDeobfuscateAsync(obfuscated, restored, TestContext.CancellationToken).Result, $"{providerName} async deobfuscate");

		CollectionAssert.AreEqual(original, restored.ToArray(), $"{providerName} async should restore original");
	}

	[TestMethod]
	public void Obfuscation_Base64_Roundtrip_String()
	{
		IObfuscationProvider provider = new Base64ObfuscationProvider();
		string original = "string obfuscate via base64";

		string obfuscated = provider.Obfuscate(original);
		string restored = provider.Deobfuscate(obfuscated);

		Assert.AreNotEqual(original, obfuscated, "Obfuscated text should not match the input");
		Assert.AreEqual(original, restored);
	}

	[TestMethod]
	public void Obfuscation_Hex_Roundtrip_String()
	{
		IObfuscationProvider provider = new HexObfuscationProvider();
		string original = "string obfuscate via hex";

		string obfuscated = provider.Obfuscate(original);
		string restored = provider.Deobfuscate(obfuscated);

		Assert.AreNotEqual(original, obfuscated, "Obfuscated text should not match the input");
		Assert.AreEqual(original, restored);
	}

	private static IObfuscationProvider CompositeWithFallibleStage()
		=> new CompositeObfuscationProvider([new XorObfuscationProvider(), new Base64ObfuscationProvider()]);

	[TestMethod]
	public void Obfuscation_Composite_TryDeobfuscate_Span_ReturnsFalseWhenAStageFails()
	{
		IObfuscationProvider provider = CompositeWithFallibleStage();
		byte[] invalid = Encoding.UTF8.GetBytes("!!!");
		byte[] destination = new byte[provider.GetMaxDeobfuscatedLength(invalid.Length)];

		Assert.IsFalse(provider.TryDeobfuscate(invalid, destination, out int bytesWritten));
		Assert.AreEqual(0, bytesWritten);
	}

	[TestMethod]
	public void Obfuscation_Composite_TryDeobfuscate_Stream_ReturnsFalseWhenAStageFails()
	{
		IObfuscationProvider provider = CompositeWithFallibleStage();
		using MemoryStream invalid = new(Encoding.UTF8.GetBytes("!!!"));
		using MemoryStream destination = new();

		Assert.IsFalse(provider.TryDeobfuscate(invalid, destination));
		Assert.AreEqual(0, destination.Length, "Nothing should be written when a stage fails");
	}

	[TestMethod]
	public void Obfuscation_Composite_WithFallibleStage_Roundtrips()
	{
		IObfuscationProvider provider = CompositeWithFallibleStage();
		byte[] original = Encoding.UTF8.GetBytes("composite with a base64 stage");

		CollectionAssert.AreEqual(original, provider.Deobfuscate(provider.Obfuscate(original)));
	}
}