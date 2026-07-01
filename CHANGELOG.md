## v1.2.1 (patch)

Changes since v1.2.0:

- [patch] fix(packaging): re-enable package validation on ktsu.Sdk 2.13.2 ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.2.0 (minor)

Changes since v1.1.0:

- Merge origin/main into consolidate-into-essentials ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: document obfuscation, NewtonsoftJson, All meta-package, and new naming convention ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add ktsu.Essentials.All meta-package ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: port NewtonsoftJson serialization provider from Common ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: conform all providers to SDK naming convention ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: conform serialization providers to SDK naming convention ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add Composite obfuscation provider that pipelines a chain ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] fix(core): disable strict ApiCompat package validation ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add Hex obfuscation provider composing the Hex encoder ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] fix(providers): derive package ids and disable strict ApiCompat ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: keep Base64 obfuscator encoder ctor public; register via factory to avoid DI greedy-ctor ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add Base64 obfuscation provider composing the Base64 encoder ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add BitRotate obfuscation provider ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add Reverse obfuscation provider ([@matt-edmondson](https://github.com/matt-edmondson))
- test: scope obfuscation string round-trip out of the shared byte-transform harness ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add Caesar obfuscation provider ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add Xor obfuscation provider and obfuscation test harness ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add IObfuscationProvider interface to Essentials core ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: implementation plan for Essentials consolidation (Phase 1) ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: design spec for consolidating Abstractions + Common into Essentials ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Pin Testably.Abstractions.FileSystem.Interface to stable 10.0.0 ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: remove unused SourceLink package versions ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: simplify package references and drop redundant SourceLink deps ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove stale files ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.3 (patch)

Changes since v1.1.2:

- [patch] fix(core): disable strict ApiCompat package validation ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] fix(providers): derive package ids and disable strict ApiCompat ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.2 (patch)

Changes since v1.1.1:

- [patch] Pin Testably.Abstractions.FileSystem.Interface to stable 10.0.0 ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.1 (patch)

Changes since v1.1.0:

- chore: remove unused SourceLink package versions ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: simplify package references and drop redundant SourceLink deps ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove stale files ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.0 (major)

- Update documentation to reflect changes in hash provider implementations ([@matt-edmondson](https://github.com/matt-edmondson))
- Rename to Essentials ([@matt-edmondson](https://github.com/matt-edmondson))
- Add persistence providers: AppData, FileSystem, and Temp ([@matt-edmondson](https://github.com/matt-edmondson))
- Consolidate shared functionality ([@matt-edmondson](https://github.com/matt-edmondson))
- Rename tests project and convert to slnx ([@matt-edmondson](https://github.com/matt-edmondson))
- Merge remote-tracking branch 'common/main' into merge-common-providers ([@matt-edmondson](https://github.com/matt-edmondson))
- Add configuration providers for JSON, TOML, and YAML formats ([@matt-edmondson](https://github.com/matt-edmondson))
- Add abstractions for command execution, configuration, encoding, logging, navigation, persistence, and validation ([@matt-edmondson](https://github.com/matt-edmondson))
- Add .gitignore and project.yml for Serena configuration ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor permissions in dotnet.yml for least privilege; add SonarLint settings.json for project configuration ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove legacy build scripts ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove skipped_release logic from build steps in dotnet.yml ([@matt-edmondson](https://github.com/matt-edmondson))
- api suppressions ([@matt-edmondson](https://github.com/matt-edmondson))
- Update KtsuBuild cloning method to retrieve the latest tag correctly ([@matt-edmondson](https://github.com/matt-edmondson))
- Update KtsuBuild cloning method to use latest tag ([@matt-edmondson](https://github.com/matt-edmondson))
- Add compression, hashing, and obfuscation providers ([@matt-edmondson](https://github.com/matt-edmondson))
- Migrate to KtsuBuild dotnet build pipeline ([@matt-edmondson](https://github.com/matt-edmondson))
- Add .gitignore and project.yml for Serena configuration ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor null checks in IObfuscationProvider and ISerializationProvider to use Ensure.NotNull method; update Polyfill package version to 9.8.0 ([@matt-edmondson](https://github.com/matt-edmondson))
- Update docs and api compatibility files ([@matt-edmondson](https://github.com/matt-edmondson))
- Add project references and update AssemblyInfo for testing and source linking ([@matt-edmondson](https://github.com/matt-edmondson))
- Change project SDK from Microsoft.NET.Sdk to MSTest.Sdk ([@matt-edmondson](https://github.com/matt-edmondson))
- Update target framework to net10.0 and adjust assertions in tests ([@matt-edmondson](https://github.com/matt-edmondson))
- Update package versions in Directory.Packages.props ([@matt-edmondson](https://github.com/matt-edmondson))
- Add CLAUDE.md for project guidance and documentation ([@matt-edmondson](https://github.com/matt-edmondson))
- Add test project detection to Invoke-DotNetTest function ([@matt-edmondson](https://github.com/matt-edmondson))
- Update .NET version to 10.0 and adjust test coverage reporting ([@matt-edmondson](https://github.com/matt-edmondson))
- Update project configuration and add CLAUDE.md for documentation ([@matt-edmondson](https://github.com/matt-edmondson))
- Enhance project type detection in update-winget-manifests.ps1 by adding checks for generated NuGet packages and refining logic to distinguish between library, executable, test, and demo projects. ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor ISerializationOptions interface to unify member serialization policies and enhance clarity. Introduce new properties for serialization and deserialization policies, and update related enums for improved configurability. ([@matt-edmondson](https://github.com/matt-edmondson))
- Add ISerializationOptions interface and related policies for serialization configuration ([@matt-edmondson](https://github.com/matt-edmondson))
- Add SHA384 and SHA512 hash providers, along with FNV1_32, FNV1a_32, FNV1_64, and FNV1a_64 implementations. Update Common.sln and add corresponding unit tests for all new providers. Enhance existing tests for dependency injection and serialization. Include necessary project files and suppressions for compatibility. ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove .runsettings file, update project references in Common.sln, and add new providers for Gzip, Aes, Base64, MD5, SHA1, and SHA256 with corresponding project files. Update package versions in Directory.Packages.props and global.json. Add unit tests for dependency injection and functionality verification. ([@matt-edmondson](https://github.com/matt-edmondson))
- Update global.json and Abstractions.csproj to use ktsu.Sdk version 1.60.0 and switch project SDK to Microsoft.NET.Sdk, improving compatibility with .NET 8.0. ([@matt-edmondson](https://github.com/matt-edmondson))
- Add MD5HashProvider implementation and project files ([@matt-edmondson](https://github.com/matt-edmondson))
- Initial commit ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor ISerializationProvider interface to use generic type parameters for deserialization methods, enhancing type safety and usability. Update CompatibilitySuppressions.xml to remove obsolete suppressions related to nullable attributes, ensuring compatibility with .NET 8.0. ([@matt-edmondson](https://github.com/matt-edmondson))
- Update CompatibilitySuppressions.xml to reflect changes in diagnostic IDs and target methods for the ktsu.Abstractions library, enhancing compatibility with .NET 8.0. This includes updates for compression, encryption, hashing, and obfuscation methods, ensuring accurate suppression of diagnostics across versions. ([@matt-edmondson](https://github.com/matt-edmondson))
- Enhance ktsu.Abstractions library by refining interface descriptions and adding zero-allocation Try methods for compression, encryption, hashing, obfuscation, and serialization. Update README to reflect these changes, emphasizing performance improvements and usage examples. ([@matt-edmondson](https://github.com/matt-edmondson))
- Update README to reflect changes in target frameworks and provide an example implementation of a custom MD5 hash provider, enhancing clarity on usage and functionality. ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor interfaces in ktsu.Abstractions to use Try methods for compression, encryption, hashing, obfuscation, and serialization, enhancing performance by reducing allocations. Update README to reflect these changes and clarify usage. ([@matt-edmondson](https://github.com/matt-edmondson))
- Add System.Memory package reference and enhance interfaces in ktsu.Abstractions for better async support. Update README for clarity on usage and installation. ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove EnumOrderingAnalyzer project and related files from the solution, streamlining the project structure and eliminating unused analyzers. ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove obsolete abstraction models for compression, encryption, hashing, obfuscation, and filesystem types, along with global usings. This cleanup streamlines the project structure. ([@matt-edmondson](https://github.com/matt-edmondson))
- Add detailed README for ktsu.Abstractions library, outlining interfaces for compression, encryption, hashing, obfuscation, serialization, and filesystem access. Include installation instructions, quickstart examples, and contributing guidelines. ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove outdated files and update project references to reflect the new repository name 'Abstractions'. Set version to 1.0.0 and clean up changelog, README, and tags. ([@matt-edmondson](https://github.com/matt-edmondson))
- Initial commit ([@matt-edmondson](https://github.com/matt-edmondson))

