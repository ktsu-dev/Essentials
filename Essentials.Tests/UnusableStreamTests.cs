// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.All;
using ktsu.Essentials.ObfuscationProviders.Composite;
using ktsu.Essentials.ObfuscationProviders.Reverse;
using ktsu.Essentials.ObfuscationProviders.Xor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Every stream <c>Try*</c> primitive in the compression, encryption, encoding and obfuscation providers reports a
/// source it cannot read, or a destination it cannot write, as <see langword="false"/> rather than letting the
/// <see cref="NotSupportedException"/> the BCL raises escape (issue #86), and the self-allocating wrapper turns that
/// into its documented <see cref="InvalidOperationException"/>.
/// </summary>
[TestClass]
public sealed class UnusableStreamTests
{
	private static readonly byte[] Payload = [.. System.Text.Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog, twice over.")];

	public TestContext TestContext { get; set; } = null!;

	public static IEnumerable<object[]> Transforms
	{
		get
		{
			ServiceCollection services = new();
			services.AddCompressionProviders().AddEncryptionProviders().AddEncodingProviders().AddObfuscationProviders();
			using ServiceProvider provider = services.BuildServiceProvider();

			foreach (ICompressionProvider compression in provider.GetServices<ICompressionProvider>())
			{
				string name = compression.GetType().Name;
				yield return [new StreamTransform($"{name} compress", compression.TryCompress, compression.TryCompressAsync, compression.Compress, Payload)];
				yield return [new StreamTransform($"{name} decompress", compression.TryDecompress, compression.TryDecompressAsync, compression.Decompress, compression.Compress(Payload))];
			}

			foreach (IEncryptionProvider encryption in provider.GetServices<IEncryptionProvider>())
			{
				string name = encryption.GetType().Name;
				byte[] key = encryption.GenerateKey();
				byte[] iv = encryption.GenerateIV();
				yield return [new StreamTransform(
					$"{name} encrypt",
					(s, d) => encryption.TryEncrypt(s, key, iv, d),
					(s, d, ct) => encryption.TryEncryptAsync(s, key, iv, d, ct),
					s => encryption.Encrypt(s, key, iv),
					Payload)];
				yield return [new StreamTransform(
					$"{name} decrypt",
					(s, d) => encryption.TryDecrypt(s, key, iv, d),
					(s, d, ct) => encryption.TryDecryptAsync(s, key, iv, d, ct),
					s => encryption.Decrypt(s, key, iv),
					encryption.Encrypt(Payload, key, iv))];
			}

			foreach (IEncodingProvider encoding in provider.GetServices<IEncodingProvider>())
			{
				string name = encoding.GetType().Name;
				yield return [new StreamTransform($"{name} encode", encoding.TryEncode, encoding.TryEncodeAsync, encoding.Encode, Payload)];
				yield return [new StreamTransform($"{name} decode", encoding.TryDecode, encoding.TryDecodeAsync, encoding.Decode, encoding.Encode(Payload))];
			}

			List<IObfuscationProvider> obfuscators = [.. provider.GetServices<IObfuscationProvider>()];
			obfuscators.Add(new CompositeObfuscationProvider([new XorObfuscationProvider(), new ReverseObfuscationProvider()]));
			foreach (IObfuscationProvider obfuscation in obfuscators)
			{
				string name = obfuscation.GetType().Name;
				yield return [new StreamTransform($"{name} obfuscate", obfuscation.TryObfuscate, obfuscation.TryObfuscateAsync, obfuscation.Obfuscate, Payload)];
				yield return [new StreamTransform($"{name} deobfuscate", obfuscation.TryDeobfuscate, obfuscation.TryDeobfuscateAsync, obfuscation.Deobfuscate, obfuscation.Obfuscate(Payload))];
			}
		}
	}

	[TestMethod]
	[DynamicData(nameof(Transforms))]
	public async Task A_Source_That_Cannot_Be_Read_Is_Reported_As_A_Failure(StreamTransform transform)
	{
		ArgumentNullException.ThrowIfNull(transform);

		string path = Path.Join(Path.GetTempPath(), $"{nameof(UnusableStreamTests)}_{Guid.NewGuid():N}.bin");
		using FileStream source = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);
		await source.WriteAsync(transform.Input, TestContext.CancellationToken).ConfigureAwait(false);
		source.Position = 0;

		using MemoryStream destination = new();
		Assert.IsFalse(transform.Try(source, destination), $"{transform} read a write-only source");
		Assert.IsFalse(await transform.TryAsync(source, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{transform} read a write-only source asynchronously");
		Assert.ThrowsExactly<InvalidOperationException>(() => transform.Allocate(source), $"{transform} did not wrap the failure");
	}

	[TestMethod]
	[DynamicData(nameof(Transforms))]
	public async Task A_Destination_That_Cannot_Be_Written_Is_Reported_As_A_Failure(StreamTransform transform)
	{
		ArgumentNullException.ThrowIfNull(transform);

		using MemoryStream destination = new(new byte[4096], writable: false);

		using (MemoryStream source = new(transform.Input.ToArray()))
		{
			Assert.IsFalse(transform.Try(source, destination), $"{transform} wrote to a read-only destination");
		}

		using (MemoryStream source = new(transform.Input.ToArray()))
		{
			Assert.IsFalse(await transform.TryAsync(source, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{transform} wrote to a read-only destination asynchronously");
		}
	}

	/// <summary>One provider's stream primitives in one direction, and an input it accepts.</summary>
	/// <param name="Name">The provider and direction, shown as the test case's name.</param>
	/// <param name="Try">The synchronous stream primitive.</param>
	/// <param name="TryAsync">The asynchronous stream primitive.</param>
	/// <param name="Allocate">The self-allocating stream wrapper.</param>
	/// <param name="Input">Bytes the transform accepts.</param>
	public sealed record StreamTransform(
		string Name,
		Func<Stream, Stream, bool> Try,
		Func<Stream, Stream, CancellationToken, Task<bool>> TryAsync,
		Func<Stream, byte[]> Allocate,
		ReadOnlyMemory<byte> Input)
	{
		/// <inheritdoc/>
		public override string ToString() => Name;
	}
}
