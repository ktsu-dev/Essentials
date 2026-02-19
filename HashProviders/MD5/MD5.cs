// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.HashProviders;

using ktsu.Essentials;
using System;
using System.IO;

/// <summary>
/// A hash provider that uses MD5 for hashing data.
/// </summary>
public class MD5 : IHashProvider, IDisposable
{
	private bool disposedValue;
	private readonly Lazy<System.Security.Cryptography.MD5> _md5;

	/// <summary>
	/// The length of the MD5 hash in bytes (16 bytes / 128 bits).
	/// </summary>
	public int HashLengthBytes => 16;

	/// <summary>
	/// Initializes a new instance of the <see cref="MD5"/> class.
	/// </summary>
	public MD5() => _md5 = new(System.Security.Cryptography.MD5.Create);

	/// <summary>
	/// Tries to hash the specified data into the provided hash buffer using MD5.
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
			return _md5.Value.TryComputeHash(data, destination, out int bytesWritten) && bytesWritten == HashLengthBytes;
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
	/// Tries to hash the specified data from a stream into the provided hash buffer using MD5.
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
			byte[] hash = _md5.Value.ComputeHash(data);

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
	/// Disposes the MD5 instance and releases any resources it holds.
	/// </summary>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				if (_md5.IsValueCreated)
				{
					_md5.Value.Dispose();
				}
			}

			disposedValue = true;
		}
	}

	/// <summary>
	/// Disposes the MD5 instance and releases any resources it holds.
	/// </summary>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
