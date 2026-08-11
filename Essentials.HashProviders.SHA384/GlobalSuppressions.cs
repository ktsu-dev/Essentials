// Copyright (c) 2023-2026 ktsu-dev contributors

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1850:Prefer static 'System.Security.Cryptography.SHA384.HashData' method over 'ComputeHash'", Justification = "Using conditional compilation for framework compatibility")]
