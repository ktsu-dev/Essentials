// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.InMemory;
using ktsu.Essentials.PersistenceProviders.FileSystem;
using ktsu.Essentials.SerializationProviders.Toml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tomlyn has no cycle detection, and the stack overflow a cycle causes ends the process, so the
/// TOML provider has to refuse a cyclic graph before handing it over (issue #124).
/// </summary>
[TestClass]
public class TomlCycleTests
{
	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public void TrySerialize_SelfReferencingObject_ReturnsFalse()
	{
		Node node = new();
		node.Self = node;
		using StringWriter writer = new();

		Assert.IsFalse(new TomlSerializationProvider().TrySerialize(node, writer));
		Assert.AreEqual(string.Empty, writer.ToString(), "Nothing should be written for a cyclic graph");
	}

	[TestMethod]
	public void TrySerialize_CycleThroughAGetOnlyProperty_ReturnsFalse()
	{
		using StringWriter writer = new();

		Assert.IsFalse(new TomlSerializationProvider().TrySerialize(new GetOnlyParent(), writer));
	}

	[TestMethod]
	public void TrySerialize_CycleThroughADictionaryAndAList_ReturnsFalse()
	{
		Container root = new();
		Container child = new();
		root.Children.Add(child);
		child.Lookup["up"] = root;
		using StringWriter writer = new();

		Assert.IsFalse(new TomlSerializationProvider().TrySerialize(root, writer));
	}

	[TestMethod]
	public void TrySerialize_SharedButAcyclicObject_StillSerializes()
	{
		Node shared = new() { Name = "shared" };
		Pair pair = new() { Left = shared, Right = shared };
		using StringWriter writer = new();

		Assert.IsTrue(new TomlSerializationProvider().TrySerialize(pair, writer), "An object reached twice along different branches is not a cycle");
		StringAssert.Contains(writer.ToString(), "shared");
	}

	[TestMethod]
	public void TrySerialize_CycleBehindAnIgnoredProperty_StillSerializes()
	{
		IgnoredParent value = new();
		value.Parent = value;
		using StringWriter writer = new();

		Assert.IsTrue(new TomlSerializationProvider().TrySerialize(value, writer), "Tomlyn does not follow [IgnoreDataMember], so neither should the cycle check");
	}

	[TestMethod]
	public async Task StoreAsync_SelfReferencingObject_ThrowsPersistenceProviderException()
	{
		Node node = new();
		node.Self = node;
		FileSystemPersistenceProvider<string> persistence = new(new InMemoryFileSystemProvider(), new TomlSerializationProvider(), "/data");

		await Assert.ThrowsExactlyAsync<PersistenceProviderException>(() => persistence.StoreAsync("k", node, TestContext.CancellationToken)).ConfigureAwait(false);
	}

	public sealed class Node
	{
		public string Name { get; set; } = "a";
		public Node? Self { get; set; }
	}

	public sealed class Pair
	{
		public Node? Left { get; set; }
		public Node? Right { get; set; }
	}

	public sealed class GetOnlyParent
	{
		public string Name { get; set; } = "a";
		public GetOnlyParent Parent => this;
	}

	public sealed class Container
	{
		public Collection<Container> Children { get; } = [];
		public Dictionary<string, object> Lookup { get; } = [];
	}

	public sealed class IgnoredParent
	{
		public string Name { get; set; } = "a";

		[System.Runtime.Serialization.IgnoreDataMember]
		public IgnoredParent? Parent { get; set; }
	}
}
