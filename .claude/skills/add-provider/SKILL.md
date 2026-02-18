---
name: add-provider
description: Scaffold a new provider interface in Abstractions and implementation in Common following the three-tier pattern
disable-model-invocation: true
---

# Add Provider

Scaffold a new provider interface and implementation in the ktsu ecosystem. This follows the established three-tier provider pattern used across the codebase.

## Required Information

Before starting, gather from the user:
1. **Provider category** (e.g., "Hash", "Compression", "Encryption", "Obfuscation", "Serialization", "FileSystem", or a new category)
2. **Provider name** (e.g., "SHA256", "Gzip", "Aes")
3. **Core operations** - what Try* methods the interface needs
4. **Whether a new interface is needed** or if implementing an existing one from Abstractions

## Step 1: Understand the Existing Pattern

Read the relevant interface from `Abstractions/Abstractions/` to understand the contract:
- `IHashProvider.cs` - hash operations with `HashLengthBytes` property
- `ICompressionProvider.cs` - compress/decompress with Span and Stream overloads
- `IEncryptionProvider.cs` - encrypt/decrypt with key and IV parameters
- `IObfuscationProvider.cs` - obfuscate/deobfuscate (non-crypto)
- `ISerializationProvider.cs` - serialize/deserialize with TextWriter/TextReader
- `IFileSystemProvider.cs` - filesystem abstraction

If creating a **new interface**, follow the three-tier pattern:

### Tier 1: Core Try* methods (implementer provides)
```csharp
bool TryOperation(ReadOnlySpan<byte> data, Span<byte> destination);
bool TryOperation(Stream data, Stream destination);
```

### Tier 2: Convenience methods (default implementations)
```csharp
byte[] Operation(ReadOnlySpan<byte> data)  // allocates and returns
byte[] Operation(Stream data)              // allocates and returns
string Operation(string data)              // UTF8 encoding wrapper
```

### Tier 3: Async methods (default implementations)
```csharp
Task<bool> TryOperationAsync(ReadOnlyMemory<byte> data, Memory<byte> destination, CancellationToken ct = default)
Task<byte[]> OperationAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
```

## Step 2: Create the Interface (if new)

**File**: `Abstractions/Abstractions/I{Category}Provider.cs`

Follow the established conventions:
- Copyright header: `// Copyright (c) ktsu.dev`
- Namespace: `ktsu.Abstractions`
- Full XML documentation on all members
- Default implementations for convenience and async methods
- Async pattern: check `cancellationToken.IsCancellationRequested` then `Task.FromCanceled` or `Task.Run`
- Convenience methods throw `InvalidOperationException` on failure

## Step 3: Create the Implementation

**Directory**: `Common/{Category}Providers/{Name}/`

Create these files:

### {Name}.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1</TargetFrameworks>
    <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ktsu.Abstractions" />
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />
    <PackageReference Include="Microsoft.SourceLink.AzureRepos.Git" PrivateAssets="All" />
    <PackageReference Include="Polyfill" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ktsu.Common.Tests" />
  </ItemGroup>
</Project>
```

### {Name}.cs
```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.{Category}Providers;

using System;
using System.IO;
using ktsu.Abstractions;

/// <summary>
/// A {category} provider that uses {Name} for {operation} data.
/// </summary>
public class {Name} : I{Category}Provider
{
    // Implement only the core Try* methods
    // All convenience and async methods are inherited via default implementations
}
```

### GlobalSuppressions.cs (if needed for netstandard2.1 compatibility)

## Step 4: Add to Common.sln

```bash
cd Common
dotnet sln add {Category}Providers/{Name}/{Name}.csproj
```

## Step 5: Build and Verify

```bash
cd Common
dotnet build
```

Verify the implementation compiles across all target frameworks.

## Step 6: Add Tests

Add test methods to `Common/Common.Tests/` following the existing test patterns. Test both the core Try* methods and the inherited convenience methods.

## Validation Checklist

- [ ] Interface follows the three-tier pattern (Try* core, convenience, async)
- [ ] Implementation only provides core Try* methods
- [ ] XML documentation is complete
- [ ] Copyright header is present
- [ ] Namespace follows `ktsu.{Category}Providers` convention
- [ ] .csproj references ktsu.Abstractions and includes ktsu.Sdk
- [ ] Multi-targeting includes all 6 frameworks
- [ ] InternalsVisibleTo includes ktsu.Common.Tests
- [ ] Project added to Common.sln
- [ ] Builds successfully across all target frameworks
- [ ] Tests pass
