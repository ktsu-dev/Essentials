// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.EncodingProviders;

using System;
using System.IO;
using System.Text;
using ktsu.Abstractions;

/// <summary>
/// An encoding provider that uses hexadecimal encoding for data encoding and decoding.
/// </summary>
public class Hex : IEncodingProvider
{
	/// <summary>
	/// Tries to encode the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="destination">The destination to write the encoded data to.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public bool TryEncode(ReadOnlySpan<byte> data, Span<byte> destination)
	{
		try
		{
			string hexString = Convert.ToHexString(data);
			byte[] encodedData = Encoding.UTF8.GetBytes(hexString);

			if (encodedData.Length > destination.Length)
			{
				return false;
			}

			encodedData.CopyTo(destination);
			destination[encodedData.Length..].Clear();
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to encode the data from the stream and write the result to the destination stream.
	/// </summary>
	/// <param name="data">The stream containing data to encode.</param>
	/// <param name="destination">The destination stream to write the encoded data to.</param>
	/// <returns>True if the encoding was successful, false otherwise.</returns>
	public bool TryEncode(Stream data, Stream destination)
	{
		if (data is null || destination is null)
		{
			return false;
		}

		try
		{
			using MemoryStream inputBuffer = new();
			data.CopyTo(inputBuffer);
			byte[] inputData = inputBuffer.ToArray();

			string hexString = Convert.ToHexString(inputData);
			byte[] encodedData = Encoding.UTF8.GetBytes(hexString);

			destination.Write(encodedData, 0, encodedData.Length);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (FormatException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decode the data from the span and write the result to the destination.
	/// </summary>
	/// <param name="encodedData">The encoded data to decode.</param>
	/// <param name="destination">The destination to write the decoded data to.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public bool TryDecode(ReadOnlySpan<byte> encodedData, Span<byte> destination)
	{
		try
		{
			// Find the actual length of encoded data (excluding trailing zeros)
			ReadOnlySpan<byte> actualData = encodedData;
			int lastNonZero = encodedData.Length - 1;
			while (lastNonZero >= 0 && encodedData[lastNonZero] == 0)
			{
				lastNonZero--;
			}

			if (lastNonZero >= 0)
			{
				actualData = encodedData[..(lastNonZero + 1)];
			}
			else
			{
				return false;
			}

			string hexString = Encoding.UTF8.GetString(actualData);
			byte[] decodedData = Convert.FromHexString(hexString);

			if (decodedData.Length > destination.Length)
			{
				return false;
			}

			decodedData.CopyTo(destination);
			destination[decodedData.Length..].Clear();
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	/// <summary>
	/// Tries to decode the data from the stream and write the result to the destination stream.
	/// </summary>
	/// <param name="encodedData">The stream containing encoded data to decode.</param>
	/// <param name="destination">The destination stream to write the decoded data to.</param>
	/// <returns>True if the decoding was successful, false otherwise.</returns>
	public bool TryDecode(Stream encodedData, Stream destination)
	{
		if (encodedData is null || destination is null)
		{
			return false;
		}

		try
		{
			using MemoryStream inputBuffer = new();
			encodedData.CopyTo(inputBuffer);
			string hexString = Encoding.UTF8.GetString(inputBuffer.ToArray());

			byte[] decodedData = Convert.FromHexString(hexString);
			destination.Write(decodedData, 0, decodedData.Length);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
		catch (FormatException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
}
