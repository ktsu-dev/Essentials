// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.All;

using ktsu.Essentials.CacheProviders.InMemory;
using ktsu.Essentials.CommandExecutors.Native;
using ktsu.Essentials.CompressionProviders.Brotli;
using ktsu.Essentials.CompressionProviders.Deflate;
using ktsu.Essentials.CompressionProviders.Gzip;
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
/// the composite obfuscator, and the filesystem, temp, data-home, and config-home persistence providers —
/// are not included here; register those explicitly from their own packages.
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
			.AddEncodingProviders()
			.AddEncryptionProviders()
			.AddFileSystemProviders()
			.AddHashProviders()
			.AddKeyedHashProviders()
			.AddLoggingProviders()
			.AddNavigationProviders()
			.AddObfuscationProviders()
			.AddPersistenceProviders()
			.AddSerializationProviders();
	}

	/// <summary>
	/// Registers every bundled compression provider.
	/// </summary>
	/// <remarks>ZLib is unavailable on netstandard2.1 and is skipped on that target.</remarks>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCompressionProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services
			.AddGzipCompressionProvider()
			.AddBrotliCompressionProvider()
			.AddDeflateCompressionProvider();

#if !NETSTANDARD2_1
		services.AddZLibCompressionProvider();
#endif

		return services;
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
			.AddBase64EncodingProvider()
			.AddHexEncodingProvider();
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
			.AddSHA256HashProvider()
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
			.AddXxHash128HashProvider();
	}

	/// <summary>
	/// Registers every keyed hashing provider.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddKeyedHashProviders(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		return services
			.AddHmacSha256KeyedHashProvider()
			.AddHmacSha384KeyedHashProvider()
			.AddHmacSha512KeyedHashProvider();
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
			.AddXorObfuscationProvider()
			.AddCaesarObfuscationProvider()
			.AddReverseObfuscationProvider()
			.AddBitRotateObfuscationProvider()
			.AddBase64ObfuscationProvider()
			.AddHexObfuscationProvider();
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
			.AddJsonSerializationProvider()
			.AddNewtonsoftJsonSerializationProvider()
			.AddYamlSerializationProvider()
			.AddTomlSerializationProvider();
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
