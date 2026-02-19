// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Common.Tests;

using ktsu.Abstractions;
using ktsu.CompressionProviders;
using ktsu.SerializationProviders;
using ktsu.EncryptionProviders;
using ktsu.HashProviders;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static ServiceCollection AddCommon(this ServiceCollection services)
	{
		services.AddCacheProviders();
		services.AddCommandExecutors();
		services.AddCompressionProviders();
		services.AddEncodingProviders();
		services.AddEncryptionProviders();
		services.AddFileSystemProviders();
		services.AddHashProviders();
		services.AddLoggingProviders();
		services.AddNavigationProviders();
		services.AddPersistenceProviders();
		services.AddSerializationProviders();
		return services;
	}

	public static ServiceCollection AddCompressionProviders(this ServiceCollection services)
	{
		services.AddSingleton<ICompressionProvider, Gzip>();
		services.AddSingleton<ICompressionProvider, Brotli>();
		services.AddSingleton<ICompressionProvider, Deflate>();
		services.AddSingleton<ICompressionProvider, ZLib>();
		return services;
	}

	public static ServiceCollection AddEncryptionProviders(this ServiceCollection services)
	{
		services.AddSingleton<IEncryptionProvider, Aes>();
		return services;
	}

	public static ServiceCollection AddFileSystemProviders(this ServiceCollection services)
	{
		services.AddSingleton<IFileSystemProvider, FileSystemProviders.Native>();
		return services;
	}

	public static ServiceCollection AddHashProviders(this ServiceCollection services)
	{
		services.AddSingleton<IHashProvider, SHA1>();
		services.AddSingleton<IHashProvider, SHA256>();
		services.AddSingleton<IHashProvider, SHA384>();
		services.AddSingleton<IHashProvider, SHA512>();
		services.AddSingleton<IHashProvider, MD5>();
		services.AddSingleton<IHashProvider, FNV1_32>();
		services.AddSingleton<IHashProvider, FNV1_64>();
		services.AddSingleton<IHashProvider, FNV1a_32>();
		services.AddSingleton<IHashProvider, FNV1a_64>();
		services.AddSingleton<IHashProvider, CRC32>();
		services.AddSingleton<IHashProvider, CRC64>();
		services.AddSingleton<IHashProvider, XxHash32>();
		services.AddSingleton<IHashProvider, XxHash64>();
		services.AddSingleton<IHashProvider, XxHash3>();
		services.AddSingleton<IHashProvider, XxHash128>();
		return services;
	}

	public static ServiceCollection AddSerializationProviders(this ServiceCollection services)
	{
		services.AddSingleton<ISerializationProvider, Json>();
		services.AddSingleton<ISerializationProvider, Yaml>();
		services.AddSingleton<ISerializationProvider, Toml>();
		return services;
	}

	public static ServiceCollection AddEncodingProviders(this ServiceCollection services)
	{
		services.AddSingleton<IEncodingProvider, EncodingProviders.Base64>();
		services.AddSingleton<IEncodingProvider, EncodingProviders.Hex>();
		return services;
	}

	public static ServiceCollection AddCommandExecutors(this ServiceCollection services)
	{
		services.AddSingleton<ICommandExecutor, CommandExecutors.Native>();
		return services;
	}

	public static ServiceCollection AddLoggingProviders(this ServiceCollection services)
	{
		services.AddSingleton<ILoggingProvider, LoggingProviders.Console>();
		return services;
	}

	public static ServiceCollection AddCacheProviders(this ServiceCollection services)
	{
		services.AddSingleton(typeof(ICacheProvider<,>), typeof(CacheProviders.InMemory<,>));
		return services;
	}

	public static ServiceCollection AddNavigationProviders(this ServiceCollection services)
	{
		services.AddTransient(typeof(INavigationProvider<>), typeof(NavigationProviders.InMemory<>));
		return services;
	}

	public static ServiceCollection AddPersistenceProviders(this ServiceCollection services)
	{
		services.AddSingleton(typeof(IPersistenceProvider<>), typeof(PersistenceProviders.InMemory<>));
		return services;
	}
}
