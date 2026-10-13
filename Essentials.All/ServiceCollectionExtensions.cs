// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.All;

using ktsu.Essentials.CacheProviders.InMemory;
using ktsu.Essentials.CommandExecutors.Native;
using ktsu.Essentials.CompressionProviders.Brotli;
using ktsu.Essentials.CompressionProviders.Deflate;
using ktsu.Essentials.CompressionProviders.Gzip;
using ktsu.Essentials.DistributionProviders.Bernoulli;
using ktsu.Essentials.DistributionProviders.Exponential;
using ktsu.Essentials.DistributionProviders.Geometric;
using ktsu.Essentials.DistributionProviders.LogNormal;
using ktsu.Essentials.DistributionProviders.Normal;
using ktsu.Essentials.DistributionProviders.Poisson;
using ktsu.Essentials.DistributionProviders.Uniform;
using ktsu.Essentials.EncodingProviders.Base64;
using ktsu.Essentials.EncodingProviders.Hex;
using ktsu.Essentials.EncryptionProviders.Aes;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.Essentials.HashProviders.CRC32;
using ktsu.Essentials.HashProviders.CRC64;
using ktsu.Essentials.HashProviders.FNV1_32;
using ktsu.Essentials.HashProviders.FNV1_64;
using ktsu.Essentials.HashProviders.FNV1a_32;
using ktsu.Essentials.HashProviders.FNV1a_64;
using ktsu.Essentials.HashProviders.MD5;
using ktsu.Essentials.HashProviders.SHA1;
using ktsu.Essentials.HashProviders.SHA256;
using ktsu.Essentials.HashProviders.SHA384;
using ktsu.Essentials.HashProviders.SHA512;
using ktsu.Essentials.HashProviders.XxHash128;
using ktsu.Essentials.HashProviders.XxHash3;
using ktsu.Essentials.HashProviders.XxHash32;
using ktsu.Essentials.HashProviders.XxHash64;
using ktsu.Essentials.KeyedHashProviders.HmacSha256;
using ktsu.Essentials.KeyedHashProviders.HmacSha384;
using ktsu.Essentials.KeyedHashProviders.HmacSha512;
using ktsu.Essentials.LoggingProviders.Console;
using ktsu.Essentials.NavigationProviders.InMemory;
using ktsu.Essentials.ObfuscationProviders.Base64;
using ktsu.Essentials.ObfuscationProviders.BitRotate;
using ktsu.Essentials.ObfuscationProviders.Caesar;
using ktsu.Essentials.ObfuscationProviders.Hex;
using ktsu.Essentials.ObfuscationProviders.Reverse;
using ktsu.Essentials.ObfuscationProviders.Xor;
using ktsu.Essentials.PersistenceProviders.InMemory;
using ktsu.Essentials.RandomProviders.Crypto;
using ktsu.Essentials.RandomProviders.Native;
using ktsu.Essentials.RandomProviders.Pcg;
using ktsu.Essentials.RandomProviders.Xoshiro;
using ktsu.Essentials.SerializationProviders.Json;
using ktsu.Essentials.SerializationProviders.NewtonsoftJson;
using ktsu.Essentials.SerializationProviders.Toml;
using ktsu.Essentials.SerializationProviders.Yaml;
using Microsoft.Extensions.DependencyInjection;

#if !NETSTANDARD2_1
using ktsu.Essentials.CompressionProviders.ZLib;
#endif

/// <summary>
/// Registers every bundled ktsu.Essentials provider in one call.
/// </summary>
/// <remarks>
/// Each method delegates to the registration extension shipped by the individual provider package, so
/// the behaviour is identical to registering them one at a time. Providers that need configuration —
/// the composite obfuscator, the filesystem, temp, data-home, and config-home persistence providers, the
/// file logger, and the triangular, binomial, and categorical distributions — are not included here;
/// register those explicitly from their own packages. The in-memory file system is left out too, because
/// it is for tests and must replace the native one rather than sit beside it.
/// <para>
/// Every category that bundles more than one implementation registers its default last, because
/// resolving a single service returns the last registration for it. Resolving by interface therefore
/// gives a deliberate choice rather than whichever package happens to be listed last: JSON
/// (System.Text.Json) for serialization, SHA-256 for hashing, HMAC-SHA256 for keyed hashing, Gzip for
/// compression, Base64 for encoding, XOR for obfuscation, the cryptographic generator for randomness,
/// and the normal and Poisson distributions. Every implementation is still listed once by
/// <c>GetServices&lt;T&gt;()</c>. The file-backed persistence providers resolve their serializer by
/// interface, so this is also what fixes their file format.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers every bundled provider across all categories.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddEssentials(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddCacheProviders()
			.AddCommandExecutors()
			.AddCompressionProviders()
			.AddDistributionProviders()
			.AddEncodingProviders()
			.AddEncryptionProviders()
			.AddFileSystemProviders()
			.AddHashProviders()
			.AddKeyedHashProviders()
			.AddLoggingProviders()
			.AddNavigationProviders()
			.AddObfuscationProviders()
			.AddPersistenceProviders()
			.AddRandomProviders()
			.AddSerializationProviders();
	}

	/// <summary>
	/// Registers every bundled compression provider.
	/// </summary>
	/// <remarks>
	/// ZLib is unavailable on netstandard2.1 and is skipped on that target. Gzip is the default and is
	/// registered last.
	/// </remarks>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCompressionProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services
			.AddBrotliCompressionProvider()
			.AddDeflateCompressionProvider();

