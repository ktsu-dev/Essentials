// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1850:Prefer static 'System.Security.Cryptography.SHA256.HashData' method over 'ComputeHash'", Justification = "Using conditional compilation for framework compatibility")]
