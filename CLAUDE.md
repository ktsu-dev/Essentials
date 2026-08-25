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

This is a .NET library (`ktsu.Essentials`) providing high-performance interfaces and implementations for common cross-cutting concerns: compression, encoding, obfuscation, encryption, hashing, serialization, caching, persistence, validation, logging, navigation, command execution, and filesystem access. The solution uses:

- **ktsu.Sdk** - Custom SDK providing shared build configuration
- **MSTest.Sdk** - Test project SDK with Microsoft Testing Platform
- Multi-targeting: net10.0, net9.0, net8.0, net7.0, net6.0, netstandard2.1

### Key Files

- `Essentials/ICompressionProvider.cs` - Compression/decompression interface with Span, Stream, and string support
- `Essentials/IEncodingProvider.cs` - Format/transport encoding interface (Base64, Hex)
- `Essentials/IObfuscationProvider.cs` - Reversible obfuscation interface (NOT encryption); implementations compose encoding transforms and simple byte operations
- `Essentials/IEncryptionProvider.cs` - Encryption/decryption interface with key/IV management
- `Essentials/IHashProvider.cs` - Hashing interface with configurable output length
- `Essentials/IIncrementalHash.cs` - Chunk-by-chunk hashing contract; `IHashProvider.CreateIncremental()` returns one
- `Essentials/IncrementalHashAdapter.cs` - Public adapter over `System.Security.Cryptography.IncrementalHash`, shared by the cryptographic hash providers
- `Essentials/BufferingIncrementalHash.cs` - Internal buffering fallback behind the `CreateIncremental()` default body
- `Shared/NonCryptoIncrementalHash.cs` - Adapter over `NonCryptographicHashAlgorithm`, linked into the six `System.IO.Hashing` providers rather than placed in the interfaces-only package
- `Essentials/IKeyedHashProvider.cs` - Keyed hashing (HMAC) interface for authenticating data with a secret key
- `Essentials/FixedTimeComparison.cs` - Static fixed-time byte comparison for tags obtained outside `IKeyedHashProvider.Verify`
- `Shared/HmacKeyedHashCore.cs` - HMAC implementation shared across algorithms, linked into the three keyed hash provider projects rather than placed in the interfaces package
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

Each provider implementation ships as its own project/package named `Essentials.<Category>.<Impl>` (NuGet id `ktsu.Essentials.<Category>.<Impl>`):

- **CompressionProviders**: Gzip, Brotli, Deflate, ZLib (ZLib targets net6.0+ only, not netstandard2.1)
- **EncodingProviders**: Base64, Hex
- **ObfuscationProviders**: Xor, Caesar, Reverse, BitRotate, Base64, Hex, Composite
- **EncryptionProviders**: Aes
- **HashProviders**: MD5, SHA1, SHA256, SHA384, SHA512, FNV1_32, FNV1a_32, FNV1_64, FNV1a_64, CRC32, CRC64, XxHash32, XxHash64, XxHash3, XxHash128
- **KeyedHashProviders**: HmacSha256, HmacSha384, HmacSha512
- **SerializationProviders**: Json (System.Text.Json), NewtonsoftJson, Yaml, Toml
- **FileSystemProviders**: Native
- **CommandExecutors**: Native
- **LoggingProviders**: Console
- **CacheProviders**: InMemory
- **NavigationProviders**: InMemory
- **PersistenceProviders**: DataHome, ConfigHome, FileSystem, InMemory, Temp (DataHome and ConfigHome resolve XDG paths — `$XDG_DATA_HOME` else `~/.local/share/<app>`, `$XDG_CONFIG_HOME` else `~/.config/<app>` — with the same layout on every platform; `~` is `%USERPROFILE%` on Windows. Both delegate storage to `FileSystemPersistenceProvider`.)

The **`Essentials.All`** project (`ktsu.Essentials.All`) is a meta-package that references every provider implementation for a one-install "batteries-included" experience; consumers can otherwise cherry-pick individual provider packages.

### Namespace & Naming Convention

