// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System.IO;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.InMemory;
using ktsu.Essentials.FileSystemProviders.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testably.Abstractions.Testing;

/// <summary>
/// What the in-memory provider promises beyond the shared contract in <see cref="FileSystemProviderTests"/>,
/// which runs over it too.
/// </summary>
[TestClass]
public class InMemoryFileSystemProviderTests
{
	[TestMethod]
	public void Writes_Never_Reach_The_Disk()
	{
		InMemoryFileSystemProvider memory = new();
		NativeFileSystemProvider disk = new();
		string path = Path.Combine(Path.GetTempPath(), "ktsu-inmemory-" + Guid.NewGuid().ToString("N") + ".txt");

		memory.Directory.CreateDirectory(Path.GetTempPath());
		memory.File.WriteAllText(path, "only in memory");

		Assert.IsTrue(memory.File.Exists(path));
		Assert.IsFalse(disk.File.Exists(path));
	}

	[TestMethod]
	public void Each_Instance_Is_Its_Own_File_System()
	{
		InMemoryFileSystemProvider first = new();
		InMemoryFileSystemProvider second = new();
		string path = first.Path.Combine(first.Path.GetTempPath(), "shared-name.txt");

		first.Directory.CreateDirectory(first.Path.GetTempPath());
		first.File.WriteAllText(path, "first");

		Assert.IsFalse(second.File.Exists(path));
	}

	[TestMethod]
	public void Behaves_Like_A_File_System_Rather_Than_A_Dictionary()
	{
		InMemoryFileSystemProvider fileSystem = new();
		string missingDirectory = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		Assert.ThrowsExactly<DirectoryNotFoundException>(() =>
			fileSystem.File.WriteAllText(fileSystem.Path.Combine(missingDirectory, "a.txt"), "x"));
		fileSystem.Directory.CreateDirectory(fileSystem.Path.GetTempPath());
		Assert.ThrowsExactly<FileNotFoundException>(() =>
			fileSystem.File.ReadAllText(fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), "missing.txt")));
	}

	[TestMethod]
	public void Exposes_The_Mock_It_Was_Given()
	{
		MockFileSystem mock = new();
		InMemoryFileSystemProvider fileSystem = new(mock);
		Assert.AreSame(mock, fileSystem.MockFileSystem);

		mock.Initialize().WithFile("seeded.txt").Which(f => f.HasStringContent("from the mock"));
		Assert.AreEqual("from the mock", fileSystem.File.ReadAllText("seeded.txt"));
	}

	[TestMethod]
	public void Null_Mock_Is_Rejected()
		=> Assert.ThrowsExactly<ArgumentNullException>(() => new InMemoryFileSystemProvider(null!));

	[TestMethod]
	public void Registration_Shares_One_File_System()
	{
		ServiceCollection services = new();
		services.AddInMemoryFileSystemProvider().AddInMemoryFileSystemProvider();
		using ServiceProvider provider = services.BuildServiceProvider();

		IFileSystemProvider[] all = [.. provider.GetServices<IFileSystemProvider>()];
		Assert.HasCount(1, all);
		Assert.AreSame(provider.GetRequiredService<InMemoryFileSystemProvider>(), all[0]);
	}
}
