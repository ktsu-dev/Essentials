// Copyright (c) 2023-2026 ktsu-dev contributors

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Security", "CA5401:Do not use CreateEncryptor with non-default IV", Justification = "AesEncryptionProvider intentionally allows custom IVs for flexibility")]
