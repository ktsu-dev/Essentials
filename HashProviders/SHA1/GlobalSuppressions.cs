// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "SHA1HashProvider is intentionally implementing SHA1 hashing for compatibility")]
[assembly: SuppressMessage("Performance", "CA1850:Prefer static 'System.Security.Cryptography.SHA1.HashData' method over 'ComputeHash'", Justification = "Using conditional compilation for framework compatibility")]
