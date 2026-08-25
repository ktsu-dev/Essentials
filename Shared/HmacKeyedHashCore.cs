// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// The HMAC implementation shared by the keyed hash providers, parameterized by algorithm.
/// </summary>
/// <remarks>
/// Linked into each provider project rather than placed in the interfaces package, following
/// <c>NonCryptoIncrementalHash</c>. It is internal because every package compiles its own copy, so a
/// public type would collide for a consumer referencing more than one keyed hash package.
/// <para>
/// Key material is copied because <see cref="IncrementalHash.CreateHMAC(HashAlgorithmName, byte[])"/>
/// takes an array on the floor target framework. Every copy is zeroed once the HMAC owns it. Placing
/// that here rather than in each provider means it is written once instead of three times.
/// </para>
/// </remarks>
internal static class HmacKeyedHashCore
{
	/// <summary>
	/// Computes the authentication tag for a span of data.
	/// </summary>
	/// <param name="algorithm">The hash algorithm underlying the HMAC.</param>
	/// <param name="hashLengthBytes">The expected tag length.</param>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The data to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written.</param>
	/// <returns>True if the tag was written, false otherwise.</returns>
	internal static bool TryHash(HashAlgorithmName algorithm, int hashLengthBytes, ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (destination.Length < hashLengthBytes)
		{
			return false;
		}

		byte[] keyCopy = key.ToArray();
		try
		{
			using IncrementalHash hash = IncrementalHash.CreateHMAC(algorithm, keyCopy);
			hash.AppendData(data);
			return hash.TryGetHashAndReset(destination, out bytesWritten)
				&& bytesWritten == hashLengthBytes;
		}
		catch (ArgumentException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (CryptographicException)
		{
			bytesWritten = 0;
			return false;
		}
		finally
		{
			CryptographicOperations.ZeroMemory(keyCopy);
		}
	}

	/// <summary>
	/// Computes the authentication tag over a stream, reading it in one pass.
	/// </summary>
	/// <param name="algorithm">The hash algorithm underlying the HMAC.</param>
	/// <param name="hashLengthBytes">The expected tag length.</param>
	/// <param name="key">The secret key.</param>
	/// <param name="data">The stream to authenticate.</param>
	/// <param name="destination">The buffer to write the tag to.</param>
	/// <param name="bytesWritten">The number of bytes written.</param>
	/// <returns>True if the tag was written, false otherwise.</returns>
	internal static bool TryHash(HashAlgorithmName algorithm, int hashLengthBytes, ReadOnlySpan<byte> key, Stream data, Span<byte> destination, out int bytesWritten)
	{
		bytesWritten = 0;

		if (data is null || destination.Length < hashLengthBytes)
		{
			return false;
		}

		byte[] keyCopy = key.ToArray();
		byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
		try
		{
			using IncrementalHash hash = IncrementalHash.CreateHMAC(algorithm, keyCopy);
			int read;
			while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
			{
				hash.AppendData(buffer.AsSpan(0, read));
			}

			return hash.TryGetHashAndReset(destination, out bytesWritten)
				&& bytesWritten == hashLengthBytes;
		}
		catch (ArgumentException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (CryptographicException)
		{
			bytesWritten = 0;
			return false;
		}
		catch (IOException)
		{
			bytesWritten = 0;
			return false;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
			CryptographicOperations.ZeroMemory(keyCopy);
		}
	}

	/// <summary>
	/// Creates a genuinely incremental keyed hash.
	/// </summary>
	/// <param name="algorithm">The hash algorithm underlying the HMAC.</param>
	/// <param name="hashLengthBytes">The tag length.</param>
	/// <param name="key">The secret key.</param>
	/// <returns>An incremental hash the caller owns and should dispose.</returns>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership of the IncrementalHash transfers to the returned IncrementalHashAdapter, which disposes it.")]
	internal static IIncrementalHash CreateIncremental(HashAlgorithmName algorithm, int hashLengthBytes, ReadOnlySpan<byte> key)
	{
		byte[] keyCopy = key.ToArray();
		try
		{
			return new IncrementalHashAdapter(
				IncrementalHash.CreateHMAC(algorithm, keyCopy),
				hashLengthBytes);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(keyCopy);
		}
	}
}
