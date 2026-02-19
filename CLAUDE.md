# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ktsu.Abstractions is a .NET library providing high-performance interfaces for common cross-cutting concerns: compression, encoding, encryption, hashing, serialization, caching, validation, and filesystem access. The library emphasizes zero-allocation operations using Span<byte> and default interface implementations to minimize implementation burden.

## Build and Test Commands

### Build
```bash
dotnet build
```

### Restore dependencies
```bash
dotnet restore
```

### Clean build artifacts
```bash
dotnet clean
```

### Multi-targeting
This project targets: net10.0, net9.0, net8.0, net7.0, net6.0, netstandard2.1. Ensure your .NET SDK version is 10.0.100 or higher (specified in global.json).

## Project Architecture

### Core Design Pattern

All provider interfaces follow a consistent three-tier pattern:

1. **Core Try* methods**: Zero-allocation methods working with `Span<byte>` or `Stream` parameters that return `bool` for success/failure
2. **Convenience methods**: Self-allocating methods that call Try* methods and manage buffers automatically
3. **Async variants**: Task-based async versions with `CancellationToken` support

Implementers only need to implement the core Try* methods - all convenience and async methods are provided via default interface implementations.

### Interface Hierarchy

Each provider interface defines:
- **Span-based operations**: `TryOperation(ReadOnlySpan<byte> data, Span<byte> destination)` - zero allocation
- **Stream-based operations**: `TryOperation(Stream data, Stream destination)` - for larger data
- **Convenience overloads**: `Operation(ReadOnlySpan<byte> data)` - auto-allocates and returns result
- **String overloads**: UTF8-encoded string variants where applicable
- **Async methods**: All operations have async counterparts with cancellation token support

### Provider Interfaces

**ICompressionProvider** (Abstractions/ICompressionProvider.cs)
- Core: `TryCompress()` and `TryDecompress()` with Span<byte> and Stream overloads
- Convenience: `Compress()`, `Decompress()` with automatic buffer management
- String support for text compression

**IEncryptionProvider** (Abstractions/IEncryptionProvider.cs)
- Core: `TryEncrypt()` and `TryDecrypt()` requiring key and IV parameters
- Key generation: `GenerateKey()` and `GenerateIV()`
- Security note: Implementations should use AEAD modes and proper key management

**IHashProvider** (Abstractions/IHashProvider.cs)
- Property: `HashLengthBytes` defines output size
- Core: `TryHash()` with Span<byte> and Stream overloads
- Convenience: `Hash()` methods that allocate the hash buffer

**IEncodingProvider** (Abstractions/IEncodingProvider.cs)
- Core: `TryEncode()` and `TryDecode()` with Span<byte> and Stream overloads
- For format/transport encodings (Base64, Hex, URL encoding) — NOT text character encodings

**ISerializationProvider** (Abstractions/ISerializationProvider.cs)
- Core: `TrySerialize()` using TextWriter, `Deserialize<T>()` using ReadOnlySpan<byte>
- Convenience: `Serialize()`, `Deserialize<T>(string)`, `Deserialize<T>(TextReader)`
- Generic type support for deserialization
- Used by both serialization (JSON, MessagePack) and configuration (JSON, YAML, TOML) providers

**IFileSystemProvider** (Abstractions/IFileSystemProvider.cs)
- Inherits from Testably.Abstractions.IFileSystem
- Enables filesystem abstraction for testability

### Default Implementation Pattern

All convenience methods are provided as default interface implementations. For example, in IHashProvider:
- `TryHash(ReadOnlySpan<byte>, Span<byte>)` - implementer must provide
- `TryHash(Stream, Span<byte>)` - implementer must provide
- `Hash(ReadOnlySpan<byte>)` - default implementation calls TryHash and manages buffer
- `HashAsync(...)` - default implementation wraps synchronous method in Task.Run

When implementing a provider, you only need to implement the core Try* methods with Span and Stream parameters.

### Multi-Framework Considerations

The codebase uses preprocessor directives sparingly. Some interfaces use `[SuppressMessage]` to suppress CA1510 (ArgumentNullException throw helper) since it's not available in netstandard2.1.

When working with newer .NET features:
- Check if the feature is available in netstandard2.1 before using
- Use `#if` directives for framework-specific code if needed
- Prefer framework-agnostic approaches when possible

## CI/CD Pipeline

The project uses a custom PSBuild PowerShell module (scripts/PSBuild.psm1) that handles the complete build, test, pack, and release workflow. This is executed via GitHub Actions (.github/workflows/dotnet.yml).

The pipeline:
1. Builds all target frameworks
2. Runs tests with coverage collection
3. Analyzes with SonarQube (if configured)
4. Versions and packages on main branch
5. Creates GitHub releases automatically
6. Updates winget manifests

When modifying the build process, check scripts/PSBuild.psm1 and .github/workflows/dotnet.yml.

## Dependencies

- **Testably.Abstractions**: Provides the base IFileSystem interface for filesystem abstraction
- **ktsu.Sdk**: Custom SDK that provides common build configuration and metadata management

## Code Style and Conventions

- All files include copyright header: `// Copyright (c) ktsu.dev`
- Interfaces follow XML documentation standards with full parameter descriptions
- Use expression-bodied members for simple default implementations
- Null checks use explicit throws (not throw helpers) for netstandard2.1 compatibility
- Async methods use `ProviderHelpers.RunAsync()` for consistent cancellation and Task.Run wrapping
- Default implementations should not allocate unnecessarily - prefer span operations
- Common patterns (async wrappers, span-to-stream bridges, UTF8 transforms) are centralized in `ProviderHelpers.cs`

## Testing Implementations

To test a custom provider implementation:
1. Implement only the required Try* methods (Span and Stream variants)
2. The default implementations provide all other functionality automatically
3. Test both the core Try* methods and convenience methods
4. Verify async methods respect CancellationToken

Example minimal implementation:
```csharp
public class MyHashProvider : IHashProvider
{
    public int HashLengthBytes => 32;

    public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        // Implementation here
    }

    public bool TryHash(Stream data, Span<byte> destination)
    {
        // Implementation here
    }

    // All other methods inherited via default implementations
}
```

## Metadata Files

The repository contains auto-generated metadata files:
- VERSION.md - current version
- CHANGELOG.md - full changelog
- LATEST_CHANGELOG.md - latest version changes
- DESCRIPTION.md - package description
- TAGS.md - package tags

These are managed by the PSBuild pipeline and should not be manually edited.
