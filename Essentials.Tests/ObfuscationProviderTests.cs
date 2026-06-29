// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Text;
using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ObfuscationProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddObfuscationProviders();
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
		IObfuscationProvider provider = new ObfuscationProviders.Base64();
		string original = "string obfuscate via base64";
		string obfuscated = provider.Obfuscate(original);
		byte[] obfuscatedBytes = System.Text.Encoding.UTF8.GetBytes(obfuscated);
		byte[] restoredBytes = provider.Deobfuscate(obfuscatedBytes);
		string restored = System.Text.Encoding.UTF8.GetString(restoredBytes);
		Assert.AreEqual(original, restored);
	}

	[TestMethod]
	public void Obfuscation_Hex_Roundtrip_String()
	{
		IObfuscationProvider provider = new ObfuscationProviders.Hex();
		string original = "string obfuscate via hex";
		string obfuscated = provider.Obfuscate(original);
		byte[] obfuscatedBytes = System.Text.Encoding.UTF8.GetBytes(obfuscated);
		byte[] restoredBytes = provider.Deobfuscate(obfuscatedBytes);
		string restored = System.Text.Encoding.UTF8.GetString(restoredBytes);
		Assert.AreEqual(original, restored);
	}
}