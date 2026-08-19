# ktsu.Essentials

> A comprehensive .NET library providing high-performance interfaces and implementations for common cross-cutting concerns including compression, encoding, obfuscation, encryption, hashing, serialization, caching, persistence, validation, logging, navigation, command execution, and filesystem access.

[![License](https://img.shields.io/github/license/ktsu-dev/Essentials.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.Essentials?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.Essentials?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.Essentials?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/Essentials?label=Commits&logo=github)](https://github.com/ktsu-dev/Essentials/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/Essentials?label=Contributors&logo=github)](https://github.com/ktsu-dev/Essentials/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/Essentials/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/Essentials/actions)

## Introduction

`ktsu.Essentials` defines a consistent, high-performance API for common cross-cutting concerns in .NET applications. Each provider interface follows a three-tier pattern: core `Try*` methods over `Span<byte>` and `Stream` that report how many bytes they wrote, convenient self-allocating methods, and async variants with `CancellationToken` support. Implementers only need to provide the core `Try*` methods — all convenience and async methods are provided via default interface implementations. The `ktsu.Essentials` package is interfaces only; implementations ship as separate `ktsu.Essentials.<Category>.<Impl>` packages, with `ktsu.Essentials.All` bundling every one of them. Higher-level concerns are expressed by composition rather than duplication — configuration is simply an `IPersistenceProvider<TKey>` over a serializer, and obfuscation composes encoding transforms.

## Features

- **Compression**: `ICompressionProvider` with Gzip, Brotli, Deflate, and ZLib implementations
- **Encoding**: `IEncodingProvider` with Base64 and Hex implementations for format/transport encoding
- **Obfuscation**: `IObfuscationProvider` with XOR, Caesar, bit-rotation, byte-reversal, Base64, and Hex implementations, plus a `Composite` provider that pipelines several together. Obfuscation is reversible but is **not** encryption — it provides no confidentiality
- **Dependency Injection**: every provider package ships an `Add<Impl><Category>Provider()` extension; `ktsu.Essentials.All` adds per-category helpers and a single `AddEssentials()`. Registrations are idempotent and expose each provider by both concrete type and interface
- **Encryption**: `IEncryptionProvider` with AES implementation including key and IV generation
- **Hashing**: `IHashProvider` with 15 implementations (MD5, SHA1/256/384/512, FNV1/FNV1a 32/64-bit, CRC32/64, XxHash32/64/3/128)
- **Serialization**: `ISerializationProvider` with System.Text.Json, Newtonsoft.Json, YAML, and TOML implementations plus configurable `ISerializationOptions`
- **Caching**: `ICacheProvider<TKey, TValue>` with in-memory implementation supporting expiration and get-or-add semantics
- **Persistence**: `IPersistenceProvider<TKey>` with DataHome, ConfigHome, FileSystem, InMemory, and Temp implementations. `DataHome` and `ConfigHome` follow the XDG Base Directory layout on every platform — `$XDG_DATA_HOME` or `~/.local/share/<app>` for application state, `$XDG_CONFIG_HOME` or `~/.config/<app>` for user settings — with `~` resolving to `%USERPROFILE%` on Windows
- **Validation**: `IValidationProvider<T>` with structured results, error codes, and throw-on-failure support
- **Logging**: `ILoggingProvider` with console implementation supporting six severity levels
- **Navigation**: `INavigationProvider<T>` with in-memory implementation for browser-like back/forward navigation
- **Command Execution**: `ICommandExecutor` with native implementation for running shell commands and capturing output
- **Filesystem**: `IFileSystemProvider` extending Testably.Abstractions for testable filesystem access
- **Explicit Buffer Contract**: every span operation is `bool TryX(source, destination, out int bytesWritten)` and each category exposes a `GetMax…Length` bound, so callers can size a buffer up front and know exactly how much was written. Encoding, hashing and obfuscation run allocation-free on the span path; compression and encryption still buffer internally, because the underlying BCL APIs for those are stream-only
- **Minimal Implementation Burden**: Default interface implementations reduce boilerplate — implement only the core `Try*` methods
- **Async Support**: Operations expose async variants with `CancellationToken` support. Stream hashing is genuinely asynchronous — it reads with `ReadAsync` and holds no thread. Most other async variants are convenience wrappers that run synchronous work on the thread pool; span-destination operations have no async form, because an `out` parameter cannot cross an await boundary
- **Batteries-Included or Cherry-Pick**: Each provider ships as its own `ktsu.Essentials.<Category>.<Impl>` package; install the `ktsu.Essentials.All` meta-package to get every provider at once, or reference only the ones you need

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
using ktsu.Essentials.All;
using ktsu.Essentials.HashProviders.SHA256;
using Microsoft.Extensions.DependencyInjection;

// Each provider package ships its own registration extension
IServiceCollection services = new ServiceCollection();
services.AddSHA256HashProvider();
services.AddGzipCompressionProvider();
services.AddBase64EncodingProvider();

// ...or register everything at once with the ktsu.Essentials.All package
services.AddEssentials();

using ServiceProvider provider = services.BuildServiceProvider();

// Resolve a specific implementation by its concrete type...
SHA256HashProvider sha256 = provider.GetRequiredService<SHA256HashProvider>();

// ...or every registered implementation of an interface
IEnumerable<IHashProvider> allHashProviders = provider.GetServices<IHashProvider>();

IHashProvider hashProvider = sha256;

// Convenience method (auto-allocates buffer)
byte[] hash = hashProvider.Hash("Hello, World!");

// Buffer-based method — no allocation, and it tells you how much it wrote
Span<byte> buffer = stackalloc byte[hashProvider.HashLengthBytes];
if (hashProvider.TryHash("Hello, World!"u8, buffer, out int written))
{
    string hex = Convert.ToHexString(buffer[..written]);
}

// Async method
byte[] asyncHash = await hashProvider.HashAsync("Hello, World!");

// Async stream hashing — one pass, no thread held, nothing buffered
using FileStream file = File.OpenRead("large-object.bin");
byte[] streamHash = await hashProvider.HashAsync(file);

// Incremental — digest bytes you are already moving for another reason
using IIncrementalHash incremental = hashProvider.CreateIncremental();
await foreach (ReadOnlyMemory<byte> chunk in source)
{
    incremental.Append(chunk.Span);
    await destination.WriteAsync(chunk);
}

byte[] digest = incremental.GetHashAndReset();
```

### Compression

```csharp
ICompressionProvider compressor = provider.GetRequiredService<ICompressionProvider>();

byte[] compressed = compressor.Compress(originalData);
byte[] decompressed = compressor.Decompress(compressed);

// String convenience — compressed bytes are returned as Base64 so they survive as text
string compressedText = compressor.Compress("Large text content...");
string originalText = compressor.Decompress(compressedText);
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

The `DataHome` and `ConfigHome` providers need an application name, so register them explicitly rather than
through `AddEssentials()`:

```csharp
using ktsu.Essentials.PersistenceProviders.ConfigHome;
using ktsu.Essentials.PersistenceProviders.DataHome;

// User settings   -> $XDG_CONFIG_HOME/MyApp   or ~/.config/MyApp
services.AddConfigHomePersistenceProvider<string>("MyApp");

// Application state -> $XDG_DATA_HOME/MyApp   or ~/.local/share/MyApp
services.AddDataHomePersistenceProvider<string>("MyApp");
```

Both use the same layout on every platform, with `~` resolving to `%USERPROFILE%` on Windows. If you need
the paths without a persistence provider, `UserDirectories` exposes them directly:

```csharp
string dataDir = UserDirectories.GetApplicationDataDirectory("MyApp");
string configDir = UserDirectories.GetApplicationConfigDirectory("MyApp");
```

### Implementing a Custom Provider

Implementers only need to provide the core `Try*` methods — all other methods are inherited:

```csharp
using ktsu.Essentials;

public sealed class MyHashProvider : IHashProvider
{
    public int HashLengthBytes => 32;

    public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        if (destination.Length < HashLengthBytes) return false;
        // Custom hash logic here
        bytesWritten = HashLengthBytes;
        return true;
    }

    public bool TryHash(Stream data, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        if (destination.Length < HashLengthBytes) return false;
        // Custom stream hash logic here
        bytesWritten = HashLengthBytes;
        return true;
    }

    // Hash(), HashAsync(), string overloads — all inherited

    // Override this. The inherited default buffers the whole input in memory,
    // and TryHashAsync(Stream, ...) is built on it.
    public IIncrementalHash CreateIncremental() => new MyIncrementalHash();
}
```

## API Reference

### `ICompressionProvider`

Compress and decompress data with Span, Stream, and string support.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `GetMaxCompressedLength(int)` | `int` | Buffer size that always fits the output |
| `TryCompress(ReadOnlySpan<byte>, Span<byte>, out int)` | `bool` | Compress, reporting bytes written |
| `TryCompress(Stream, Stream)` | `bool` | Stream-based compression |
| `Compress(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating compression |
| `Compress(string)` | `string` | Compresses UTF8 text, returns Base64 |
| `TryDecompress(ReadOnlySpan<byte>, Span<byte>, out int)` | `bool` | Decompress, reporting bytes written |
| `Decompress(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating decompression |
| `Decompress(string)` | `string` | Reverses `Compress(string)` |

### `IEncodingProvider`

Format/transport encoding (Base64, Hex) — not text character encodings.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `GetMaxEncodedLength(int)` / `GetMaxDecodedLength(int)` | `int` | Buffer sizes that always fit the output |
| `TryEncode(ReadOnlySpan<byte>, Span<byte>, out int)` | `bool` | Encode, reporting bytes written |
| `TryEncode(Stream, Stream)` | `bool` | Stream-based encoding |
| `Encode(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating encoding |
| `Encode(string)` | `string` | Encodes UTF8 text |
| `TryDecode(ReadOnlySpan<byte>, Span<byte>, out int)` | `bool` | Decode, reporting bytes written |
| `Decode(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating decoding |
| `Decode(string)` | `string` | Reverses `Encode(string)` |

### `IEncryptionProvider`

Encrypt and decrypt data with key and IV management.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `GetMaxEncryptedLength(int)` | `int` | Buffer size that always fits the ciphertext |
| `TryEncrypt(ReadOnlySpan<byte>, …, Span<byte>, out int)` | `bool` | Encrypt, reporting bytes written |
| `TryDecrypt(ReadOnlySpan<byte>, …, Span<byte>, out int)` | `bool` | Decrypt, reporting bytes written |
| `Encrypt(string, ...)` | `string` | Encrypts UTF8 text, returns Base64 |
| `Decrypt(string, ...)` | `string` | Reverses `Encrypt(string, ...)` |
| `GenerateKey()` | `byte[]` | Generates a new encryption key |
| `GenerateIV()` | `byte[]` | Generates a new initialization vector |

### `IHashProvider`

Hash data with configurable output length. Exposes `HashLengthBytes` property for the output size in bytes.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TryHash(ReadOnlySpan<byte>, Span<byte>, out int)` | `bool` | Hash, reporting bytes written |
| `TryHash(Stream, Span<byte>, out int)` | `bool` | Stream-based hashing |
| `Hash(ReadOnlySpan<byte>)` | `byte[]` | Self-allocating hashing |
| `Hash(string)` | `byte[]` | Hash a UTF8 string |
| `CreateIncremental()` | `IIncrementalHash` | Create an incremental hash for chunk-by-chunk digesting |
| `TryHashAsync(Stream, Memory<byte>, CancellationToken)` | `Task<bool>` | Genuinely async stream hashing into a caller-owned buffer |
| `HashAsync(Stream, CancellationToken)` | `Task<byte[]>` | Genuinely async self-allocating stream hashing |

### `IIncrementalHash`

A hash computation that accepts data in successive chunks. Obtained from `IHashProvider.CreateIncremental()`. Stateful, not thread-safe, and disposable.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `HashLengthBytes` | `int` | Length of the hash in bytes |
| `Append(ReadOnlySpan<byte>)` | `void` | Append data to the running hash |
| `TryGetHashAndReset(Span<byte>, out int)` | `bool` | Write the hash and reset, reporting bytes written |
| `GetHashAndReset()` | `byte[]` | Self-allocating variant of the above |

### `ISerializationProvider`

Serialize and deserialize objects supporting JSON, YAML, TOML, and other text-based formats.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `FileExtension` | `string` | Conventional extension for the format, e.g. `.yaml` |
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
