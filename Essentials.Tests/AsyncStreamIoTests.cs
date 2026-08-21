// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Threading.Tasks;
using ktsu.Essentials.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Proves the stream paths are genuinely asynchronous rather than synchronous work on a pool thread.
/// </summary>
/// <remarks>
/// "Holds no thread" cannot be established by measuring elapsed time without flaking. These tests
/// establish it by construction instead: <see cref="AsyncOnlyStream"/> throws from every synchronous
/// member, so an implementation that wraps synchronous work fails loudly rather than passing quietly.
/// </remarks>
[TestClass]
public class AsyncStreamIoTests
{
	[TestMethod]
	public void AsyncOnlyStream_ThrowsFromSynchronousRead()
	{
		using AsyncOnlyStream source = new([1, 2, 3]);

		Assert.ThrowsExactly<NotSupportedException>(() => source.ReadByte());
	}

	[TestMethod]
	public void AsyncOnlyStream_ThrowsFromSynchronousWrite()
	{
		using AsyncOnlyStream sink = new();

		Assert.ThrowsExactly<NotSupportedException>(() => sink.WriteByte(1));
	}

	[TestMethod]
	public async Task AsyncOnlyStream_RoundTripsThroughTheAsynchronousMembersAsync()
	{
		byte[] payload = [1, 2, 3, 4, 5];
		using AsyncOnlyStream source = new(payload);
		using AsyncOnlyStream sink = new();

		await source.CopyToAsync(sink, 81920, TestContext.CancellationTokenSource.Token).ConfigureAwait(false);

		CollectionAssert.AreEqual(payload, sink.ToArray());
	}

	public TestContext TestContext { get; set; } = null!;
}
