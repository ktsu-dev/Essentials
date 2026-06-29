# Consolidating `Abstractions` + `Common` into `Essentials`

**Date:** 2026-06-29
**Status:** Approved design — ready for implementation planning
**Repos affected:** `ktsu-dev/Essentials` (canonical), `ktsu-dev/Abstractions` (retire), `ktsu-dev/Common` (retire)

## Background

Three repos in the ktsu ecosystem cover the same ground:

- **`ktsu.Abstractions`** (v1.4.x) — the provider *interfaces* (`IHashProvider`, `ICompressionProvider`, …) plus ~34 bundled reference provider-implementation packages (`ktsu.<Category>.<Impl>`).
- **`ktsu.Common`** (v1.1.x) — ~36 additional provider-implementation packages that depend on `ktsu.Abstractions`. Has **no core package** of its own.
- **`ktsu.Essentials`** (v1.1.x) — a clone of the `Abstractions` repo (shared initial git history, same Aug 2025 seed commit). In Feb 2026 it absorbed Common's providers (`merge-common-providers` PR) and was renamed to `Essentials`, with the intent of becoming a single batteries-included package family (interfaces + implementations under `ktsu.Essentials.*`).

The Feb 2026 consolidation stalled: `Essentials` was published but never adopted (**0 in-tree dependents**), while `Abstractions` + `Common` kept all real development and the entire ecosystem's references (the only in-tree consumer of either is `Ecosystem`, which is itself abandoned). A cross-repo "Sync" bot kept re-applying identical maintenance commits to all repos, leaving `Abstractions` and `Essentials` near-identical twins. The only real code differences today: `Essentials` uses `ktsu.Essentials.*` namespaces and has a polished README; `Abstractions` retains two extra interfaces (`IConfigurationProvider`, `IObfuscationProvider`) that `Essentials` dropped.

The shared interface files are byte-identical modulo line endings and the namespace token — verified by normalized diff across all 17 common interface/helper files.

## Goal

Finish the original consolidation: make `ktsu.Essentials` the single canonical provider family — a faithful superset of `Abstractions` + `Common` — and retire the other two repos without losing capability for external consumers.

## Non-goals

- Migrating `Ecosystem` — it is abandoned and stays on the old packages.
- Source-compat shims for the ~70 leaf provider-implementation packages (interface-level compat only; see Retirement).
- Any change to the 17 already-identical interface/helper contracts beyond adding `IObfuscationProvider`.

## Design

### 1. Packaging — tiered (granular + meta)

Keep the existing model and add a convenience meta-package:

- `ktsu.Essentials` — core interfaces and helpers (unchanged).
- `ktsu.Essentials.<Category>.<Impl>` — one NuGet package per provider implementation (unchanged model).
- **New:** `ktsu.Essentials.All` — a meta-package with `PackageReference`s to every provider-implementation package, so consumers who want everything get one install while others continue to cherry-pick.

### 2. Reconciled taxonomy — compose, don't duplicate

Primitive categories remain first-class. Higher-level concepts are expressed as compositions of primitives rather than parallel duplicate categories.

#### Serialization (format primitive: object ↔ text)
- Keep: `Json` (System.Text.Json), `Yaml`, `Toml`.
- **Add:** port Common's `NewtonsoftJson` as an alternative JSON serializer → `ktsu.Essentials.SerializationProviders.NewtonsoftJson`. `Json` (System.Text.Json) remains the default/canonical JSON serializer; both implement `ISerializationProvider`.

#### Persistence (storage primitive — already composes a serializer)
- Keep: `FileSystem`, `AppData`, `Temp`, `InMemory`.
- `IPersistenceProvider<TKey>` stores/retrieves *typed* objects; e.g. `FileSystem<TKey>` is constructed with an `ISerializationProvider` + `IFileSystemProvider` and serializes on store / deserializes on retrieve. This is the "serialization + storage" aggregate.

#### Configuration — dropped, not ported
- `IConfigurationProvider` (Abstractions) is a text serialize/deserialize contract with no storage — a worse-named subset of `ISerializationProvider`.
- "Configuration" = `IPersistenceProvider<TKey>` over a serializer + a store. The JSON/YAML/TOML "config formats" are already serialization providers.
- **Action:** do **not** introduce `IConfigurationProvider` or `ConfigurationProviders/*` into `Essentials`. Common's `ConfigurationProviders/{Json,Toml,Yaml}` are not ported (their formats already exist as serialization providers).

#### Obfuscation — kept as a distinct intent, implemented by composition
- Keep `IObfuscationProvider` as a distinct, intent-revealing interface (obfuscation is explicitly *not* encryption). Port the interface from `Abstractions` into the `ktsu.Essentials` namespace verbatim (it already uses the shared `ProviderHelpers` patterns).
- Implementations are thin compositions over `IEncodingProvider` and simple reversible byte transforms — not copies of the Base64/Hex encoder code.

