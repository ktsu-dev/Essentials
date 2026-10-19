// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Globalization;
using System.IO;
using ktsu.Essentials;
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

	[TestMethod]
	[DataRow(64)]
	[DataRow(65)]
	[DataRow(200)]
	public void TrySerialize_DeepChain_SucceedsOnlyWhenItDeserializes(int depth)
	{
		NewtonsoftJsonSerializationProvider provider = new();
		Node root = new() { Name = "0" };
		Node tail = root;
		for (int i = 1; i < depth; i++)
		{
			tail.Next = new Node { Name = i.ToString(CultureInfo.InvariantCulture) };
			tail = tail.Next;
		}

		using StringWriter writer = new();
		bool serialized = provider.TrySerialize(root, writer);
		Node? roundTripped = ((ISerializationProvider)provider).Deserialize<Node>(writer.ToString());

		if (depth <= 64)
		{
			Assert.IsTrue(serialized);
			Assert.IsNotNull(roundTripped);
		}
		else
		{
			Assert.IsFalse(serialized);
		}
	}

	private sealed class Node
	{
		public string Name { get; set; } = string.Empty;

		public Node? Next { get; set; }
	}

	private sealed class FailingWriter : StringWriter
	{
		public override void Write(char value) => throw new IOException("disk full");

		public override void Write(char[] buffer, int index, int count) => throw new IOException("disk full");

		public override void Write(string? value) => throw new IOException("disk full");

		public override void Flush() => throw new IOException("disk full");
	}
}
