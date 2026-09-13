// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.RandomProviders.Crypto;

using ktsu.Essentials;
using System;
using System.Security.Cryptography;

/// <summary>
/// An <see cref="IRandomProvider"/> backed by the operating system's cryptographic random number generator.
/// </summary>
/// <remarks>
/// <para>
/// The provider to reach for whenever the output is a secret or is used to authenticate something:
/// tokens, salts, nonces, password reset codes, shuffles whose result must not be predictable. Its
/// output cannot be reproduced or predicted from earlier output, which is exactly what the seedable
/// providers here cannot promise.
/// </para>
/// <para>
/// That guarantee rules out seeding, so this provider cannot be used for a repeatable simulation. It is
/// also the slowest of the four, by roughly an order of magnitude, because every draw goes through the
/// operating system rather than through arithmetic on local state.
/// </para>
/// <para>
/// Stateless and thread-safe: one instance can be shared across any number of threads.
/// </para>
/// </remarks>
public sealed class CryptoRandomProvider : IRandomProvider
{
	/// <inheritdoc />
	public void NextBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
