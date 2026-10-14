// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.IO;
using ktsu.Essentials.SerializationProviders.NewtonsoftJson;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NewtonsoftJsonSerializationProviderTests
{
	[TestMethod]
	public void TrySerialize_DisposedWriter_ReturnsFalse()
	{
		NewtonsoftJsonSerializationProvider provider = new();
		StringWriter writer = new();
		writer.Dispose();

		Assert.IsFalse(provider.TrySerialize(new { A = 1 }, writer));
	}

	[TestMethod]
	public void TrySerialize_WriterThrowingIOException_ReturnsFalse()
	{
		NewtonsoftJsonSerializationProvider provider = new();
		using FailingWriter writer = new();

		Assert.IsFalse(provider.TrySerialize(new { A = 1 }, writer));
	}

	private sealed class FailingWriter : StringWriter
	{
		public override void Write(char value) => throw new IOException("disk full");

		public override void Write(char[] buffer, int index, int count) => throw new IOException("disk full");

		public override void Write(string? value) => throw new IOException("disk full");

		public override void Flush() => throw new IOException("disk full");
	}
}
