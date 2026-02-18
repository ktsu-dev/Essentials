---
name: cross-repo-update
description: Identify and plan updates needed across the ktsu monorepo when an interface or shared component changes
disable-model-invocation: true
---

# Cross-Repo Update

When an interface in Abstractions or a shared component changes, identify all impacted projects across the ktsu monorepo and plan the necessary updates.

## Required Information

Gather from the user:
1. **What changed** - which interface, class, or component was modified
2. **Nature of the change** - breaking (removal/rename/signature change) vs additive (new method with default implementation)
3. **The specific project** - typically Abstractions, Common, or Sdk

## Step 1: Identify the Changed Symbol

Read the modified interface or component to understand the exact change. Determine:
- Was a method added, removed, or renamed?
- Did a method signature change?
- Is there a default implementation (non-breaking) or must implementers update?
- Were any types changed (e.g., parameter types, return types)?

## Step 2: Find All Implementations

Search the monorepo for projects that implement or reference the changed interface.

```bash
# Find all projects that reference ktsu.Abstractions
grep -rl "ktsu.Abstractions" c:/dev/ktsu-dev/*/Directory.Packages.props c:/dev/ktsu-dev/*/*/*.csproj 2>/dev/null

# Find all implementations of a specific interface
grep -rl "I{InterfaceName}" c:/dev/ktsu-dev/Common/ --include="*.cs" 2>/dev/null
```

For the Common providers specifically:
- **IHashProvider**: `Common/HashProviders/` - MD5, SHA1, SHA256, SHA384, SHA512, FNV1_32, FNV1a_32, FNV1_64, FNV1a_64, CRC32, CRC64, XxHash32, XxHash64, XxHash3, XxHash128
- **ICompressionProvider**: `Common/CompressionProviders/` - Gzip, Brotli, Deflate, ZLib
- **IEncryptionProvider**: `Common/EncryptionProviders/` - Aes
- **IObfuscationProvider**: `Common/ObfuscationProviders/` - Base64, Hex
- **ISerializationProvider**: `Common/SerializationProviders/` - SystemTextJson, NewtonsoftJson
- **IFileSystemProvider**: `Common/FileSystemProviders/` - Native

## Step 3: Assess Impact

For each impacted project, determine:
- **Must update**: Implementation is broken by the change (removed/renamed method, changed signature)
- **Should update**: New functionality available but existing code still compiles (new method with default impl)
- **No action needed**: Change is fully backward-compatible via default implementations

## Step 4: Generate Update Plan

Create a prioritized list of changes:

1. **Abstractions** - The source of the change (already done)
2. **Common implementations** - Direct implementers in Common/
3. **Consumer libraries** - Projects that call the changed methods
4. **Downstream dependents** - Projects that depend on consumer libraries

For each project, specify:
- File(s) to modify
- Exact changes needed
- Whether tests need updating
- Version bump recommendation (`[major]` for breaking, `[minor]` for additive)

## Step 5: Execute Updates

Work through the plan project by project:

1. Make the code changes
2. Build to verify compilation: `cd <project> && dotnet build`
3. Run tests: `cd <project> && dotnet test`
4. Note version bump needed in commit message

## Breaking Change Checklist

- [ ] All implementations in Common/ updated
- [ ] All consumer libraries updated
- [ ] All test projects updated
- [ ] Each project builds across all target frameworks
- [ ] All tests pass
- [ ] Commit messages include appropriate version markers (`[major]`/`[minor]`/`[patch]`)
- [ ] CLAUDE.md updated if architectural patterns changed

## Additive Change Checklist

- [ ] Default implementation provided in interface (no implementer changes needed)
- [ ] Convenience and async variants included via defaults
- [ ] Tests added for the new functionality
- [ ] Commit message includes `[minor]` version marker