#if !NETSTANDARD2_1
		services.AddZLibCompressionProvider();
#endif

		// Registered last so that it is the provider an ICompressionProvider resolves to.
		return services.AddGzipCompressionProvider();
	}

	/// <summary>
	/// Registers every bundled distribution provider that has a usable default parameterisation.
	/// </summary>
	/// <remarks>
	/// The triangular, binomial, and categorical distributions are excluded because none of them has a
	/// standard form to default to — a three-point estimate, a trial count and a weight vector all belong
	/// to the situation being modelled. Register those from their own packages with the parameters you
	/// want. What is registered here is each distribution in its standard form, which is what makes the
	/// resolvable set useful: resolving <see cref="IContinuousDistribution"/> or
	/// <see cref="IDiscreteDistribution"/> yields every shape at once.
	/// </remarks>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddDistributionProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddUniformDistributionProvider()
			.AddLogNormalDistributionProvider()
			.AddExponentialDistributionProvider()
			.AddBernoulliDistributionProvider()
			.AddGeometricDistributionProvider()
			// The defaults go last, so that a single IContinuousDistribution or IDiscreteDistribution
			// resolves to the normal or Poisson distribution respectively.
			.AddNormalDistributionProvider()
			.AddPoissonDistributionProvider();
	}

	/// <summary>
	/// Registers every bundled random provider.
	/// </summary>
	/// <remarks>
	/// The cryptographic provider is a singleton and the other three are transients, because only the
	/// first is stateless — see each package's own registration for why. A container-resolved seedable
	/// provider is seeded from system entropy; construct one yourself when the point is to reproduce a
	/// sequence.
	/// </remarks>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddRandomProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddNativeRandomProvider()
			.AddXoshiroRandomProvider()
			.AddPcgRandomProvider()
			// Registered last so that it is the provider an IRandomProvider resolves to.
			.AddCryptoRandomProvider();
	}

	/// <summary>
	/// Registers every bundled encoding provider.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddEncodingProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddHexEncodingProvider()
			// Registered last so that it is the provider an IEncodingProvider resolves to.
			.AddBase64EncodingProvider();
	}

	/// <summary>
	/// Registers every bundled encryption provider.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddEncryptionProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services.AddAesEncryptionProvider();
	}

	/// <summary>
	/// Registers every bundled hash provider.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddHashProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddSHA1HashProvider()
			.AddSHA384HashProvider()
			.AddSHA512HashProvider()
			.AddMD5HashProvider()
			.AddFNV1_32HashProvider()
			.AddFNV1_64HashProvider()
			.AddFNV1a_32HashProvider()
			.AddFNV1a_64HashProvider()
			.AddCRC32HashProvider()
			.AddCRC64HashProvider()
			.AddXxHash32HashProvider()
			.AddXxHash64HashProvider()
			.AddXxHash3HashProvider()
			.AddXxHash128HashProvider()
			// Registered last so that it is the provider an IHashProvider resolves to.
			.AddSHA256HashProvider();
	}

	/// <summary>
	/// Registers every bundled keyed hash provider.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddKeyedHashProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddHmacSha384KeyedHashProvider()
			.AddHmacSha512KeyedHashProvider()
			// Registered last so that it is the provider an IKeyedHashProvider resolves to.
			.AddHmacSha256KeyedHashProvider();
	}

	/// <summary>
	/// Registers every bundled obfuscation provider that has a usable default configuration.
	/// </summary>
	/// <remarks>
	/// The composite obfuscator is excluded because it has no meaningful default — register it from its
	/// own package with the stages you want.
	/// </remarks>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddObfuscationProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddCaesarObfuscationProvider()
			.AddReverseObfuscationProvider()
			.AddBitRotateObfuscationProvider()
			.AddBase64ObfuscationProvider()
			.AddHexObfuscationProvider()
			// Registered last so that it is the provider an IObfuscationProvider resolves to.
			.AddXorObfuscationProvider();
	}

	/// <summary>
	/// Registers every bundled serialization provider.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddSerializationProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddNewtonsoftJsonSerializationProvider()
			.AddYamlSerializationProvider()
			.AddTomlSerializationProvider()
			// Registered last so that it is the provider an ISerializationProvider resolves to, which
			// also makes it the format the file-backed persistence providers write.
			.AddJsonSerializationProvider();
	}

	/// <summary>
	/// Registers the bundled filesystem provider.
	/// </summary>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddFileSystemProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services.AddNativeFileSystemProvider();
	}

	/// <summary>
	/// Registers the bundled command executor.
	/// </summary>
	/// <param name="services">The service collection to add the executor to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCommandExecutors(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services.AddNativeCommandExecutor();
	}

	/// <summary>
	/// Registers the bundled logging provider.
	/// </summary>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddLoggingProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services.AddConsoleLoggingProvider();
	}

	/// <summary>
	/// Registers the bundled cache provider.
	/// </summary>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCacheProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services.AddInMemoryCacheProvider();
	}

	/// <summary>
	/// Registers the bundled navigation provider.
	/// </summary>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddNavigationProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services.AddInMemoryNavigationProvider();
	}

	/// <summary>
	/// Registers the bundled in-memory persistence provider.
	/// </summary>
	/// <remarks>
	/// The filesystem, temp, data-home, and config-home providers each need configuration, so they are
	/// registered from their own packages rather than here.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddPersistenceProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services.AddInMemoryPersistenceProvider();
	}
}
