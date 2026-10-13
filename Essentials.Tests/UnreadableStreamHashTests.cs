// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.All;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The async stream defaults on <see cref="IHashProvider"/> and <see cref="IKeyedHashProvider"/> must honour the
/// <c>Try*</c> contract the synchronous <c>TryHash(Stream, ...)</c> already keeps: a stream that cannot be read
/// reports <see langword="false"/>, and <c>HashAsync</c> turns that into its documented
/// <see cref="InvalidOperationException"/>. Cancellation is not a read failure and still propagates.
/// </summary>
[TestClass]
public class UnreadableStreamHashTests
{
	private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

	public static IEnumerable<object[]> HashProviders
	{
		get
		{
			ServiceCollection services = new();
			services.AddHashProviders();
			return services.BuildServiceProvider().EnumerateProviders<IHashProvider>();
		}
	}

	public static IEnumerable<object[]> KeyedHashProviders
	{
		get
		{
			ServiceCollection services = new();
			services.AddKeyedHashProviders();
			return services.BuildServiceProvider().EnumerateProviders<IKeyedHashProvider>();
		}
	}

	public TestContext TestContext { get; set; } = null!;

	private static MemoryStream CreateDisposedStream()
	{
		MemoryStream stream = new([1, 2, 3]);
		stream.Dispose();
		return stream;
	}

	private static FileStream CreateWriteOnlyStream(string path) =>
		new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);

	private static string TempPath() => Path.Combine(Path.GetTempPath(), $"{nameof(UnreadableStreamHashTests)}_{Guid.NewGuid():N}.bin");

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Returns_False_For_A_Disposed_Stream(IHashProvider provider, string providerName)
	{
		using MemoryStream stream = CreateDisposedStream();
		byte[] destination = new byte[provider.HashLengthBytes];

		Assert.IsFalse(await provider.TryHashAsync(stream, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{providerName} should report a disposed stream as a failure");
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.HashAsync(stream, TestContext.CancellationToken)).ConfigureAwait(false);
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Returns_False_For_A_Write_Only_Stream(IHashProvider provider, string providerName)
	{
		using FileStream stream = CreateWriteOnlyStream(TempPath());
		byte[] destination = new byte[provider.HashLengthBytes];

		Assert.IsFalse(await provider.TryHashAsync(stream, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{providerName} should report a write-only stream as a failure");
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.HashAsync(stream, TestContext.CancellationToken)).ConfigureAwait(false);
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Returns_False_When_A_Read_Fails(IHashProvider provider, string providerName)
	{
		using FailingReadStream stream = new();
		byte[] destination = new byte[provider.HashLengthBytes];

		Assert.IsFalse(await provider.TryHashAsync(stream, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{providerName} should report a failed read as a failure");
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.HashAsync(stream, TestContext.CancellationToken)).ConfigureAwait(false);
	}

	[TestMethod]
	[DynamicData(nameof(HashProviders))]
	public async Task TryHashAsync_Still_Throws_On_Cancellation(IHashProvider provider, string providerName)
	{
		_ = providerName;
		using MemoryStream stream = new([1, 2, 3]);
		using CancellationTokenSource cts = new();
		await cts.CancelAsync().ConfigureAwait(false);
		byte[] destination = new byte[provider.HashLengthBytes];

		await Assert.ThrowsAsync<OperationCanceledException>(() => provider.TryHashAsync(stream, destination, cts.Token)).ConfigureAwait(false);
	}

	[TestMethod]
	[DynamicData(nameof(KeyedHashProviders))]
	public async Task Keyed_TryHashAsync_Returns_False_For_A_Disposed_Stream(IKeyedHashProvider provider, string providerName)
	{
		using MemoryStream stream = CreateDisposedStream();
		byte[] destination = new byte[provider.HashLengthBytes];

		Assert.IsFalse(await provider.TryHashAsync(Key, stream, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{providerName} should report a disposed stream as a failure");
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.HashAsync(Key, stream, TestContext.CancellationToken)).ConfigureAwait(false);
	}

	[TestMethod]
	[DynamicData(nameof(KeyedHashProviders))]
	public async Task Keyed_TryHashAsync_Returns_False_For_A_Write_Only_Stream(IKeyedHashProvider provider, string providerName)
	{
		using FileStream stream = CreateWriteOnlyStream(TempPath());
		byte[] destination = new byte[provider.HashLengthBytes];

		Assert.IsFalse(await provider.TryHashAsync(Key, stream, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{providerName} should report a write-only stream as a failure");
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.HashAsync(Key, stream, TestContext.CancellationToken)).ConfigureAwait(false);
	}

	[TestMethod]
	[DynamicData(nameof(KeyedHashProviders))]
	public async Task Keyed_TryHashAsync_Returns_False_When_A_Read_Fails(IKeyedHashProvider provider, string providerName)
	{
		using FailingReadStream stream = new();
		byte[] destination = new byte[provider.HashLengthBytes];

		Assert.IsFalse(await provider.TryHashAsync(Key, stream, destination, TestContext.CancellationToken).ConfigureAwait(false), $"{providerName} should report a failed read as a failure");
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.HashAsync(Key, stream, TestContext.CancellationToken)).ConfigureAwait(false);
	}

	[TestMethod]
	[DynamicData(nameof(KeyedHashProviders))]
	public async Task Keyed_TryHashAsync_Still_Throws_On_Cancellation(IKeyedHashProvider provider, string providerName)
	{
		_ = providerName;
		using MemoryStream stream = new([1, 2, 3]);
		using CancellationTokenSource cts = new();
		await cts.CancelAsync().ConfigureAwait(false);
		byte[] destination = new byte[provider.HashLengthBytes];

		await Assert.ThrowsAsync<OperationCanceledException>(() => provider.TryHashAsync(Key, stream, destination, cts.Token)).ConfigureAwait(false);
	}

	/// <summary>
	/// A readable stream whose reads fail with <see cref="IOException"/>, as a dropped network or file handle does.
	/// </summary>
	private sealed class FailingReadStream : Stream
	{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The read failed.");
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
