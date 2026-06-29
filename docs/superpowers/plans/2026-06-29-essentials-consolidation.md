# Essentials Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ktsu.Essentials` a faithful superset of `ktsu.Abstractions` + `ktsu.Common` by adding obfuscation providers, the `NewtonsoftJson` serializer, and a `ktsu.Essentials.All` meta-package, so the other two repos can be retired.

**Architecture:** `ktsu.Essentials` is a core interfaces package plus one NuGet package per provider implementation. New providers are added as sibling projects under category folders, registered for DI in the test `ServiceCollectionExtensions`, and exercised by `[DynamicData]`-driven contract tests that enumerate every registered provider of an interface. Obfuscation is a distinct intent-revealing concept implemented by composing `IEncodingProvider` and simple reversible byte transforms (compose, don't duplicate).

**Tech Stack:** C# (multi-target `net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1`), `ktsu.Sdk` 2.13.0, MSTest.Sdk, Microsoft.Extensions.DependencyInjection, Polyfill, Newtonsoft.Json 13.0.4.

## Global Constraints

- Indentation: **tabs** in C# files. Line endings: **CRLF**.
- File-scoped namespaces; `using` directives **inside** the namespace where existing files do so (provider impl files put framework usings after the namespace — match the neighbouring file exactly).
- Every `.cs` file starts with the 3-line header:
  ```
  // Copyright (c) ktsu.dev
  // All rights reserved.
  // Licensed under the MIT license.
  ```
- Nullable reference types enabled; treat warnings as errors. No global suppressions — use targeted `[SuppressMessage]` with justification only if unavoidable.
- Interfaces live in namespace `ktsu.Essentials`. Provider implementations live in `ktsu.Essentials.<Category>` and their packages are `ktsu.Essentials.<Category>.<Impl>` (set via `AssemblyName`; `RootNamespace` is `ktsu.Essentials.<Category>`).
- Implementers provide ONLY the core `Try*` methods (Span + Stream variants); all convenience/async members come from default interface implementations.
- Provider classes are `public` and **not** `sealed` (they must remain inheritable for the Phase 2 compat shims).
- Each provider project references `..\..\Essentials\Essentials.csproj` and `Polyfill` (`PrivateAssets="All"`), and has `<InternalsVisibleTo Include="ktsu.Essentials.Tests" />`.
- New projects must be added to `Essentials.slnx` and referenced from `Essentials.Tests/Essentials.Tests.csproj`.
- Build/test commands run from the repo root `C:\dev\ktsu-dev\Essentials`. Tests target `net10.0` only.
- Work happens on branch `consolidate-into-essentials` (already created).

---

## File Structure

New files (Phase 1):

- `Essentials/Essentials/IObfuscationProvider.cs` — obfuscation interface (ported from Abstractions, re-namespaced).
- `Essentials/ObfuscationProviders/Xor/{Xor.csproj,Xor.cs}` — repeating-key XOR (self-inverse).
- `Essentials/ObfuscationProviders/Caesar/{Caesar.csproj,Caesar.cs}` — per-byte additive shift.
- `Essentials/ObfuscationProviders/Reverse/{Reverse.csproj,Reverse.cs}` — byte-order reversal (self-inverse).
- `Essentials/ObfuscationProviders/BitRotate/{BitRotate.csproj,BitRotate.cs}` — per-byte bit rotation.
- `Essentials/ObfuscationProviders/Base64/{Base64.csproj,Base64.cs}` — wraps the Base64 `IEncodingProvider`.
- `Essentials/ObfuscationProviders/Hex/{Hex.csproj,Hex.cs}` — wraps the Hex `IEncodingProvider`.
- `Essentials/ObfuscationProviders/Composite/{Composite.csproj,Composite.cs}` — pipelines an ordered list of obfuscators.
- `Essentials/SerializationProviders/NewtonsoftJson/{NewtonsoftJson.csproj,NewtonsoftJson.cs}` — Newtonsoft.Json serializer.
- `Essentials/All/All.csproj` — `ktsu.Essentials.All` meta-package (no code).
- `Essentials/Essentials.Tests/ObfuscationProviderTests.cs` — DynamicData contract tests for all obfuscators.

Modified files:

- `Essentials/Essentials.Tests/ServiceCollectionExtensions.cs` — add `AddObfuscationProviders`, register `NewtonsoftJson`.
- `Essentials/Essentials.Tests/Essentials.Tests.csproj` — add `ProjectReference`s to the 8 new provider projects.
- `Essentials/Directory.Packages.props` — add `Newtonsoft.Json` version pin.
- `Essentials/README.md`, `CLAUDE.md`, `DESCRIPTION.md`, `TAGS.md` — doc refresh (Task 11).

---

## Task 1: Port `IObfuscationProvider` into Essentials core

**Files:**
- Create: `Essentials/Essentials/IObfuscationProvider.cs`

**Interfaces:**
- Produces: `ktsu.Essentials.IObfuscationProvider` with core members an implementer must provide:
  - `bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)`
  - `bool TryObfuscate(Stream data, Stream destination)`
  - `bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)`
  - `bool TryDeobfuscate(Stream obfuscatedData, Stream destination)`
  - Plus default-implemented convenience/async members (`Obfuscate`/`Deobfuscate`/`*Async`) identical to the Abstractions version.

- [ ] **Step 1: Copy the interface and re-namespace it**

Copy `C:\dev\ktsu-dev\Abstractions\Abstractions\IObfuscationProvider.cs` to `Essentials/Essentials/IObfuscationProvider.cs`, changing **only** line 5 from `namespace ktsu.Abstractions;` to `namespace ktsu.Essentials;`. Everything else (the full member set, which already uses `ProviderHelpers.SpanToStreamBridge`, `ProviderHelpers.ExecuteToByteArray`, `ProviderHelpers.Utf8Transform`, `ProviderHelpers.RunAsync`) is unchanged — those helpers already exist in `Essentials/Essentials/ProviderHelpers.cs`.

- [ ] **Step 2: Build the core project to verify it compiles**

Run: `dotnet build Essentials/Essentials/Essentials.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Essentials/Essentials/IObfuscationProvider.cs
git commit -m "feat: add IObfuscationProvider interface to Essentials core"
```

---

## Task 2: `Xor` obfuscator + obfuscation test harness + DI wiring

This task establishes the obfuscator project pattern, the DI registration method, and the shared `[DynamicData]` round-trip test harness, using the simplest transform.

**Files:**
- Create: `Essentials/ObfuscationProviders/Xor/Xor.csproj`
- Create: `Essentials/ObfuscationProviders/Xor/Xor.cs`
- Create: `Essentials/Essentials.Tests/ObfuscationProviderTests.cs`
- Modify: `Essentials/Essentials.Tests/ServiceCollectionExtensions.cs`
- Modify: `Essentials/Essentials.Tests/Essentials.Tests.csproj`

**Interfaces:**
- Consumes: `ktsu.Essentials.IObfuscationProvider` (Task 1).
- Produces: `ktsu.Essentials.ObfuscationProviders.Xor` with a parameterless constructor (default key) and `Xor(byte[] key)`; `ServiceCollectionExtensions.AddObfuscationProviders(this ServiceCollection)`.

- [ ] **Step 1: Write the failing test (shared obfuscation harness)**

Create `Essentials/Essentials.Tests/ObfuscationProviderTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Text;
using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ObfuscationProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddObfuscationProviders();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> ObfuscationProviders => BuildProvider().EnumerateProviders<IObfuscationProvider>();

	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Roundtrip_Bytes(IObfuscationProvider provider, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("obfuscate me with " + providerName);

		byte[] obfuscated = provider.Obfuscate(original);
		byte[] restored = provider.Deobfuscate(obfuscated);

		CollectionAssert.AreEqual(original, restored, $"{providerName} should restore original bytes");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Roundtrip_Stream(IObfuscationProvider provider, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("stream obfuscate with " + providerName);

		using MemoryStream input = new(original);
		using MemoryStream obfuscated = new();
		Assert.IsTrue(provider.TryObfuscate(input, obfuscated), $"{providerName} should obfuscate stream");

		obfuscated.Position = 0;
		using MemoryStream restored = new();
		Assert.IsTrue(provider.TryDeobfuscate(obfuscated, restored), $"{providerName} should deobfuscate stream");

		CollectionAssert.AreEqual(original, restored.ToArray(), $"{providerName} should restore original from stream");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Roundtrip_String(IObfuscationProvider provider, string providerName)
	{
		string original = "string obfuscate with " + providerName;

		string obfuscated = provider.Obfuscate(original);
		byte[] obfuscatedBytes = Encoding.UTF8.GetBytes(obfuscated);
		byte[] restoredBytes = provider.Deobfuscate(obfuscatedBytes);
		string restored = Encoding.UTF8.GetString(restoredBytes);

		Assert.AreEqual(original, restored, $"{providerName} should restore original string");
	}

	[TestMethod]
	[DynamicData(nameof(ObfuscationProviders))]
	public void Obfuscation_Async_Roundtrip(IObfuscationProvider provider, string providerName)
	{
		byte[] original = Encoding.UTF8.GetBytes("async obfuscate with " + providerName);

		using MemoryStream input = new(original);
		using MemoryStream obfuscated = new();
		Assert.IsTrue(provider.TryObfuscateAsync(input, obfuscated, TestContext.CancellationToken).Result, $"{providerName} async obfuscate");

		obfuscated.Position = 0;
		using MemoryStream restored = new();
		Assert.IsTrue(provider.TryDeobfuscateAsync(obfuscated, restored, TestContext.CancellationToken).Result, $"{providerName} async deobfuscate");

		CollectionAssert.AreEqual(original, restored.ToArray(), $"{providerName} async should restore original");
	}
}
```

> Note: `Obfuscation_Roundtrip_String` relies on the obfuscated bytes being valid UTF-8. For `Xor`/`Caesar`/`BitRotate` the obfuscated bytes may not be valid UTF-8; the string convenience method uses `ProviderHelpers.Utf8Transform`, which round-trips through UTF-8 string⇄bytes. Because `Obfuscate(string)` returns a UTF-8 string built from the obfuscated bytes and `Deobfuscate` reverses it, the assertion holds for length-preserving transforms. If a specific transform fails this assertion in practice, keep the byte/stream/async tests and move the string assertion into a per-transform test for the encoding-based providers only — but attempt the shared version first.

- [ ] **Step 2: Add the `AddObfuscationProviders` registration**

In `Essentials/Essentials.Tests/ServiceCollectionExtensions.cs`, add `using ktsu.Essentials.ObfuscationProviders;` to the using block, add the call inside `AddCommon` (after `AddNavigationProviders();`), and add the new method:

```csharp
	public static ServiceCollection AddObfuscationProviders(this ServiceCollection services)
	{
		services.AddSingleton<IObfuscationProvider, Xor>();
		return services;
	}
```

And inside `AddCommon`, add the line:

```csharp
		services.AddObfuscationProviders();
```

- [ ] **Step 3: Create the Xor project file**

Create `Essentials/ObfuscationProviders/Xor/Xor.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1</TargetFrameworks>
    <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>
    <AssemblyName>ktsu.Essentials.ObfuscationProviders.Xor</AssemblyName>
    <RootNamespace>ktsu.Essentials.ObfuscationProviders</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Essentials\Essentials.csproj" />
    <PackageReference Include="Polyfill" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ktsu.Essentials.Tests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Implement `Xor`**

Create `Essentials/ObfuscationProviders/Xor/Xor.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that XORs each byte with a repeating key. Self-inverse: obfuscation and
/// deobfuscation are the same transform. This is NOT encryption and provides no confidentiality.
/// </summary>
public class Xor : IObfuscationProvider
{
	private readonly byte[] _key;

	/// <summary>Initializes a new instance with the default single-byte key.</summary>
	public Xor() : this([0x5A]) { }

	/// <summary>Initializes a new instance with the specified repeating key.</summary>
	/// <param name="key">The non-empty key bytes to XOR against.</param>
	public Xor(byte[] key)
	{
		Ensure.NotNull(key);
		if (key.Length == 0)
		{
			throw new ArgumentException("Key must contain at least one byte.", nameof(key));
		}

		_key = key;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < data.Length)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = (byte)(data[i] ^ _key[i % _key.Length]);
		}

		destination[data.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		int b;
		long i = 0;
		while ((b = data.ReadByte()) >= 0)
		{
			destination.WriteByte((byte)(b ^ _key[(int)(i % _key.Length)]));
			i++;
		}

		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
		=> TryObfuscate(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
		=> TryObfuscate(obfuscatedData, destination);
}
```

- [ ] **Step 5: Wire the project into the solution and test project**

Add a `ProjectReference` to `Essentials/Essentials.Tests/Essentials.Tests.csproj` (inside the second `<ItemGroup>` with the other provider refs):

```xml
    <ProjectReference Include="..\ObfuscationProviders\Xor\Xor.csproj" />
```

Add to the solution:

```bash
dotnet sln Essentials.slnx add Essentials/ObfuscationProviders/Xor/Xor.csproj
```

- [ ] **Step 6: Run the obfuscation tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: PASS — 4 tests (one per DynamicData row for the single `Xor` provider).

- [ ] **Step 7: Commit**

```bash
git add Essentials/ObfuscationProviders/Xor Essentials/Essentials.Tests/ObfuscationProviderTests.cs Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: add Xor obfuscation provider and obfuscation test harness"
```

---

## Task 3: `Caesar` obfuscator

**Files:**
- Create: `Essentials/ObfuscationProviders/Caesar/Caesar.csproj`
- Create: `Essentials/ObfuscationProviders/Caesar/Caesar.cs`
- Modify: `Essentials/Essentials.Tests/ServiceCollectionExtensions.cs`
- Modify: `Essentials/Essentials.Tests/Essentials.Tests.csproj`

**Interfaces:**
- Produces: `ktsu.Essentials.ObfuscationProviders.Caesar` with `Caesar()` (default shift 13) and `Caesar(byte shift)`.

- [ ] **Step 1: Register the provider (extend the failing harness)**

In `ServiceCollectionExtensions.cs`, add to `AddObfuscationProviders`:

```csharp
		services.AddSingleton<IObfuscationProvider, Caesar>();
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: FAIL — `Caesar` does not exist (compile error).

- [ ] **Step 3: Create the project file**

Create `Essentials/ObfuscationProviders/Caesar/Caesar.csproj` identical to Task 2 Step 3 but with `<AssemblyName>ktsu.Essentials.ObfuscationProviders.Caesar</AssemblyName>`.

- [ ] **Step 4: Implement `Caesar`**

Create `Essentials/ObfuscationProviders/Caesar/Caesar.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that adds a fixed shift to each byte (wrapping at 256). This is NOT
/// encryption and provides no confidentiality.
/// </summary>
public class Caesar : IObfuscationProvider
{
	private readonly byte _shift;

	/// <summary>Initializes a new instance with the default shift of 13.</summary>
	public Caesar() : this(13) { }

	/// <summary>Initializes a new instance with the specified shift.</summary>
	/// <param name="shift">The amount added to each byte when obfuscating.</param>
	public Caesar(byte shift) => _shift = shift;

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < data.Length)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = (byte)(data[i] + _shift);
		}

		destination[data.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		int b;
		while ((b = data.ReadByte()) >= 0)
		{
			destination.WriteByte((byte)(b + _shift));
		}

		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
	{
		if (destination.Length < obfuscatedData.Length)
		{
			return false;
		}

		for (int i = 0; i < obfuscatedData.Length; i++)
		{
			destination[i] = (byte)(obfuscatedData[i] - _shift);
		}

		destination[obfuscatedData.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
	{
		if (obfuscatedData is null || destination is null)
		{
			return false;
		}

		int b;
		while ((b = obfuscatedData.ReadByte()) >= 0)
		{
			destination.WriteByte((byte)(b - _shift));
		}

		return true;
	}
}
```

- [ ] **Step 5: Wire into solution and test project**

Add `<ProjectReference Include="..\ObfuscationProviders\Caesar\Caesar.csproj" />` to `Essentials.Tests.csproj`, then:

```bash
dotnet sln Essentials.slnx add Essentials/ObfuscationProviders/Caesar/Caesar.csproj
```

- [ ] **Step 6: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: PASS — 8 tests (Xor + Caesar).

- [ ] **Step 7: Commit**

```bash
git add Essentials/ObfuscationProviders/Caesar Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: add Caesar obfuscation provider"
```

---

## Task 4: `Reverse` obfuscator

**Files:**
- Create: `Essentials/ObfuscationProviders/Reverse/Reverse.csproj`
- Create: `Essentials/ObfuscationProviders/Reverse/Reverse.cs`
- Modify: `ServiceCollectionExtensions.cs`, `Essentials.Tests.csproj`

**Interfaces:**
- Produces: `ktsu.Essentials.ObfuscationProviders.Reverse` with a parameterless constructor.

- [ ] **Step 1: Register**

Add to `AddObfuscationProviders`: `services.AddSingleton<IObfuscationProvider, Reverse>();`

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: FAIL — `Reverse` does not exist.

- [ ] **Step 3: Create the project file**

Create `Essentials/ObfuscationProviders/Reverse/Reverse.csproj` as in Task 2 Step 3 with `<AssemblyName>ktsu.Essentials.ObfuscationProviders.Reverse</AssemblyName>`.

- [ ] **Step 4: Implement `Reverse`**

Create `Essentials/ObfuscationProviders/Reverse/Reverse.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that reverses the byte order. Self-inverse. This is NOT encryption and
/// provides no confidentiality.
/// </summary>
public class Reverse : IObfuscationProvider
{
	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < data.Length)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = data[data.Length - 1 - i];
		}

		destination[data.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		using MemoryStream buffer = new();
		data.CopyTo(buffer);
		byte[] bytes = buffer.ToArray();
		Array.Reverse(bytes);
		destination.Write(bytes, 0, bytes.Length);
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
		=> TryObfuscate(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
		=> TryObfuscate(obfuscatedData, destination);
}
```

- [ ] **Step 5: Wire into solution and test project**

Add `<ProjectReference Include="..\ObfuscationProviders\Reverse\Reverse.csproj" />` to `Essentials.Tests.csproj`, then:

```bash
dotnet sln Essentials.slnx add Essentials/ObfuscationProviders/Reverse/Reverse.csproj
```

- [ ] **Step 6: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: PASS — 12 tests.

- [ ] **Step 7: Commit**

```bash
git add Essentials/ObfuscationProviders/Reverse Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: add Reverse obfuscation provider"
```

---

## Task 5: `BitRotate` obfuscator

**Files:**
- Create: `Essentials/ObfuscationProviders/BitRotate/BitRotate.csproj`
- Create: `Essentials/ObfuscationProviders/BitRotate/BitRotate.cs`
- Modify: `ServiceCollectionExtensions.cs`, `Essentials.Tests.csproj`

**Interfaces:**
- Produces: `ktsu.Essentials.ObfuscationProviders.BitRotate` with `BitRotate()` (default rotate 3) and `BitRotate(int bits)` where `bits` is 1–7.

- [ ] **Step 1: Register**

Add to `AddObfuscationProviders`: `services.AddSingleton<IObfuscationProvider, BitRotate>();`

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: FAIL — `BitRotate` does not exist.

- [ ] **Step 3: Create the project file**

Create `Essentials/ObfuscationProviders/BitRotate/BitRotate.csproj` as in Task 2 Step 3 with `<AssemblyName>ktsu.Essentials.ObfuscationProviders.BitRotate</AssemblyName>`.

- [ ] **Step 4: Implement `BitRotate`**

Create `Essentials/ObfuscationProviders/BitRotate/BitRotate.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that rotates the bits of each byte left when obfuscating and right when
/// deobfuscating. This is NOT encryption and provides no confidentiality.
/// </summary>
public class BitRotate : IObfuscationProvider
{
	private readonly int _bits;

	/// <summary>Initializes a new instance rotating by 3 bits.</summary>
	public BitRotate() : this(3) { }

	/// <summary>Initializes a new instance rotating by the specified number of bits (1–7).</summary>
	/// <param name="bits">The number of bits to rotate; must be between 1 and 7 inclusive.</param>
	public BitRotate(int bits)
	{
		if (bits is < 1 or > 7)
		{
			throw new ArgumentOutOfRangeException(nameof(bits), bits, "Rotation must be between 1 and 7 bits.");
		}

		_bits = bits;
	}

	private static byte RotateLeft(byte value, int bits) => (byte)((value << bits) | (value >> (8 - bits)));

	private static byte RotateRight(byte value, int bits) => (byte)((value >> bits) | (value << (8 - bits)));

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < data.Length)
		{
			return false;
		}

		for (int i = 0; i < data.Length; i++)
		{
			destination[i] = RotateLeft(data[i], _bits);
		}

		destination[data.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		int b;
		while ((b = data.ReadByte()) >= 0)
		{
			destination.WriteByte(RotateLeft((byte)b, _bits));
		}

		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
	{
		if (destination.Length < obfuscatedData.Length)
		{
			return false;
		}

		for (int i = 0; i < obfuscatedData.Length; i++)
		{
			destination[i] = RotateRight(obfuscatedData[i], _bits);
		}

		destination[obfuscatedData.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
	{
		if (obfuscatedData is null || destination is null)
		{
			return false;
		}

		int b;
		while ((b = obfuscatedData.ReadByte()) >= 0)
		{
			destination.WriteByte(RotateRight((byte)b, _bits));
		}

		return true;
	}
}
```

- [ ] **Step 5: Wire into solution and test project**

Add `<ProjectReference Include="..\ObfuscationProviders\BitRotate\BitRotate.csproj" />` to `Essentials.Tests.csproj`, then:

```bash
dotnet sln Essentials.slnx add Essentials/ObfuscationProviders/BitRotate/BitRotate.csproj
```

- [ ] **Step 6: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: PASS — 16 tests.

- [ ] **Step 7: Commit**

```bash
git add Essentials/ObfuscationProviders/BitRotate Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: add BitRotate obfuscation provider"
```

---

## Task 6: `Base64` obfuscator (composes the Base64 encoder)

**Files:**
- Create: `Essentials/ObfuscationProviders/Base64/Base64.csproj`
- Create: `Essentials/ObfuscationProviders/Base64/Base64.cs`
- Modify: `ServiceCollectionExtensions.cs`, `Essentials.Tests.csproj`

**Interfaces:**
- Consumes: `ktsu.Essentials.IEncodingProvider`, `ktsu.Essentials.EncodingProviders.Base64` (existing encoder).
- Produces: `ktsu.Essentials.ObfuscationProviders.Base64` with `Base64()` and `Base64(IEncodingProvider encoder)`.

- [ ] **Step 1: Register**

In `ServiceCollectionExtensions.cs` add to `AddObfuscationProviders`:

```csharp
		services.AddSingleton<IObfuscationProvider, ObfuscationProviders.Base64>();
```

(Use the fully-qualified `ObfuscationProviders.Base64` because `EncodingProviders.Base64` is also referenced in this file.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: FAIL — `ObfuscationProviders.Base64` does not exist.

- [ ] **Step 3: Create the project file**

Create `Essentials/ObfuscationProviders/Base64/Base64.csproj` — like Task 2 Step 3, with `<AssemblyName>ktsu.Essentials.ObfuscationProviders.Base64</AssemblyName>` and an **extra** project reference to the encoder:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\Essentials\Essentials.csproj" />
    <ProjectReference Include="..\..\EncodingProviders\Base64\Base64.csproj" />
    <PackageReference Include="Polyfill" PrivateAssets="All" />
  </ItemGroup>
```

- [ ] **Step 4: Implement `Base64`**

Create `Essentials/ObfuscationProviders/Base64/Base64.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that composes a Base64 <see cref="IEncodingProvider"/>: obfuscation is
/// Base64 encoding, deobfuscation is Base64 decoding. This is NOT encryption.
/// </summary>
public class Base64 : IObfuscationProvider
{
	private readonly IEncodingProvider _encoder;

	/// <summary>Initializes a new instance using the default Base64 encoder.</summary>
	public Base64() : this(new EncodingProviders.Base64()) { }

	/// <summary>Initializes a new instance using the supplied encoder.</summary>
	/// <param name="encoder">The encoding provider used to perform the transform.</param>
	public Base64(IEncodingProvider encoder) => _encoder = Ensure.NotNull(encoder);

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination) => _encoder.TryDecode(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination) => _encoder.TryDecode(obfuscatedData, destination);
}
```

- [ ] **Step 5: Wire into solution and test project**

Add `<ProjectReference Include="..\ObfuscationProviders\Base64\Base64.csproj" />` to `Essentials.Tests.csproj`, then:

```bash
dotnet sln Essentials.slnx add Essentials/ObfuscationProviders/Base64/Base64.csproj
```

- [ ] **Step 6: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: PASS — 20 tests. If `Obfuscation_Roundtrip_String` fails for this provider only, that indicates a UTF-8 edge case; in that case keep the other 3 shared tests passing and move the string assertion into provider-specific tests as noted in Task 2 Step 1.

- [ ] **Step 7: Commit**

```bash
git add Essentials/ObfuscationProviders/Base64 Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: add Base64 obfuscation provider composing the Base64 encoder"
```

---

## Task 7: `Hex` obfuscator (composes the Hex encoder)

**Files:**
- Create: `Essentials/ObfuscationProviders/Hex/Hex.csproj`
- Create: `Essentials/ObfuscationProviders/Hex/Hex.cs`
- Modify: `ServiceCollectionExtensions.cs`, `Essentials.Tests.csproj`

**Interfaces:**
- Consumes: `ktsu.Essentials.EncodingProviders.Hex` (existing encoder).
- Produces: `ktsu.Essentials.ObfuscationProviders.Hex` with `Hex()` and `Hex(IEncodingProvider encoder)`.

- [ ] **Step 1: Register**

Add to `AddObfuscationProviders`: `services.AddSingleton<IObfuscationProvider, ObfuscationProviders.Hex>();`

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: FAIL — `ObfuscationProviders.Hex` does not exist.

- [ ] **Step 3: Create the project file**

Create `Essentials/ObfuscationProviders/Hex/Hex.csproj` like Task 6 Step 3 but `<AssemblyName>ktsu.Essentials.ObfuscationProviders.Hex</AssemblyName>` and the extra reference pointing to the Hex encoder:

```xml
    <ProjectReference Include="..\..\EncodingProviders\Hex\Hex.csproj" />
```

- [ ] **Step 4: Implement `Hex`**

Create `Essentials/ObfuscationProviders/Hex/Hex.cs` — identical body to Task 6 Step 4 but with class `Hex`, default ctor `Hex() : this(new EncodingProviders.Hex())`, and the summary referencing Hex:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// An obfuscation provider that composes a Hex <see cref="IEncodingProvider"/>: obfuscation is hex
/// encoding, deobfuscation is hex decoding. This is NOT encryption.
/// </summary>
public class Hex : IObfuscationProvider
{
	private readonly IEncodingProvider _encoder;

	/// <summary>Initializes a new instance using the default Hex encoder.</summary>
	public Hex() : this(new EncodingProviders.Hex()) { }

	/// <summary>Initializes a new instance using the supplied encoder.</summary>
	/// <param name="encoder">The encoding provider used to perform the transform.</param>
	public Hex(IEncodingProvider encoder) => _encoder = Ensure.NotNull(encoder);

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination) => _encoder.TryEncode(data, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination) => _encoder.TryDecode(obfuscatedData, destination);

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination) => _encoder.TryDecode(obfuscatedData, destination);
}
```

- [ ] **Step 5: Wire into solution and test project**

Add `<ProjectReference Include="..\ObfuscationProviders\Hex\Hex.csproj" />` to `Essentials.Tests.csproj`, then:

```bash
dotnet sln Essentials.slnx add Essentials/ObfuscationProviders/Hex/Hex.csproj
```

- [ ] **Step 6: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: PASS — 24 tests.

- [ ] **Step 7: Commit**

```bash
git add Essentials/ObfuscationProviders/Hex Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: add Hex obfuscation provider composing the Hex encoder"
```

---

## Task 8: `Composite` obfuscator (pipelines a chain)

**Files:**
- Create: `Essentials/ObfuscationProviders/Composite/Composite.csproj`
- Create: `Essentials/ObfuscationProviders/Composite/Composite.cs`
- Modify: `ServiceCollectionExtensions.cs`, `Essentials.Tests.csproj`

**Interfaces:**
- Consumes: `ktsu.Essentials.IObfuscationProvider`.
- Produces: `ktsu.Essentials.ObfuscationProviders.Composite` with `Composite(IReadOnlyList<IObfuscationProvider> stages)`. No parameterless constructor — it is configured by composition. Obfuscation applies stages in order; deobfuscation applies them in reverse order.

- [ ] **Step 1: Register via factory (a default Xor → BitRotate chain)**

In `ServiceCollectionExtensions.cs` add to `AddObfuscationProviders`:

```csharp
		services.AddSingleton<IObfuscationProvider>(_ => new Composite([new Xor(), new BitRotate()]));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: FAIL — `Composite` does not exist.

- [ ] **Step 3: Create the project file**

Create `Essentials/ObfuscationProviders/Composite/Composite.csproj` as in Task 2 Step 3 with `<AssemblyName>ktsu.Essentials.ObfuscationProviders.Composite</AssemblyName>`. It references only `Essentials.csproj` + `Polyfill` — the chain stages are supplied by the caller, so no references to sibling obfuscator projects are needed.

- [ ] **Step 4: Implement `Composite`**

Create `Essentials/ObfuscationProviders/Composite/Composite.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.ObfuscationProviders;

using ktsu.Essentials;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// An obfuscation provider that pipelines an ordered list of obfuscators. Obfuscation applies the
/// stages in order; deobfuscation applies them in reverse order. This is NOT encryption.
/// </summary>
public class Composite : IObfuscationProvider
{
	private readonly IReadOnlyList<IObfuscationProvider> _stages;

	/// <summary>Initializes a new instance with the ordered obfuscation stages.</summary>
	/// <param name="stages">The non-empty ordered list of obfuscators to pipeline.</param>
	public Composite(IReadOnlyList<IObfuscationProvider> stages)
	{
		Ensure.NotNull(stages);
		if (stages.Count == 0)
		{
			throw new ArgumentException("At least one stage is required.", nameof(stages));
		}

		_stages = stages;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		byte[] current = data.ToArray();
		foreach (IObfuscationProvider stage in _stages)
		{
			current = stage.Obfuscate(current);
		}

		if (destination.Length < current.Length)
		{
			return false;
		}

		current.CopyTo(destination);
		destination[current.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryObfuscate(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		using MemoryStream buffer = new();
		data.CopyTo(buffer);
		byte[] current = buffer.ToArray();
		foreach (IObfuscationProvider stage in _stages)
		{
			current = stage.Obfuscate(current);
		}

		destination.Write(current, 0, current.Length);
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(ReadOnlySpan<byte> obfuscatedData, Span<byte> destination)
	{
		byte[] current = obfuscatedData.ToArray();
		for (int i = _stages.Count - 1; i >= 0; i--)
		{
			current = _stages[i].Deobfuscate(current);
		}

		if (destination.Length < current.Length)
		{
			return false;
		}

		current.CopyTo(destination);
		destination[current.Length..].Clear();
		return true;
	}

	/// <inheritdoc/>
	public bool TryDeobfuscate(Stream obfuscatedData, Stream destination)
	{
		if (obfuscatedData is null || destination is null)
		{
			return false;
		}

		using MemoryStream buffer = new();
		obfuscatedData.CopyTo(buffer);
		byte[] current = buffer.ToArray();
		for (int i = _stages.Count - 1; i >= 0; i--)
		{
			current = _stages[i].Deobfuscate(current);
		}

		destination.Write(current, 0, current.Length);
		return true;
	}
}
```

- [ ] **Step 5: Wire into solution and test project**

Add `<ProjectReference Include="..\ObfuscationProviders\Composite\Composite.csproj" />` to `Essentials.Tests.csproj`, then:

```bash
dotnet sln Essentials.slnx add Essentials/ObfuscationProviders/Composite/Composite.csproj
```

- [ ] **Step 6: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~ObfuscationProviderTests"`
Expected: PASS — 28 tests (Xor, Caesar, Reverse, BitRotate, Base64, Hex, Composite).

- [ ] **Step 7: Commit**

```bash
git add Essentials/ObfuscationProviders/Composite Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: add Composite obfuscation provider that pipelines a chain"
```

---

## Task 9: Port the `NewtonsoftJson` serialization provider

**Files:**
- Create: `Essentials/SerializationProviders/NewtonsoftJson/NewtonsoftJson.csproj`
- Create: `Essentials/SerializationProviders/NewtonsoftJson/NewtonsoftJson.cs`
- Modify: `Essentials/Directory.Packages.props`
- Modify: `ServiceCollectionExtensions.cs`, `Essentials.Tests.csproj`

**Interfaces:**
- Consumes: `ktsu.Essentials.ISerializationProvider` (core: `bool TrySerialize(object obj, TextWriter writer)`, `T? Deserialize<T>(ReadOnlySpan<byte> data)`).
- Produces: `ktsu.Essentials.SerializationProviders.NewtonsoftJson` (parameterless constructor).

- [ ] **Step 1: Add the package version pin**

In `Essentials/Directory.Packages.props`, add inside the `<ItemGroup>`:

```xml
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
```

- [ ] **Step 2: Register the provider (extend the failing serialization suite)**

In `ServiceCollectionExtensions.cs`, in `AddSerializationProviders`, add:

```csharp
		services.AddSingleton<ISerializationProvider, NewtonsoftJson>();
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~SerializationProviderTests"`
Expected: FAIL — `NewtonsoftJson` does not exist.

- [ ] **Step 4: Create the project file**

Create `Essentials/SerializationProviders/NewtonsoftJson/NewtonsoftJson.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1</TargetFrameworks>
    <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>
    <AssemblyName>ktsu.Essentials.SerializationProviders.NewtonsoftJson</AssemblyName>
    <RootNamespace>ktsu.Essentials.SerializationProviders</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Essentials\Essentials.csproj" />
    <PackageReference Include="Newtonsoft.Json" />
    <PackageReference Include="Polyfill" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ktsu.Essentials.Tests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Implement `NewtonsoftJson`**

Copy `C:\dev\ktsu-dev\Common\SerializationProviders\NewtonsoftJson\NewtonsoftJson.cs` to the new path, changing line 5 from `namespace ktsu.SerializationProviders;` to `namespace ktsu.Essentials.SerializationProviders;` and line 10 from `using ktsu.Abstractions;` to `using ktsu.Essentials;`. The class body is unchanged. Result:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.SerializationProviders;

using System;
using System.IO;
using System.Text;
using ktsu.Essentials;
using Newtonsoft.Json;

/// <summary>
/// A serialization provider that uses Newtonsoft.Json for JSON serialization and deserialization.
/// </summary>
public class NewtonsoftJson : ISerializationProvider
{
	private readonly JsonSerializerSettings settings = new();

	/// <inheritdoc/>
	public T? Deserialize<T>(ReadOnlySpan<byte> data)
	{
		if (data.IsEmpty)
		{
			return default;
		}

		try
		{
			string jsonString = Encoding.UTF8.GetString(data);
			return JsonConvert.DeserializeObject<T>(jsonString, settings);
		}
		catch (JsonReaderException)
		{
			return default;
		}
		catch (ArgumentException)
		{
			return default;
		}
	}

	/// <inheritdoc/>
	public bool TrySerialize(object obj, TextWriter writer)
	{
		if (writer is null)
		{
			return false;
		}

		try
		{
			using JsonTextWriter jsonWriter = new(writer);
			JsonSerializer serializer = JsonSerializer.Create(settings);
			serializer.Serialize(jsonWriter, obj);
			return true;
		}
		catch (JsonSerializationException)
		{
			return false;
		}
		catch (JsonWriterException)
		{
			return false;
		}
	}
}
```

- [ ] **Step 6: Wire into solution and test project**

Add `<ProjectReference Include="..\SerializationProviders\NewtonsoftJson\NewtonsoftJson.csproj" />` to `Essentials.Tests.csproj`, then:

```bash
dotnet sln Essentials.slnx add Essentials/SerializationProviders/NewtonsoftJson/NewtonsoftJson.csproj
```

- [ ] **Step 7: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~SerializationProviderTests"`
Expected: PASS — the existing serialization contract tests now also run against `NewtonsoftJson`.

- [ ] **Step 8: Commit**

```bash
git add Essentials/SerializationProviders/NewtonsoftJson Essentials/Directory.Packages.props Essentials/Essentials.Tests/ServiceCollectionExtensions.cs Essentials/Essentials.Tests/Essentials.Tests.csproj Essentials.slnx
git commit -m "feat: port NewtonsoftJson serialization provider from Common"
```

---

## Task 10: `ktsu.Essentials.All` meta-package

**Files:**
- Create: `Essentials/All/All.csproj`

**Interfaces:**
- Produces: NuGet package `ktsu.Essentials.All` whose only content is `PackageReference`-equivalent dependencies on every `ktsu.Essentials.*` provider package (emitted from `ProjectReference`s when packed). No assembly output.

- [ ] **Step 1: Create the meta-package project**

Create `Essentials/All/All.csproj`. List a `ProjectReference` for **every** provider implementation project in the repo (all `CacheProviders`, `CommandExecutors`, `CompressionProviders`, `EncodingProviders`, `EncryptionProviders`, `FileSystemProviders`, `HashProviders`, `LoggingProviders`, `NavigationProviders`, `ObfuscationProviders`, `PersistenceProviders`, `SerializationProviders` leaf projects). Use the test project's `ProjectReference` list as the source of truth and add the 8 new providers (7 obfuscators + NewtonsoftJson):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0;net7.0;net6.0;netstandard2.1</TargetFrameworks>
    <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>
    <AssemblyName>ktsu.Essentials.All</AssemblyName>
    <RootNamespace>ktsu.Essentials.All</RootNamespace>
    <IncludeBuildOutput>false</IncludeBuildOutput>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\CacheProviders\InMemory\InMemory.csproj" />
    <ProjectReference Include="..\CommandExecutors\Native\Native.csproj" />
    <ProjectReference Include="..\CompressionProviders\Gzip\Gzip.csproj" />
    <ProjectReference Include="..\CompressionProviders\Brotli\Brotli.csproj" />
    <ProjectReference Include="..\CompressionProviders\Deflate\Deflate.csproj" />
    <ProjectReference Include="..\CompressionProviders\ZLib\ZLib.csproj" />
    <ProjectReference Include="..\EncodingProviders\Base64\Base64.csproj" />
    <ProjectReference Include="..\EncodingProviders\Hex\Hex.csproj" />
    <ProjectReference Include="..\EncryptionProviders\Aes\Aes.csproj" />
    <ProjectReference Include="..\FileSystemProviders\Native\Native.csproj" />
    <ProjectReference Include="..\HashProviders\MD5\MD5.csproj" />
    <ProjectReference Include="..\HashProviders\SHA1\SHA1.csproj" />
    <ProjectReference Include="..\HashProviders\SHA256\SHA256.csproj" />
    <ProjectReference Include="..\HashProviders\SHA384\SHA384.csproj" />
    <ProjectReference Include="..\HashProviders\SHA512\SHA512.csproj" />
    <ProjectReference Include="..\HashProviders\FNV1_32\FNV1_32.csproj" />
    <ProjectReference Include="..\HashProviders\FNV1a_32\FNV1a_32.csproj" />
    <ProjectReference Include="..\HashProviders\FNV1_64\FNV1_64.csproj" />
    <ProjectReference Include="..\HashProviders\FNV1a_64\FNV1a_64.csproj" />
    <ProjectReference Include="..\HashProviders\CRC32\CRC32.csproj" />
    <ProjectReference Include="..\HashProviders\CRC64\CRC64.csproj" />
    <ProjectReference Include="..\HashProviders\XxHash32\XxHash32.csproj" />
    <ProjectReference Include="..\HashProviders\XxHash64\XxHash64.csproj" />
    <ProjectReference Include="..\HashProviders\XxHash3\XxHash3.csproj" />
    <ProjectReference Include="..\HashProviders\XxHash128\XxHash128.csproj" />
    <ProjectReference Include="..\LoggingProviders\Console\Console.csproj" />
    <ProjectReference Include="..\NavigationProviders\InMemory\InMemory.csproj" />
    <ProjectReference Include="..\ObfuscationProviders\Xor\Xor.csproj" />
    <ProjectReference Include="..\ObfuscationProviders\Caesar\Caesar.csproj" />
    <ProjectReference Include="..\ObfuscationProviders\Reverse\Reverse.csproj" />
    <ProjectReference Include="..\ObfuscationProviders\BitRotate\BitRotate.csproj" />
    <ProjectReference Include="..\ObfuscationProviders\Base64\Base64.csproj" />
    <ProjectReference Include="..\ObfuscationProviders\Hex\Hex.csproj" />
    <ProjectReference Include="..\ObfuscationProviders\Composite\Composite.csproj" />
    <ProjectReference Include="..\PersistenceProviders\AppData\AppData.csproj" />
    <ProjectReference Include="..\PersistenceProviders\FileSystem\FileSystem.csproj" />
    <ProjectReference Include="..\PersistenceProviders\InMemory\InMemory.csproj" />
    <ProjectReference Include="..\PersistenceProviders\Temp\Temp.csproj" />
    <ProjectReference Include="..\SerializationProviders\Json\Json.csproj" />
    <ProjectReference Include="..\SerializationProviders\Yaml\Yaml.csproj" />
    <ProjectReference Include="..\SerializationProviders\Toml\Toml.csproj" />
    <ProjectReference Include="..\SerializationProviders\NewtonsoftJson\NewtonsoftJson.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add to the solution and verify it packs with dependencies (no lib output)**

```bash
dotnet sln Essentials.slnx add Essentials/All/All.csproj
dotnet pack Essentials/All/All.csproj -o ./staging
```
Expected: `ktsu.Essentials.All.<version>.nupkg` is produced. Inspect that the nuspec lists every provider package as a `<dependency>` and the package contains no `lib/*/ktsu.Essentials.All.dll`.

> If `ProjectReference`s do not surface as package dependencies under `ktsu.Sdk` packing, convert them to `PackageReference`s to the published `ktsu.Essentials.<Category>.<Impl>` packages (add each version to `Directory.Packages.props`). Verify the produced nuspec either way.

- [ ] **Step 3: Build the whole solution to confirm nothing regressed**

Run: `dotnet build Essentials.slnx`
Expected: Build succeeded, 0 warnings (warnings-as-errors), 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Essentials/All/All.csproj Essentials.slnx
git commit -m "feat: add ktsu.Essentials.All meta-package"
```

---

## Task 11: Refresh Essentials documentation

**Files:**
- Modify: `Essentials/README.md`, `Essentials/CLAUDE.md`, `Essentials/DESCRIPTION.md`, `Essentials/TAGS.md`

- [ ] **Step 1: Run the docs skill**

Invoke the `update-docs` skill for the `Essentials` repo. Ensure the refreshed docs state:
- The composition model: **Configuration = Persistence + Serialization** (no separate configuration providers); **Obfuscation composes Encoding**.
- New `ObfuscationProviders`: `Xor`, `Caesar`, `Reverse`, `BitRotate`, `Base64`, `Hex`, `Composite`, plus the `IObfuscationProvider` interface (obfuscation is explicitly **not** encryption).
- New `SerializationProviders/NewtonsoftJson` (System.Text.Json `Json` remains the default).
- The new `ktsu.Essentials.All` meta-package and the cherry-pick model.
- That `ktsu.Essentials` supersedes `ktsu.Abstractions` and `ktsu.Common`.

- [ ] **Step 2: Full verification build + test**

Run: `dotnet build Essentials.slnx && dotnet test`
Expected: Build succeeded; all tests pass (including the 28 obfuscation tests and the serialization tests covering `NewtonsoftJson`).

- [ ] **Step 3: Commit**

```bash
git add Essentials/README.md Essentials/CLAUDE.md Essentials/DESCRIPTION.md Essentials/TAGS.md
git commit -m "docs: document obfuscation, NewtonsoftJson, the All meta-package, and supersession"
```

---

## Phase 1 done → ship

After Task 11, open a PR from `consolidate-into-essentials` to `main`, merge, and let CI publish the new `ktsu.Essentials.*` packages (including `ktsu.Essentials.All`). Phase 2 cannot begin until the new packages are live on nuget.org (the Abstractions shims inherit from them).

---

## Phase 2: Retirement of `Abstractions` + `Common` (separate plan)

These are release/operations tasks that produce no testable software and depend on Phase 1 being published. They will be expanded into their own plan (`docs/superpowers/plans/<date>-retire-abstractions-common.md`) once Phase 1 ships. Captured here for traceability against the spec:

1. **`Abstractions` core compat shim release.** In `ktsu.Abstractions`, change each interface to inherit its `ktsu.Essentials` counterpart and mark it `[Obsolete("Moved to ktsu.Essentials. This package is deprecated.")]`, e.g. `public interface IHashProvider : ktsu.Essentials.IHashProvider { }`. Add a `PackageReference` to `ktsu.Essentials`. Interfaces to shim (with generic arity): `ICacheProvider<TKey,TValue>`, `ICommandExecutor`, `ICompressionProvider`, `IEncodingProvider`, `IEncryptionProvider`, `IFileSystemProvider`, `IHashProvider`, `ILoggingProvider`, `INavigationProvider<T>`, `IObfuscationProvider`, `IPersistenceProvider<TKey>`, `ISerializationOptions`, `ISerializationProvider`, `IValidationProvider<T>`. **Exception:** `IConfigurationProvider` has no Essentials counterpart — mark `[Obsolete("Configuration is now persistence over a serializer; use ktsu.Essentials.IPersistenceProvider<TKey>.")]` with no base interface. Publish as a final major-version release.
2. **NuGet-deprecate the provider-impl packages.** Mark every published `ktsu.Abstractions`-repo provider package (`ktsu.<Category>.<Impl>`) and every `ktsu.Common.*` package as Deprecated on nuget.org with `AlternatePackage` = the matching `ktsu.Essentials.<Category>.<Impl>` (via the nuget.org UI or the registration/deprecation API). Enumerate the package IDs from each repo's provider folders.
3. **Remove from the Sync bot.** Remove `Abstractions` and `Common` from the cross-repo Sync bot configuration so they stop receiving twin maintenance commits.
4. **Archive the repos.** Archive `ktsu-dev/Abstractions` and `ktsu-dev/Common` on GitHub after their final releases.

(`Ecosystem` is abandoned and intentionally not migrated.)

---

## Self-Review

**Spec coverage:**
- Tiered packaging (granular + `All` meta) → Tasks 2–9 keep granular packages; Task 10 adds the meta. ✅
- Configuration dropped → no task ports it; called out in docs (Task 11) and Phase 2 shim exception. ✅
- Obfuscation kept, composed over encoding, with the 7-obfuscator set → Tasks 1–8. ✅
- NewtonsoftJson ported → Task 9. ✅
- Interface shims (Q4=B) + impl deprecation (Q5=A), Sync-bot removal, archival → Phase 2. ✅
- Tests for obfuscators + NewtonsoftJson coverage; no Configuration tests → Tasks 2–9. ✅
- Docs refresh → Task 11. ✅

**Placeholder scan:** No TBD/TODO; every code step shows complete code; csproj/DI edits are explicit. The two conditional notes (string-roundtrip fallback in Task 6; ProjectReference-vs-PackageReference packing in Task 10) give a concrete primary action plus a concrete fallback, not a placeholder. ✅

**Type consistency:** Core members `TryObfuscate`/`TryDeobfuscate` (Span + Stream) and convenience `Obfuscate`/`Deobfuscate` match `IObfuscationProvider` (Task 1). `AddObfuscationProviders` defined in Task 2 and extended in Tasks 3–8. `NewtonsoftJson` implements `TrySerialize(object, TextWriter)` + `Deserialize<T>(ReadOnlySpan<byte>)` matching `ISerializationProvider`. `Composite(IReadOnlyList<IObfuscationProvider>)` constructor used consistently in the Task 8 DI factory. ✅
