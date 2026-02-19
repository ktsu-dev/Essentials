---
status: draft
title: ktsu.Abstractions
description: A library providing a comprehensive set of interfaces for compression, encoding, encryption, hashing, serialization, caching, validation, and filesystem access with zero-allocation Try* methods and convenient default implementations.
tags:
  - abstractions
  - .net
  - csharp
  - provider pattern
  - dependency injection
  - serialization
  - compression
  - encryption
  - hashing
  - encoding
  - zero-allocation
  - spans
---

# ktsu.Abstractions

A comprehensive library of interfaces that define a consistent, high-performance API for common cross-cutting concerns:

- **Compression**: `ICompressionProvider` - compress/decompress data with Span<byte> and Stream support
- **Encryption**: `IEncryptionProvider` - encrypt/decrypt data with key and IV management
- **Hashing**: `IHashProvider` - hash data with configurable output length
- **Encoding**: `IEncodingProvider` - format/transport encoding (Base64, Hex, URL encoding)
- **Serialization**: `ISerializationProvider` - serialize/deserialize objects (JSON, YAML, TOML, etc.)
- **Filesystem**: `IFileSystemProvider` - file system operations abstraction

Each interface supports both zero-allocation Try* and Stream based methods and convenient self-allocating methods, with comprehensive async support.

## Target frameworks

This package multi-targets common frameworks for broad compatibility:

- netstandard2.1
- net5.0, net6.0, net7.0, net8.0, net9.0

Supported OS: Windows, Linux, macOS.

## Install

Via dotnet CLI:

```bash
dotnet add package ktsu.Abstractions
```

Via NuGet Package Manager:

```powershell
Install-Package ktsu.Abstractions
```

Via PackageReference in csproj:

```xml
<ItemGroup>
    <PackageReference Include="ktsu.Abstractions" Version="1.0.0" />
</ItemGroup>
```

## Quickstart

Using the implementations from the `ktsu.Common` package via DI:

```csharp
using ktsu.Abstractions;
using ktsu.Common;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddTransient<ICompressionProvider, ktsu.Common.GZipCompressionProvider>();
services.AddTransient<IEncryptionProvider, ktsu.Common.AesEncryptionProvider>();
services.AddTransient<IHashProvider, ktsu.Common.Sha256HashProvider>();
services.AddTransient<IEncodingProvider, ktsu.Common.Base64EncodingProvider>();
services.AddTransient<ISerializationProvider, ktsu.Common.JsonSerializationProvider>();
services.AddTransient<IFileSystemProvider, ktsu.Common.FileSystemProvider>();

using IServiceProvider provider = services.BuildServiceProvider();

ICompressionProvider compressionProvider = provider.GetRequiredService<ICompressionProvider>();
IEncryptionProvider encryptionProvider = provider.GetRequiredService<IEncryptionProvider>();
IHashProvider hashProvider = provider.GetRequiredService<IHashProvider>();

```

## API Design Pattern

All interfaces follow a consistent pattern:

1. **Core methods**: Zero-allocation `Try*` methods that work with `Span<byte>` and `Stream` parameters
2. **Convenience methods**: Allocating methods that call the Try* methods and handle buffer management
3. **Async support**: `Task`-based async versions of all operations with `CancellationToken` support
4. **String overloads**: UTF8-encoded string variants for convenience

### Example Implementation

Here's how to implement a custom MD5 hash provider:

```csharp
using System.Security.Cryptography;
using ktsu.Abstractions;

public sealed class MyMD5HashProvider : IHashProvider
{
    public int HashLengthBytes => 16; // MD5 produces 128-bit (16-byte) hashes

    // Zero-allocation implementation
    public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        if (destination.Length < HashLengthBytes)
        {
            return false;
        }

        using var md5 = MD5.Create();
        byte[] hashBytes = md5.ComputeHash(data.ToArray());
        hashBytes.AsSpan().CopyTo(destination);
        
        return true;
    }

    // Stream implementation
    public bool TryHash(Stream data, Span<byte> destination)
    {
        if (destination.Length < HashLengthBytes)
        {
            return false;
        }

        using var md5 = MD5.Create();
        byte[] hashBytes = md5.ComputeHash(data);
        hashBytes.AsSpan().CopyTo(destination);
        
        return true;
    }
    
    // All other methods (Hash(), HashAsync(), etc.) are provided by default implementations
}
```

### Usage Example

```csharp
using System.Text;
using ktsu.Abstractions;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddTransient<IHashProvider, MyMD5HashProvider>();

using IServiceProvider provider = services.BuildServiceProvider();
IHashProvider hashProvider = provider.GetRequiredService<IHashProvider>();

// Using the convenience method (allocates and manages buffer)
byte[] inputData = Encoding.UTF8.GetBytes("Hello, World!");
byte[] hash = hashProvider.Hash(inputData);

// Using string convenience method
string textHash = Convert.ToHexString(hashProvider.Hash("Hello, World!"));

// Using the zero-allocation Try method
Span<byte> hashBuffer = stackalloc byte[hashProvider.HashLengthBytes];
if (hashProvider.TryHash(inputData, hashBuffer))
{
    string result = Convert.ToHexString(hashBuffer);
}

// Async usage
byte[] asyncHash = await hashProvider.HashAsync(inputData);
```
## Design principles

- **Core methods support zero-allocation and streaming implementations**: The fundamental operations use `Try*` methods that work with caller-provided `Span<byte>` or `Stream` destinations, returning boolean success indicators.
- **Default implementations provide convenience**: Interfaces include default implementations for allocating methods (`Hash()`, `Compress()`, etc.) that handle buffer management and forward to the Try* methods.
- **Comprehensive async support**: All operations have async variants with proper `CancellationToken` support.
- **String convenience methods**: UTF8-encoded string overloads are provided where appropriate for developer convenience.
- **Minimal implementation burden**: Implementers only need to provide the core Try* methods; all other functionality is inherited through default interface implementations.

## Security notes

- Implementations of `IEncryptionProvider` should rely on proven libraries (e.g., .NET BCL crypto, libsodium bindings) and follow best practices (AEAD modes, random IVs/nonces, key management).
- `IEncodingProvider` is for format/transport encodings (Base64, Hex) — these are NOT encryption and provide no security.

## Thread-safety and lifetime

- Provider implementations should be stateless or internally synchronized and safe to register as singletons in DI.
- If an implementation maintains internal mutable state, document the concurrency model and recommended lifetime.

## Why use these abstractions?

- **Consistency**: A single, predictable surface across implementations
- **Testability**: Swap implementations and mock easily, especially for filesystem
- **Separation of concerns**: Keep app code free of vendor-specific details

## Contributing

- Fork the repo and create a feature branch
- Implement or refine providers/analyzers and add tests
- Open a pull request

## License

Licensed under the MIT License. See [LICENSE.md](LICENSE.md) for details.