Interfaces are defined in the `ktsu.Essentials` namespace (in the `Essentials/` directory). Each provider implementation lives in its own directory `Essentials.<Category>.<Impl>/`, in namespace `ktsu.Essentials.<Category>.<Impl>`, with the class named `<Impl><Category-singular>Provider`. For example, the SHA-256 hash provider is class `SHA256HashProvider` in namespace `ktsu.Essentials.HashProviders.SHA256`, and the XOR obfuscator is class `XorObfuscationProvider` in `ktsu.Essentials.ObfuscationProviders.Xor`. Higher-level concerns compose primitives rather than duplicating them: configuration is an `IPersistenceProvider<TKey>` over a serializer, and each obfuscator composes an encoding transform or a simple reversible byte operation. Obfuscators that wrap an `IEncodingProvider` (Base64, Hex) keep both a parameterless and an encoder-accepting constructor public and are registered via a DI factory lambda to avoid greedy-constructor selection.

### Dependencies

- **Testably.Abstractions** - Base `IFileSystem` interface for filesystem abstraction
- **Polyfill** - Backports of newer .NET APIs for older target frameworks
- **Microsoft.SourceLink.GitHub / AzureRepos.Git** - Source link support for debugging

## Architecture

All provider interfaces follow a consistent three-tier pattern:

1. **Core Try\* methods**: Buffer-based methods over `Span<byte>` or `Stream`. Span overloads are `bool TryX(source, destination, out int bytesWritten)`, paired with a `GetMax…Length` bound per category so callers can size buffers. These are the only methods implementers must provide.
2. **Convenience methods**: Self-allocating methods that call Try\* methods and manage buffers automatically. Provided via default interface implementations.
3. **Async variants**: Task-based async versions with `CancellationToken` support. The stream paths of the compression providers and of `AesEncryptionProvider`, along with `IHashProvider.TryHashAsync(Stream, ...)`, are genuinely asynchronous — real `ReadAsync`/`WriteAsync`, no thread held. The rest are still `Task.Run` wrappers over synchronous work via `ProviderHelpers.RunAsync()`; see issue #8. A provider makes its stream paths genuine by declaring the two `Try…Async(Stream, Stream, ...)` primitives itself, which replaces the default implementation; the four derived stream defaults compose over those primitives, so overriding two members converts all six. Span-destination async overloads do not exist — an `out` parameter cannot cross an async boundary.

Common patterns are centralized in `ProviderHelpers.cs`:

- `RunAsync()` - Wraps sync methods in `Task.Run` with cancellation. Used by the in-memory async variants and by any stream path whose provider has not declared its own asynchronous primitives.
- `ExecuteToByteArray()` - Calls a try-operation with a MemoryStream destination
- `SpanToStreamBridge()` - Bridges Span input to Stream-based operations
- `Utf8Transform()` - Applies byte operations to UTF8 strings

### Multi-Framework Considerations

The codebase uses `[SuppressMessage]` attributes for APIs not available in netstandard2.1 (e.g., CA1510 for `ArgumentNullException` throw helpers). The `Polyfill` package backports newer APIs where possible. When adding new code, verify availability in netstandard2.1 and use `#if` directives if needed.

## Testing

Tests use **MSTest.Sdk** targeting net10.0 only. The test project (`Essentials.Tests/`) references all provider implementations and tests them through the interface contracts. Key test files:

- `HashProviderTests.cs` - Tests all 15 hash provider implementations
- `IncrementalHashTests.cs` - Tests `CreateIncremental()` and async stream hashing across all 15 hash providers, asserting incremental output equals one-shot output
- `KeyedHashProviderTests.cs` - Tests all 3 HMAC keyed hash providers, `Verify`, and `FixedTimeComparison`
- `CacheProviderTests.cs` - Tests cache operations including expiration
- `CommandExecutorTests.cs` - Tests command execution
- `EncodingProviderTests.cs` - Tests Base64 and Hex encoding
- `ObfuscationProviderTests.cs` - Tests all obfuscation providers via round-trip (obfuscate → deobfuscate)
- `FileSystemProviderTests.cs` - Tests filesystem operations
- `LoggingProviderTests.cs` - Tests logging provider
- `NavigationProviderTests.cs` - Tests navigation stack behavior
- `PersistenceProviderTests.cs` - Tests all persistence backends
- `SerializationProviderTests.cs` - Tests serialization provider implementations
- `RoundTripTests.cs` - Tests compression/encoding/encryption round-trips
- `DiTests.cs` - Tests dependency injection registration

## CI/CD

Uses `scripts/PSBuild.psm1` PowerShell module for CI pipeline. Version increments are controlled by commit message tags: `[major]`, `[minor]`, `[patch]`, `[pre]`.

## Code Quality

Do not add global suppressions for warnings. Use explicit suppression attributes with justifications when needed, with preprocessor defines only as fallback. Make the smallest, most targeted suppressions possible.
