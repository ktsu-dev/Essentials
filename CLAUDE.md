# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Restore, build, and test (standard workflow)
dotnet restore
dotnet build
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Build specific configuration
dotnet build -c Release
```

## Project Structure

This is a .NET library (`ktsu.Essentials`) providing high-performance interfaces and implementations for common cross-cutting concerns: compression, encoding, encryption, hashing, serialization, caching, persistence, validation, logging, navigation, command execution, and filesystem access. The solution uses:

- **ktsu.Sdk** - Custom SDK providing shared build configuration
- **MSTest.Sdk** - Test project SDK with Microsoft Testing Platform
- Multi-targeting: net10.0, net9.0, net8.0, net7.0, net6.0, netstandard2.1

### Key Files

- `Essentials/ICompressionProvider.cs` - Compression/decompression interface with Span, Stream, and string support
- `Essentials/IEncodingProvider.cs` - Format/transport encoding interface (Base64, Hex)
- `Essentials/IEncryptionProvider.cs` - Encryption/decryption interface with key/IV management
- `Essentials/IHashProvider.cs` - Hashing interface with configurable output length
- `Essentials/ISerializationProvider.cs` - Object serialization/deserialization interface
- `Essentials/ISerializationOptions.cs` - Configurable serialization options (naming, inclusion, boxing policies)
- `Essentials/ICacheProvider.cs` - Generic cache interface with expiration and get-or-add
- `Essentials/IPersistenceProvider.cs` - Object persistence interface with pluggable backends
- `Essentials/IValidationProvider.cs` - Validation interface with structured results
- `Essentials/ILoggingProvider.cs` - Logging interface with six severity levels
- `Essentials/INavigationProvider.cs` - Browser-like back/forward navigation interface
- `Essentials/ICommandExecutor.cs` - Shell command execution interface
- `Essentials/IFileSystemProvider.cs` - Filesystem abstraction extending Testably.Abstractions
- `Essentials/ProviderHelpers.cs` - Internal utilities for async wrapping, stream bridging, UTF8 transforms
- `Essentials/PersistenceProviderUtilities.cs` - Shared utilities for persistence providers (safe filenames, key conversion)
- `Essentials/PersistenceProviderException.cs` - Custom exception for persistence operations

### Provider Implementations (in solution)

- **CompressionProviders/**: Gzip, Brotli, Deflate, ZLib — namespace `ktsu.Essentials.CompressionProviders`
- **EncodingProviders/**: Base64, Hex — namespace `ktsu.Essentials.EncodingProviders`
- **EncryptionProviders/**: Aes — namespace `ktsu.Essentials.EncryptionProviders`
- **HashProviders/**: MD5, SHA1, SHA256, SHA384, SHA512, FNV1_32, FNV1a_32, FNV1_64, FNV1a_64, CRC32, CRC64, XxHash32, XxHash64, XxHash3, XxHash128 — namespace `ktsu.Essentials.HashProviders`
- **SerializationProviders/**: Json, Yaml, Toml — namespace `ktsu.Essentials.SerializationProviders`
- **FileSystemProviders/**: Native — namespace `ktsu.Essentials.FileSystemProviders`
- **CommandExecutors/**: Native — namespace `ktsu.Essentials.CommandExecutors`
- **LoggingProviders/**: Console — namespace `ktsu.Essentials.LoggingProviders`
- **CacheProviders/**: InMemory — namespace `ktsu.Essentials.CacheProviders`
- **NavigationProviders/**: InMemory — namespace `ktsu.Essentials.NavigationProviders`
- **PersistenceProviders/**: AppData, FileSystem, InMemory, Temp — namespace `ktsu.Essentials.PersistenceProviders`

### Namespace Convention

Interfaces are defined in the `ktsu.Essentials` namespace (in the `Essentials/` directory). Provider implementations use sub-namespaces matching their category directory: `ktsu.Essentials.<Category>`. For example, `SHA256` is in `ktsu.Essentials.HashProviders` and `Gzip` is in `ktsu.Essentials.CompressionProviders`.

### Dependencies

- **Testably.Abstractions** - Base `IFileSystem` interface for filesystem abstraction
- **Polyfill** - Backports of newer .NET APIs for older target frameworks
- **Microsoft.SourceLink.GitHub / AzureRepos.Git** - Source link support for debugging

## Architecture

All provider interfaces follow a consistent three-tier pattern:

1. **Core Try\* methods**: Zero-allocation methods working with `Span<byte>` or `Stream` parameters that return `bool` for success/failure. These are the only methods implementers must provide.
2. **Convenience methods**: Self-allocating methods that call Try\* methods and manage buffers automatically. Provided via default interface implementations.
3. **Async variants**: Task-based async versions with `CancellationToken` support. Provided via `ProviderHelpers.RunAsync()`.

Common patterns are centralized in `ProviderHelpers.cs`:

- `RunAsync()` - Wraps sync methods in `Task.Run` with cancellation
- `ExecuteToByteArray()` - Calls a try-operation with a MemoryStream destination
- `SpanToStreamBridge()` - Bridges Span input to Stream-based operations
- `Utf8Transform()` - Applies byte operations to UTF8 strings

### Multi-Framework Considerations

The codebase uses `[SuppressMessage]` attributes for APIs not available in netstandard2.1 (e.g., CA1510 for `ArgumentNullException` throw helpers). The `Polyfill` package backports newer APIs where possible. When adding new code, verify availability in netstandard2.1 and use `#if` directives if needed.

## Testing

Tests use **MSTest.Sdk** targeting net10.0 only. The test project (`Essentials.Tests/`) references all provider implementations and tests them through the interface contracts. Key test files:

- `HashProviderTests.cs` - Tests all 17 hash provider implementations
- `CacheProviderTests.cs` - Tests cache operations including expiration
- `CommandExecutorTests.cs` - Tests command execution
- `EncodingProviderTests.cs` - Tests Base64 and Hex encoding
- `FileSystemProviderTests.cs` - Tests filesystem operations
- `LoggingProviderTests.cs` - Tests logging provider
- `NavigationProviderTests.cs` - Tests navigation stack behavior
- `PersistenceProviderTests.cs` - Tests all persistence backends
- `ConfigurationProviderTests.cs` - Tests serialization configuration
- `RoundTripTests.cs` - Tests compression/encoding/encryption round-trips
- `DiTests.cs` - Tests dependency injection registration

## CI/CD

Uses `scripts/PSBuild.psm1` PowerShell module for CI pipeline. Version increments are controlled by commit message tags: `[major]`, `[minor]`, `[patch]`, `[pre]`.

## Code Quality

Do not add global suppressions for warnings. Use explicit suppression attributes with justifications when needed, with preprocessor defines only as fallback. Make the smallest, most targeted suppressions possible.
