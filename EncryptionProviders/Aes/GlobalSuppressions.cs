// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Security", "CA5401:Do not use CreateEncryptor with non-default IV", Justification = "AesEncryptionProvider intentionally allows custom IVs for flexibility")]
