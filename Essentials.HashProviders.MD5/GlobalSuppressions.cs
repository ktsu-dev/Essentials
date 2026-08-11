// Copyright (c) 2023-2026 ktsu-dev contributors

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "MD5HashProvider is intentionally implementing MD5 hashing")]
[assembly: SuppressMessage("Performance", "CA1850:Prefer static 'System.Security.Cryptography.MD5.HashData' method over 'ComputeHash'", Justification = "Using conditional compilation for framework compatibility")]
