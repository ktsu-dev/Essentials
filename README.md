# ktsu.Essentials

> A comprehensive .NET library providing high-performance interfaces and implementations for common cross-cutting concerns including compression, encoding, obfuscation, encryption, hashing, serialization, caching, persistence, validation, logging, navigation, randomness, probability distributions, command execution, and filesystem access.

[![License](https://img.shields.io/github/license/ktsu-dev/Essentials.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.Essentials?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.Essentials?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.Essentials?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.Essentials)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/Essentials?label=Commits&logo=github)](https://github.com/ktsu-dev/Essentials/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/Essentials?label=Contributors&logo=github)](https://github.com/ktsu-dev/Essentials/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/Essentials/dotnet.yml?branch=main&label=Build&logo=github)](https://github.com/ktsu-dev/Essentials/actions)

## Introduction

`ktsu.Essentials` defines a consistent, high-performance API for common cross-cutting concerns in .NET applications. Each provider interface follows a three-tier pattern: core `Try*` methods over `Span<byte>` and `Stream` that report how many bytes they wrote, convenient self-allocating methods, and async variants with `CancellationToken` support. Implementers only need to provide the core `Try*` methods — all convenience and async methods are provided via default interface implementations, though some, like `IHashProvider.CreateIncremental()`, are worth overriding for efficiency. The `ktsu.Essentials` package is interfaces only; implementations ship as separate `ktsu.Essentials.<Category>.<Impl>` packages, with `ktsu.Essentials.All` bundling every one of them. Higher-level concerns are expressed by composition rather than duplication — configuration is simply an `IPersistenceProvider<TKey>` over a serializer, and obfuscation composes encoding transforms.

## Features

- **Compression**: `ICompressionProvider` with Gzip, Brotli, Deflate, and ZLib implementations
- **Encoding**: `IEncodingProvider` with Base64 and Hex implementations for format/transport encoding
- **Obfuscation**: `IObfuscationProvider` with XOR, Caesar, bit-rotation, byte-reversal, Base64, and Hex implementations, plus a `Composite` provider that pipelines several together. Obfuscation is reversible but is **not** encryption — it provides no confidentiality
- **Dependency Injection**: every provider package ships an `Add<Impl><Category>Provider()` extension; `ktsu.Essentials.All` adds per-category helpers and a single `AddEssentials()`. Registrations are idempotent and expose each provider by both concrete type and interface
- **Encryption**: `IEncryptionProvider` with AES implementation including key and IV generation. Provides confidentiality only, not tamper detection
- **Hashing**: `IHashProvider` with 15 implementations (MD5, SHA1/256/384/512, FNV1/FNV1a 32/64-bit, CRC32/64, XxHash32/64/3/128)
- **Keyed Hashing**: `IKeyedHashProvider` with HMAC-SHA256/384/512 implementations for authenticating data, plus `Verify` for fixed-time tag checking
- **Serialization**: `ISerializationProvider` with System.Text.Json, Newtonsoft.Json, YAML, and TOML implementations plus configurable `ISerializationOptions`
- **Caching**: `ICacheProvider<TKey, TValue>` with in-memory implementation supporting expiration and get-or-add semantics
- **Persistence**: `IPersistenceProvider<TKey>` with DataHome, ConfigHome, FileSystem, InMemory, and Temp implementations. `DataHome` and `ConfigHome` follow the XDG Base Directory layout on every platform — `$XDG_DATA_HOME` or `~/.local/share/<app>` for application state, `$XDG_CONFIG_HOME` or `~/.config/<app>` for user settings — with `~` resolving to `%USERPROFILE%` on Windows
- **Validation**: `IValidationProvider<T>` with structured results, error codes, and throw-on-failure support
- **Logging**: `ILoggingProvider` with console implementation supporting six severity levels
- **Navigation**: `INavigationProvider<T>` with in-memory implementation for browser-like back/forward navigation
- **Randomness**: `IRandomProvider` with four implementations — `Native` over `System.Random`, `Crypto` over the OS cryptographic generator, and `Xoshiro` (xoshiro256\*\*) and `Pcg` (PCG-XSH-RR), whose seeded sequences are fixed by the package rather than by the framework and so replay identically on any machine or runtime. One primitive to implement; range-limited draws are free of modulo bias, and shuffling, weighted choice and sampling without replacement come with it
- **Probability Distributions**: `IContinuousDistribution` and `IDiscreteDistribution` with CDF, quantile, survival function, density or mass, moments and sampling. Ten implementations — uniform, normal, exponential, log-normal and triangular; Bernoulli, binomial, Poisson, geometric and categorical. Where a CDF has no closed form it is evaluated through the incomplete gamma and incomplete beta functions rather than by summation, so the cost does not grow with the value asked about
- **Command Execution**: `ICommandExecutor` with native implementation for running shell commands and capturing output
- **Filesystem**: `IFileSystemProvider` extending Testably.Abstractions for testable filesystem access
- **Explicit Buffer Contract**: every span operation is `bool TryX(source, destination, out int bytesWritten)` and each category exposes a `GetMax…Length` bound, so callers can size a buffer up front and know exactly how much was written. Encoding, hashing and obfuscation run allocation-free on the span path; compression and encryption still buffer internally, because the underlying BCL APIs for those are stream-only
- **Minimal Implementation Burden**: Default interface implementations reduce boilerplate — implement only the core `Try*` methods
- **Async Support**: Operations expose async variants with `CancellationToken` support. Stream hashing, keyed hash stream hashing, and the stream paths of the compression, AES encryption and encoding providers, are genuinely asynchronous — they read and write with `ReadAsync`/`WriteAsync` and hold no thread. The obfuscation, serialization and in-memory variants are convenience wrappers that run synchronous work on the thread pool; span-destination operations have no async form, because an `out` parameter cannot cross an await boundary
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

### Keyed Hashing

Pair `IKeyedHashProvider` with `IEncryptionProvider` to detect tampering, because encryption alone gives confidentiality but not integrity:

```csharp
IEncryptionProvider encryption = provider.GetRequiredService<IEncryptionProvider>();
IKeyedHashProvider keyedHash = provider.GetRequiredService<IKeyedHashProvider>();

byte[] encryptionKey = encryption.GenerateKey();
byte[] iv = encryption.GenerateIV();
byte[] authenticationKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(keyedHash.HashLengthBytes);

byte[] ciphertext = encryption.Encrypt("Hello, World!"u8, encryptionKey, iv);

// Authenticate the IV together with the ciphertext. A tag over the ciphertext alone
// leaves the IV rewritable, and CBC recovers the first plaintext block from it.
byte[] authenticated = new byte[iv.Length + ciphertext.Length];
iv.CopyTo(authenticated, 0);
ciphertext.CopyTo(authenticated, iv.Length);

byte[] tag = keyedHash.Hash(authenticationKey, authenticated);

// ...iv, ciphertext and tag travel together to the other side...
byte[] receivedTag = tag;

// On the way back in, verify before decrypting.
if (!keyedHash.Verify(authenticationKey, authenticated, receivedTag))
{
    throw new System.Security.Cryptography.CryptographicException("Ciphertext failed authentication.");
}

byte[] plaintext = encryption.Decrypt(ciphertext, encryptionKey, iv);
```

For a large payload, authenticate incrementally with `CreateIncremental` and compare the result with
`FixedTimeComparison.FixedTimeEquals` instead of allocating a concatenated copy the way the example above does.

### Randomness

```csharp
using ktsu.Essentials;
using ktsu.Essentials.RandomProviders.Crypto;
using ktsu.Essentials.RandomProviders.Xoshiro;

// Resolve whichever generator the container was given, or pick one deliberately.
IRandomProvider random = provider.GetRequiredService<CryptoRandomProvider>();

int roll = random.NextInt32(1, 7);              // unbiased over a range that does not divide 2^32
double unit = random.NextDouble();               // [0, 1) at full 53-bit resolution
bool heads = random.NextBoolean();
bool rareEvent = random.NextBoolean(0.001);      // a Bernoulli trial
byte[] token = random.NextBytes(32);

List<Card> deck = BuildDeck();
random.Shuffle(deck);                                          // every ordering equally likely
Card drawn = random.Choose(deck);
Item loot = random.Choose(items, weights: [60.0, 30.0, 10.0]);  // weighted
IReadOnlyList<Player> team = random.Sample(roster, 5);          // distinct, without replacement

// A seeded generator replays exactly, on any platform and any framework version — which is what
// makes a simulation reproducible and a test that asserts on generated data possible.
XoshiroRandomProvider seeded = new(seed: 20260913UL);
XoshiroRandomProvider replay = new(seed: 20260913UL);
bool identical = seeded.NextUInt64() == replay.NextUInt64();  // always true
```

Pick the implementation by what the output is for. `CryptoRandomProvider` is the one to use whenever the
output is a secret or authenticates something — tokens, salts, nonces, a shuffle that must not be
predictable — and it is stateless and thread-safe. `XoshiroRandomProvider` and `PcgRandomProvider` are far
faster and replay from a seed, which the cryptographic provider cannot do by design; PCG adds a stream
parameter, so one seed can give each actor in a simulation its own independent generator.
`NativeRandomProvider` is the platform default, repeatable within a framework version but not across them.
The three seedable providers carry state and are not thread-safe, which is why the container hands out a
fresh instance per consumer rather than sharing one.

### Probability Distributions

```csharp
using ktsu.Essentials;
using ktsu.Essentials.DistributionProviders.Normal;
using ktsu.Essentials.DistributionProviders.Poisson;
using ktsu.Essentials.RandomProviders.Xoshiro;

IContinuousDistribution latency = new NormalDistributionProvider(mean: 250.0, standardDeviation: 40.0);

double median = latency.Median;                          // 250
double p95 = latency.Quantile(0.95);                     // the 95th percentile, 315.8 ms
double withinBudget = latency.Cdf(300.0);                // P(latency <= 300 ms) = 0.894
double overBudget = latency.SurvivalFunction(300.0);     // the complement, computed directly

// The survival function is not just 1 - Cdf. Far out in the tail the subtraction rounds the answer
// away: 570 ms is eight standard deviations out, where the true tail is 6.2e-16 and subtracting a
// CDF of 0.9999999999999994 from one keeps about one digit of it.
double eightSigma = latency.SurvivalFunction(570.0);

// Sampling takes the randomness at the call site, so the distribution stays immutable and shareable.
IRandomProvider random = new XoshiroRandomProvider(seed: 42UL);
double[] simulated = latency.Sample(random, count: 10_000);

// Discrete distributions add the mass function, and the CDF is still a single evaluation.
IDiscreteDistribution arrivals = new PoissonDistributionProvider(rate: 4.5);

double exactlyThree = arrivals.Pmf(3);                   // P(N = 3) = 0.169
double atMostThree = arrivals.Cdf(3);                    // P(N <= 3) = 0.342
int busyHour = arrivals.Quantile(0.99);                  // the count only 1% of intervals exceed
int observed = arrivals.Sample(random);
```

Every distribution requires only its CDF, quantile, support and first two moments; sampling defaults to
inverting the CDF, the median to the half quantile, and a discrete quantile to bisecting the CDF, so a new
distribution is a small class. Nothing in either interface is asynchronous, deliberately: a draw is a few
arithmetic instructions over in-memory state, and scheduling one on the thread pool would cost far more
than the work.

The triangular, binomial and categorical distributions are left out of `AddEssentials()` because none of
them has a standard form to default to — a three-point estimate, a trial count and a weight vector all
belong to the situation being modelled. Register those from their own packages:

```csharp
using ktsu.Essentials.DistributionProviders.Categorical;
using ktsu.Essentials.DistributionProviders.Triangular;

services.AddTriangularDistributionProvider(minimum: 2.0, mode: 5.0, maximum: 14.0);  // a task estimate
services.AddCategoricalDistributionProvider([60.0, 30.0, 9.0, 1.0]);                 // a loot table
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
    public IIncrementalHash CreateIncremental() => new MyIncrementalHash(); // ...implementing IIncrementalHash over your algorithm's running state
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

Encrypt and decrypt data with key and IV management. Provides confidentiality only, not tamper detection. Pair with `IKeyedHashProvider` to authenticate the initialization vector and the ciphertext together before decrypting.

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

A hash computation that accepts data in successive chunks. Obtained from `IHashProvider.CreateIncremental()` or `IKeyedHashProvider.CreateIncremental(key)`. Stateful, not thread-safe, and disposable.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `HashLengthBytes` | `int` | Length of the hash in bytes |
| `Append(ReadOnlySpan<byte>)` | `void` | Append data to the running hash |
| `TryGetHashAndReset(Span<byte>, out int)` | `bool` | Write the hash and reset, reporting bytes written |
| `GetHashAndReset()` | `byte[]` | Self-allocating variant of the above |

### `IKeyedHashProvider`

Compute and verify authentication tags (MACs) over data using a secret key. Exposes `HashLengthBytes` property for the tag size in bytes. See [Keyed Hashing](#keyed-hashing) above for a usage example that pairs this with `IEncryptionProvider`.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `TryHash(ReadOnlySpan<byte>, ReadOnlySpan<byte>, Span<byte>, out int)` | `bool` | Compute a tag, reporting bytes written |
| `TryHash(ReadOnlySpan<byte>, Stream, Span<byte>, out int)` | `bool` | Stream-based tag computation |
| `CreateIncremental(ReadOnlySpan<byte>)` | `IIncrementalHash` | Create a keyed incremental hash for chunk-by-chunk authentication |
| `TryHashAsync(ReadOnlyMemory<byte>, Stream, Memory<byte>, CancellationToken)` | `Task<bool>` | Genuinely async stream tag computation into a caller-owned buffer |
| `HashAsync(ReadOnlyMemory<byte>, Stream, CancellationToken)` | `Task<byte[]>` | Genuinely async self-allocating stream tag computation |
| `HashAsync(ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, CancellationToken)` | `Task<byte[]>` | Async self-allocating tag computation |
| `Hash(ReadOnlySpan<byte>, ReadOnlySpan<byte>)` | `byte[]` | Self-allocating tag computation |
| `Hash(ReadOnlySpan<byte>, string)` | `byte[]` | Tag over a UTF8 string |
| `Hash(ReadOnlySpan<byte>, Stream)` | `byte[]` | Self-allocating stream-based tag computation |
| `Verify(ReadOnlySpan<byte>, ReadOnlySpan<byte>, ReadOnlySpan<byte>)` | `bool` | Fixed-time check of a tag against data |

### `FixedTimeComparison`

A static fixed-time byte comparison for a tag obtained outside `IKeyedHashProvider.Verify`, such as one computed incrementally with `CreateIncremental`.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `FixedTimeEquals(ReadOnlySpan<byte>, ReadOnlySpan<byte>)` | `bool` | Compare two byte sequences in a time that does not depend on their contents |

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

### `IRandomProvider`

A source of uniformly distributed random values. `NextBytes(Span<byte>)` is the only member an implementation must supply; a word-at-a-time generator should also declare `NextUInt32` and `NextUInt64`, which every other default draws through. Range-limited integer draws reject the values that would make the mapping uneven rather than folding them into the low end, so they carry no modulo bias. Thread safety is per implementation.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `NextBytes(Span<byte>)` | `void` | Fill a buffer with random bytes |
| `NextBytes(int)` | `byte[]` | Self-allocating variant of the above |
| `NextUInt32()` / `NextUInt64()` | `uint` / `ulong` | A full-width draw |
| `NextInt32()` | `int` | A value in [0, `int.MaxValue`) |
| `NextInt32(int)` | `int` | A value in [0, bound) |
| `NextInt32(int, int)` | `int` | A value in [min, max) |
| `NextInt64()`, `NextInt64(long)`, `NextInt64(long, long)` | `long` | The 64-bit counterparts |
| `NextDouble()` | `double` | A value in [0, 1) at 53-bit resolution |
| `NextDoubleExclusive()` | `double` | A value in (0, 1), for inverting a quantile function |
| `NextDouble(double, double)` | `double` | A value in [min, max) |
| `NextSingle()` | `float` | A value in [0, 1) |
| `NextBoolean()` | `bool` | True or false with equal probability |
| `NextBoolean(double)` | `bool` | A Bernoulli trial at the given probability |
| `Shuffle<T>(IList<T>)` | `void` | Shuffle in place, every ordering equally likely |
| `Choose<T>(IReadOnlyList<T>)` | `T` | One element, uniformly |
| `Choose<T>(IReadOnlyList<T>, IReadOnlyList<double>)` | `T` | One element, weighted |
| `Sample<T>(IReadOnlyList<T>, int)` | `IReadOnlyList<T>` | Distinct elements, without replacement |

### `IDistribution<T>`

A univariate probability distribution over `double` for a continuous family or `int` for a discrete one. An implementation supplies the CDF, the quantile, the support bounds and the first two moments; the rest is inherited. Instances are immutable, so one is safe to share across threads provided the `IRandomProvider` passed in at the call site is.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `Mean` / `Variance` / `StandardDeviation` | `double` | The first two moments |
| `Minimum` / `Maximum` | `T` | The support bounds, infinite where unbounded |
| `Cdf(T)` | `double` | P(X ≤ value) |
| `SurvivalFunction(T)` | `double` | P(X > value), declared directly where the tail matters |
| `Quantile(double)` | `T` | The inverse of the CDF |
| `Median` | `T` | `Quantile(0.5)` |
| `Sample(IRandomProvider)` | `T` | One draw; defaults to inverting the CDF |
| `Sample(IRandomProvider, Span<T>)` | `void` | Fill a buffer with independent draws |
| `Sample(IRandomProvider, int)` | `T[]` | Self-allocating variant of the above |

### `IContinuousDistribution`

`IDistribution<double>` plus a density. The density is a probability per unit of x, not a probability: it integrates to one and may exceed one where the distribution is narrow.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `Pdf(double)` | `double` | The probability density |
| `LogPdf(double)` | `double` | Its logarithm, declared directly where a closed form exists |

### `IDiscreteDistribution`

`IDistribution<int>` plus a mass function. Unlike a density, the mass at a point is a probability in its own right. The quantile has a default here — bisection over the CDF, at most 31 evaluations even over an unbounded support — so a discrete distribution need only supply its CDF.

| Name | Return Type | Description |
| ---- | ----------- | ----------- |
| `Pmf(int)` | `double` | The probability of exactly that outcome |
| `LogPmf(int)` | `double` | Its logarithm, declared directly where a closed form exists |

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.
