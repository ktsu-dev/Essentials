// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.All;
using ktsu.Essentials.CompressionProviders.Gzip;
using ktsu.Essentials.DistributionProviders.Normal;
using ktsu.Essentials.DistributionProviders.Poisson;
using ktsu.Essentials.EncodingProviders.Base64;
using ktsu.Essentials.HashProviders.SHA256;
using ktsu.Essentials.KeyedHashProviders.HmacSha256;
using ktsu.Essentials.ObfuscationProviders.Xor;
using ktsu.Essentials.PersistenceProviders.FileSystem;
using ktsu.Essentials.RandomProviders.Crypto;
using ktsu.Essentials.SerializationProviders.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Resolving a single provider by interface after <see cref="ktsu.Essentials.All.ServiceCollectionExtensions.AddEssentials"/>
/// must return the documented default for its category, not whichever package happens to register last.
/// </summary>
[TestClass]
public class DefaultProviderTests
{
	public TestContext TestContext { get; set; } = null!;

	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddEssentials();
		return services.BuildServiceProvider();
	}

	[TestMethod]
	public void AddEssentials_Resolves_The_Documented_Default_Per_Category()
	{
		using ServiceProvider serviceProvider = BuildProvider();

		Assert.IsInstanceOfType<JsonSerializationProvider>(serviceProvider.GetRequiredService<ISerializationProvider>());
		Assert.IsInstanceOfType<SHA256HashProvider>(serviceProvider.GetRequiredService<IHashProvider>());
		Assert.IsInstanceOfType<HmacSha256KeyedHashProvider>(serviceProvider.GetRequiredService<IKeyedHashProvider>());
		Assert.IsInstanceOfType<GzipCompressionProvider>(serviceProvider.GetRequiredService<ICompressionProvider>());
		Assert.IsInstanceOfType<Base64EncodingProvider>(serviceProvider.GetRequiredService<IEncodingProvider>());
		Assert.IsInstanceOfType<XorObfuscationProvider>(serviceProvider.GetRequiredService<IObfuscationProvider>());
		Assert.IsInstanceOfType<CryptoRandomProvider>(serviceProvider.GetRequiredService<IRandomProvider>());
		Assert.IsInstanceOfType<NormalDistributionProvider>(serviceProvider.GetRequiredService<IContinuousDistribution>());
		Assert.IsInstanceOfType<PoissonDistributionProvider>(serviceProvider.GetRequiredService<IDiscreteDistribution>());
	}

	[TestMethod]
	public void AddEssentials_Lists_Each_Implementation_Once()
	{
		using ServiceProvider serviceProvider = BuildProvider();

		AssertDistinct(serviceProvider.GetServices<ISerializationProvider>());
		AssertDistinct(serviceProvider.GetServices<IHashProvider>());
		AssertDistinct(serviceProvider.GetServices<IKeyedHashProvider>());
		AssertDistinct(serviceProvider.GetServices<ICompressionProvider>());
		AssertDistinct(serviceProvider.GetServices<IEncodingProvider>());
		AssertDistinct(serviceProvider.GetServices<IObfuscationProvider>());
		AssertDistinct(serviceProvider.GetServices<IRandomProvider>());
		AssertDistinct(serviceProvider.GetServices<IContinuousDistribution>());
		AssertDistinct(serviceProvider.GetServices<IDiscreteDistribution>());
	}

	[TestMethod]
	public async Task AddEssentials_FileSystem_Persistence_Writes_Json_And_Keeps_Lists()
	{
		string directory = Path.Join(Path.GetTempPath(), $"{nameof(DefaultProviderTests)}_{Guid.NewGuid():N}");
		try
		{
			ServiceCollection services = new();
			services.AddEssentials();
			services.AddFileSystemPersistenceProvider<string>(directory);
			using ServiceProvider serviceProvider = services.BuildServiceProvider();
			IPersistenceProvider<string> persistence = serviceProvider.GetRequiredService<IPersistenceProvider<string>>();

			await persistence.StoreAsync("settings", new Settings { Value = 7, Items = ["a", "b"] }, TestContext.CancellationToken).ConfigureAwait(false);
			Settings? loaded = await persistence.RetrieveAsync<Settings>("settings", TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsTrue(File.Exists(Path.Join(directory, "settings.json")), "The default serializer should write a .json file");
			Assert.IsNotNull(loaded);
			Assert.AreEqual(7, loaded.Value);
			Assert.HasCount(2, loaded.Items);
			Assert.AreEqual("a", loaded.Items[0]);
			Assert.AreEqual("b", loaded.Items[1]);
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
	}

	private static void AssertDistinct<T>(IEnumerable<T> providers)
	{
		string[] names = [.. providers.Select(p => p!.GetType().FullName!)];
		Assert.HasCount(names.Distinct().Count(), names, $"Each {typeof(T).Name} implementation should be registered once");
	}

	public sealed class Settings
	{
		public int Value { get; set; }
		public IReadOnlyList<string> Items { get; init; } = [];
	}
}
