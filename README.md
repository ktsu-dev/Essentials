# ktsu.Essentials

> A comprehensive .NET library providing high-performance interfaces and implementations for common cross-cutting concerns including compression, encoding, encryption, hashing, serialization, caching, persistence, validation, logging, navigation, command execution, and filesystem access.

[![License](https://img.shields.io/github/license/ktsu-dev/Essentials.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.Essentials?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.Essentials?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.Essentials?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/Essentials?label=Commits&logo=github)](https://github.com/ktsu-dev/Essentials/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/Essentials?label=Contributors&logo=github)](https://github.com/ktsu-dev/Essentials/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/Essentials/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/Essentials/actions)

## Introduction

`ktsu.Essentials` defines a consistent, high-performance API for common cross-cutting concerns in .NET applications. Each provider interface follows a three-tier pattern: zero-allocation `Try*` methods using `Span<byte>` and `Stream`, convenient self-allocating methods, and async variants with `CancellationToken` support. Implementers only need to provide the core `Try*` methods — all convenience and async methods are provided via default interface implementations. The package also includes ready-to-use provider implementations for compression, hashing, encoding, encryption, serialization, caching, persistence, logging, navigation, and command execution.

## Features

- **Compression**: `ICompressionProvider` with Gzip, Brotli, Deflate, and ZLib implementations
- **Encoding**: `IEncodingProvider` with Base64 and Hex implementations for format/transport encoding
- **Encryption**: `IEncryptionProvider` with AES implementation including key and IV generation
- **Hashing**: `IHashProvider` with 15 implementations (MD5, SHA1/256/384/512, FNV1/FNV1a 32/64-bit, CRC32/64, XxHash32/64/3/128)
- **Serialization**: `ISerializationProvider` with JSON, YAML, and TOML implementations plus configurable `ISerializationOptions`
- **Caching**: `ICacheProvider<TKey, TValue>` with in-memory implementation supporting expiration and get-or-add semantics
- **Persistence**: `IPersistenceProvider<TKey>` with AppData, FileSystem, InMemory, and Temp implementations
- **Validation**: `IValidationProvider<T>` with structured results, error codes, and throw-on-failure support
- **Logging**: `ILoggingProvider` with console implementation supporting six severity levels
- **Navigation**: `INavigationProvider<T>` with in-memory implementation for browser-like back/forward navigation
- **Command Execution**: `ICommandExecutor` with native implementation for running shell commands and capturing output
- **Filesystem**: `IFileSystemProvider` extending Testably.Abstractions for testable filesystem access
- **Zero-Allocation Core**: All byte-oriented providers support `Span<byte>` and `Stream` for allocation-free operations
- **Minimal Implementation Burden**: Default interface implementations reduce boilerplate — implement only the core `Try*` methods
- **Comprehensive Async Support**: Every operation has async variants with proper `CancellationToken` support

## Installation

### Package Manager Console

```powershell
Install-Package ktsu.Essentials
```

### .NET CLI

```bash
dotnet add package ktsu.Essentials
```

### Package Reference

```xml
<PackageReference Include="ktsu.Essentials" Version="x.y.z" />
```

## Usage Examples

### Basic Example

```csharp
using ktsu.Essentials;
using ktsu.Essentials.HashProviders;
using ktsu.Essentials.CompressionProviders;
using ktsu.Essentials.EncodingProviders;
using Microsoft.Extensions.DependencyInjection;

// Register provider implementations via DI
IServiceCollection services = new ServiceCollection();
services.AddSingleton<IHashProvider, SHA256>();
services.AddSingleton<ICompressionProvider, Gzip>();
services.AddSingleton<IEncodingProvider, EncodingProviders.Base64>();

using IServiceProvider provider = services.BuildServiceProvider();
IHashProvider hashProvider = provider.GetRequiredService<IHashProvider>();

// Convenience method (auto-allocates buffer)
byte[] hash = hashProvider.Hash("Hello, World!");

// Zero-allocation method
Span<byte> buffer = stackalloc byte[hashProvider.HashLengthBytes];
if (hashProvider.TryHash("Hello, World!"u8, buffer))
{
    string hex = Convert.ToHexString(buffer);
}

// Async method
byte[] asyncHash = await hashProvider.HashAsync("Hello, World!");
```

### Compression

```csharp
ICompressionProvider compressor = provider.GetRequiredService<ICompressionProvider>();

byte[] compressed = compressor.Compress(originalData);
byte[] decompressed = compressor.Decompress(compressed);

// String convenience
string compressedText = compressor.Compress("Large text content...");
```

### Serialization

```csharp
ISerializationProvider serializer = provider.GetRequiredService<ISerializationProvider>();

string json = serializer.Serialize(myObject);
MyClass? deserialized = serializer.Deserialize<MyClass>(json);

// Async
string asyncJson = await serializer.SerializeAsync(myObject);
```

### Caching

```csharp
ICacheProvider<string, MyData> cache = provider.GetRequiredService<ICacheProvider<string, MyData>>();

cache.Set("key", myData, expiration: TimeSpan.FromMinutes(5));
MyData value = cache.GetOrAdd("key", k => LoadData(k));
```

### Persistence

```csharp
IPersistenceProvider<string> persistence = provider.GetRequiredService<IPersistenceProvider<string>>();

await persistence.StoreAsync("settings", mySettings);
MySettings? loaded = await persistence.RetrieveAsync<MySettings>("settings");
MySettings guaranteed = await persistence.RetrieveOrCreateAsync<MySettings>("settings");
```

### Implementing a Custom Provider

Implementers only need to provide the core `Try*` methods — all other methods are inherited:

```csharp
using ktsu.Essentials;

public sealed class MyHashProvider : IHashProvider
{
    public int HashLengthBytes => 32;

    public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        if (destination.Length < HashLengthBytes) return false;
        // Custom hash logic here
        return true;
    }

    public bool TryHash(Stream data, Span<byte> destination)
    {
        if (destination.Length < HashLengthBytes) return false;
        // Custom stream hash logic here
        return true;
    }

    // Hash(), HashAsync(), TryHashAsync(), string overloads — all inherited
}
```

## API Reference

### `ICompressionProvider`

Compress and decompress data with Span, Stream, and string support.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TryCompress(ReadOnlySpan<byte>, Span<byte>)` | `bool` | Zero-allocation compression |
| `TryCompress(Stream, Stream)` | `bool` | Stream-based compression |
| `Compress(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating compression |
| `Compress(string)` | `string` | UTF8 string compression |
| `TryDecompress(ReadOnlySpan<byte>, Span<byte>)` | `bool` | Zero-allocation decompression |
| `Decompress(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating decompression |

### `IEncodingProvider`

Format/transport encoding (Base64, Hex) — not text character encodings.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TryEncode(ReadOnlySpan<byte>, Span<byte>)` | `bool` | Zero-allocation encoding |
| `TryEncode(Stream, Stream)` | `bool` | Stream-based encoding |
| `Encode(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating encoding |
| `TryDecode(ReadOnlySpan<byte>, Span<byte>)` | `bool` | Zero-allocation decoding |
| `Decode(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating decoding |

### `IEncryptionProvider`

Encrypt and decrypt data with key and IV management.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TryEncrypt(ReadOnlySpan<byte>, Span<byte>, byte[], byte[])` | `bool` | Zero-allocation encryption |
| `TryDecrypt(ReadOnlySpan<byte>, Span<byte>, byte[], byte[])` | `bool` | Zero-allocation decryption |
| `GenerateKey()` | `byte[]` | Generates a new encryption key |
| `GenerateIV()` | `byte[]` | Generates a new initialization vector |

### `IHashProvider`

Hash data with configurable output length. Exposes `HashLengthBytes` property for the output size in bytes.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TryHash(ReadOnlySpan<byte>, Span<byte>)` | `bool` | Zero-allocation hashing |
| `TryHash(Stream, Span<byte>)` | `bool` | Stream-based hashing |
| `Hash(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating hashing |
| `Hash(string)` | `byte[]` | Hash a UTF8 string |

### `ISerializationProvider`

Serialize and deserialize objects supporting JSON, YAML, TOML, and other text-based formats.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TrySerialize(object, TextWriter)` | `bool` | Serialize to a TextWriter |
| `Serialize(object)` | `string` | Serialize to a string |
| `Deserialize<T>(ReadOnlySpan<byte>)` | `T?` | Deserialize from bytes |
| `Deserialize<T>(string)` | `T?` | Deserialize from a string |
| `Deserialize<T>(TextReader)` | `T?` | Deserialize from a TextReader |

### `ICacheProvider<TKey, TValue>`

Cache key-value pairs with optional expiration.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TryGet(TKey, out TValue?)` | `bool` | Try to get a cached value |
| `Get(TKey)` | `TValue` | Get a value or throw |
| `Set(TKey, TValue, TimeSpan?)` | `void` | Set a value with optional expiration |
| `GetOrAdd(TKey, Func<TKey, TValue>, TimeSpan?)` | `TValue` | Get or create a value |
| `Remove(TKey)` | `bool` | Remove a cached value |
| `Clear()` | `void` | Clear all entries |

### `IPersistenceProvider<TKey>`

Store and retrieve objects with pluggable storage backends. Exposes `ProviderName` and `IsPersistent` properties.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `StoreAsync<T>(TKey, T)` | `Task` | Store an object |
| `RetrieveAsync<T>(TKey)` | `Task<T?>` | Retrieve an object |
| `RetrieveOrCreateAsync<T>(TKey)` | `Task<T>` | Retrieve or create a new instance |
| `ExistsAsync(TKey)` | `Task<bool>` | Check if a key exists |
| `RemoveAsync(TKey)` | `Task<bool>` | Remove an object |
| `GetAllKeysAsync()` | `Task<IEnumerable<TKey>>` | List all stored keys |
| `ClearAsync()` | `Task` | Clear all stored objects |

### `IValidationProvider<T>`

Validate objects and return structured results.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `Validate(T)` | `ValidationResult` | Validate and return result |
| `IsValid(T)` | `bool` | Check validity |
| `ValidateAndThrow(T)` | `void` | Validate or throw `ValidationException` |

### `ILoggingProvider`

Write structured log messages at various severity levels.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `Log(LogLevel, string)` | `void` | Write a log entry |
| `Log(LogLevel, Exception, string)` | `void` | Write a log entry with an exception |
| `IsEnabled(LogLevel)` | `bool` | Check if a log level is enabled |
| `LogTrace(string)` through `LogCritical(string)` | `void` | Level-specific convenience methods |

### `INavigationProvider<T>`

Browser-like back/forward navigation. Exposes `Current`, `CanGoBack`, and `CanGoForward` properties.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `NavigateTo(T)` | `void` | Navigate to a destination |
| `GoBack()` | `T?` | Navigate backward |
| `GoForward()` | `T?` | Navigate forward |
| `Clear()` | `void` | Clear all history |

### `ICommandExecutor`

Run shell commands and capture output.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `ExecuteAsync(string, string?)` | `Task<CommandResult>` | Execute a command |
| `Execute(string, string?)` | `CommandResult` | Execute a command synchronously |
| `ExecuteAndGetOutputAsync(string, string?)` | `Task<string>` | Execute and return stdout or throw |

### `IFileSystemProvider`

Extends `Testably.Abstractions.IFileSystem` for testable filesystem operations.

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.