**Obfuscator implementations** (`ktsu.Essentials.ObfuscationProviders.<Impl>`):

| Impl | Behavior | Reversibility |
|---|---|---|
| `Base64` | Wraps the Base64 `IEncodingProvider`. | encoding is reversible |
| `Hex` | Wraps the Hex `IEncodingProvider`. | encoding is reversible |
| `Xor` | XOR each byte with a repeating key (configurable key bytes). | self-inverse with same key |
| `Caesar` | Add a configurable shift to each byte (mod 256). | deobfuscate subtracts the shift |
| `Reverse` | Reverse the byte sequence. | self-inverse |
| `BitRotate` | Rotate the bits of each byte by a configurable amount. | deobfuscate rotates the other way |
| `Composite` | Pipelines an ordered list of `IObfuscationProvider`s (e.g. `Xor` → `Base64`); deobfuscation applies them in reverse order. | composed of reversible steps |

All implement the core `TryObfuscate`/`TryDeobfuscate` (Span + Stream) methods; convenience and async members come from the interface's default implementations.

### 3. Retirement of `Abstractions` + `Common`

Compatibility at the interface level, clean-break deprecation for leaf implementation packages.

**`ktsu.Abstractions` core (interfaces):** publish a final release where every interface is `[Obsolete("Moved to ktsu.Essentials. This package is deprecated.")]` and **inherits** its `ktsu.Essentials` counterpart, e.g.:

```csharp
namespace ktsu.Abstractions;

[Obsolete("Moved to ktsu.Essentials. This package is deprecated.")]
public interface IHashProvider : ktsu.Essentials.IHashProvider { }
```

Existing external implementers (`class MyHasher : ktsu.Abstractions.IHashProvider`) keep compiling, and their instances are usable wherever a `ktsu.Essentials.IHashProvider` is expected (inheritance), giving a non-breaking migration path. Generic interfaces (`IPersistenceProvider<TKey>`, `ICacheProvider<TKey,TValue>`, `IValidationProvider<T>`, `INavigationProvider<T>`) shim the same way with their type parameters forwarded.

- **Exception — `IConfigurationProvider`:** no Essentials counterpart exists. Mark it `[Obsolete("Configuration is now persistence over a serializer; use ktsu.Essentials.IPersistenceProvider<TKey>.")]` with no base interface.
- **Exception — `IObfuscationProvider`:** shim it to inherit `ktsu.Essentials.IObfuscationProvider` like the others.

**Provider-implementation packages (~70 total: Abstractions-bundled `ktsu.<Category>.*` + all `ktsu.Common.*`):** no code shims. Mark each **Deprecated on NuGet** with a message naming its `ktsu.Essentials.<Category>.<Impl>` replacement. Retargeting is a one-line `PackageReference` + `using` change for consumers.

**Repos:** after the final deprecation release of each, archive `Abstractions` and `Common` on GitHub, and **remove both from the cross-repo Sync bot** configuration so they stop generating twin maintenance commits. `Essentials` remains on the Sync bot.

### 4. Tests

- Add a `ObfuscationProviderTests` suite covering round-trip (obfuscate → deobfuscate → original) for every obfuscator, including `Composite` ordering, across Span, Stream, string, and async paths.
- Add `NewtonsoftJson` coverage to the existing serialization tests.
- Do not port `ConfigurationProviderTests` (Configuration is dropped).
- Keep the rest of the existing `Essentials.Tests` suite as-is.

### 5. Documentation

Refresh `Essentials` docs via the `update-docs` skill:
- `README.md`, `CLAUDE.md`, `DESCRIPTION.md`, `TAGS.md` — document the composition model (Configuration = Persistence + Serialization; Obfuscation composes Encoding), the new obfuscators and `NewtonsoftJson` serializer, the `ktsu.Essentials.All` meta-package, and that `Essentials` supersedes `Abstractions` + `Common`.

## Work breakdown (high level)

1. Port `IObfuscationProvider` into `ktsu.Essentials`.
2. Implement the seven obfuscator packages + tests.
3. Port the `NewtonsoftJson` serializer package + tests.
4. Add the `ktsu.Essentials.All` meta-package.
5. Refresh Essentials docs.
6. `Abstractions`: convert core interfaces to `[Obsolete]` inheriting shims (with the two noted exceptions); final release; NuGet-deprecate the bundled provider packages.
7. `Common`: NuGet-deprecate all provider packages (no core to shim).
8. Remove `Abstractions` + `Common` from the cross-repo Sync bot; archive both repos.

(The detailed, sequenced implementation plan is produced by the writing-plans step.)

## Open questions

None outstanding — all design decisions resolved during brainstorming.
