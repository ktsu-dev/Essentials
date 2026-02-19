// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.HashProviders;

using System;
using System.IO;
using ktsu.Abstractions;

/// <summary>
/// A hash provider that uses SHA1 for hashing data.
/// </summary>
public class SHA1 : IHashProvider, IDisposable
{
	private bool disposedValue;
	private readonly Lazy<System.Security.Cryptography.SHA1> _sha1;

	/// <summary>
	/// The length of the SHA1 hash in bytes (20 bytes / 160 bits).
	/// </summary>
	public int HashLengthBytes => 20;

	/// <summary>
	/// Initializes a new instance of the <see cref="SHA1"/> class.
	/// </summary>
	public SHA1() => _sha1 = new(System.Security.Cryptography.SHA1.Create);

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer using SHA1.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		try
		{
			return _sha1.Value.TryComputeHash(data, destination, out int bytesWritten) && bytesWritten == HashLengthBytes;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
		catch (NotSupportedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to hash the specified data from a stream into the provided hash buffer using SHA1.
	/// </summary>
	/// <param name="data">The stream containing data to hash.</param>
	/// <param name="destination">The hash buffer to write the result to.</param>
	/// <returns>True if the hash operation was successful, false otherwise.</returns>
	public bool TryHash(Stream data, Span<byte> destination)
	{
		if (destination.Length < HashLengthBytes)
		{
			return false;
		}

		if (data is null)
		{
			return false;
		}

		try
		{
			byte[] hash = _sha1.Value.ComputeHash(data);

			if (hash.Length != HashLengthBytes)
			{
				return false;
			}

			hash.CopyTo(destination);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
		catch (NotSupportedException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="SHA1"/> instance and optionally releases the managed resources.
	/// </summary>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				if (_sha1.IsValueCreated)
				{
					_sha1.Value.Dispose();
				}
			}

			disposedValue = true;
		}
	}

	/// <summary>
	/// Releases all resources used by the <see cref="SHA1"/> instance.
	/// </summary>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
