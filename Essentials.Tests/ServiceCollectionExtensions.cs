// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using ktsu.Essentials;
using ktsu.Essentials.CacheProviders.InMemory;
using ktsu.Essentials.CommandExecutors.Native;
using ktsu.Essentials.CompressionProviders.Brotli;
using ktsu.Essentials.CompressionProviders.Deflate;
using ktsu.Essentials.CompressionProviders.Gzip;
using ktsu.Essentials.CompressionProviders.ZLib;
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
using ktsu.Essentials.LoggingProviders.Console;
using ktsu.Essentials.NavigationProviders.InMemory;
using ktsu.Essentials.ObfuscationProviders.Base64;
using ktsu.Essentials.ObfuscationProviders.BitRotate;
using ktsu.Essentials.ObfuscationProviders.Caesar;
using ktsu.Essentials.ObfuscationProviders.Composite;
using ktsu.Essentials.ObfuscationProviders.Hex;
using ktsu.Essentials.ObfuscationProviders.Reverse;
using ktsu.Essentials.ObfuscationProviders.Xor;
using ktsu.Essentials.PersistenceProviders.InMemory;
using ktsu.Essentials.SerializationProviders.Json;
using ktsu.Essentials.SerializationProviders.NewtonsoftJson;
using ktsu.Essentials.SerializationProviders.Toml;
using ktsu.Essentials.SerializationProviders.Yaml;
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
		services.AddObfuscationProviders();
		services.AddPersistenceProviders();
		services.AddSerializationProviders();
		return services;
	}

	public static ServiceCollection AddCompressionProviders(this ServiceCollection services)
	{
		services.AddSingleton<ICompressionProvider, GzipCompressionProvider>();
		services.AddSingleton<ICompressionProvider, BrotliCompressionProvider>();
		services.AddSingleton<ICompressionProvider, DeflateCompressionProvider>();
		services.AddSingleton<ICompressionProvider, ZLibCompressionProvider>();
		return services;
	}

	public static ServiceCollection AddEncryptionProviders(this ServiceCollection services)
	{
		services.AddSingleton<IEncryptionProvider, AesEncryptionProvider>();
		return services;
	}

	public static ServiceCollection AddObfuscationProviders(this ServiceCollection services)
	{
		services.AddSingleton<IObfuscationProvider, XorObfuscationProvider>();
		services.AddSingleton<IObfuscationProvider, CaesarObfuscationProvider>();
		services.AddSingleton<IObfuscationProvider, ReverseObfuscationProvider>();
		services.AddSingleton<IObfuscationProvider, BitRotateObfuscationProvider>();
		services.AddSingleton<IObfuscationProvider>(_ => new Base64ObfuscationProvider());
		services.AddSingleton<IObfuscationProvider>(_ => new HexObfuscationProvider());
		services.AddSingleton<IObfuscationProvider>(_ => new CompositeObfuscationProvider([new XorObfuscationProvider(), new BitRotateObfuscationProvider()]));
		return services;
	}

	public static ServiceCollection AddFileSystemProviders(this ServiceCollection services)
	{
		services.AddSingleton<IFileSystemProvider, NativeFileSystemProvider>();
		return services;
	}

	public static ServiceCollection AddHashProviders(this ServiceCollection services)
	{
		services.AddSingleton<IHashProvider, SHA1HashProvider>();
		services.AddSingleton<IHashProvider, SHA256HashProvider>();
		services.AddSingleton<IHashProvider, SHA384HashProvider>();
		services.AddSingleton<IHashProvider, SHA512HashProvider>();
		services.AddSingleton<IHashProvider, MD5HashProvider>();
		services.AddSingleton<IHashProvider, FNV1_32HashProvider>();
		services.AddSingleton<IHashProvider, FNV1_64HashProvider>();
		services.AddSingleton<IHashProvider, FNV1a_32HashProvider>();
		services.AddSingleton<IHashProvider, FNV1a_64HashProvider>();
		services.AddSingleton<IHashProvider, CRC32HashProvider>();
		services.AddSingleton<IHashProvider, CRC64HashProvider>();
		services.AddSingleton<IHashProvider, XxHash32HashProvider>();
		services.AddSingleton<IHashProvider, XxHash64HashProvider>();
		services.AddSingleton<IHashProvider, XxHash3HashProvider>();
		services.AddSingleton<IHashProvider, XxHash128HashProvider>();
		return services;
	}

	public static ServiceCollection AddSerializationProviders(this ServiceCollection services)
	{
		services.AddSingleton<ISerializationProvider, JsonSerializationProvider>();
		services.AddSingleton<ISerializationProvider, NewtonsoftJsonSerializationProvider>();
		services.AddSingleton<ISerializationProvider, YamlSerializationProvider>();
		services.AddSingleton<ISerializationProvider, TomlSerializationProvider>();
		return services;
	}

	public static ServiceCollection AddEncodingProviders(this ServiceCollection services)
	{
		services.AddSingleton<IEncodingProvider, Base64EncodingProvider>();
		services.AddSingleton<IEncodingProvider, HexEncodingProvider>();
		return services;
	}

	public static ServiceCollection AddCommandExecutors(this ServiceCollection services)
	{
		services.AddSingleton<ICommandExecutor, NativeCommandExecutor>();
		return services;
	}

	public static ServiceCollection AddLoggingProviders(this ServiceCollection services)
	{
		services.AddSingleton<ILoggingProvider, ConsoleLoggingProvider>();
		return services;
	}

	public static ServiceCollection AddCacheProviders(this ServiceCollection services)
	{
		services.AddSingleton(typeof(ICacheProvider<,>), typeof(InMemoryCacheProvider<,>));
		return services;
	}

	public static ServiceCollection AddNavigationProviders(this ServiceCollection services)
	{
		services.AddTransient(typeof(INavigationProvider<>), typeof(InMemoryNavigationProvider<>));
		return services;
	}

	public static ServiceCollection AddPersistenceProviders(this ServiceCollection services)
	{
		services.AddSingleton(typeof(IPersistenceProvider<>), typeof(InMemoryPersistenceProvider<>));
		return services;
	}
}
