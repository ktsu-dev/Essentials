// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.Tests;

using System.Collections.Generic;
using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class FileSystemProviderTests
{
	private static ServiceProvider BuildProvider()
	{
		ServiceCollection services = new();
		services.AddFileSystemProviders();
		return services.BuildServiceProvider();
	}

	public static IEnumerable<object[]> FileSystemProviders => BuildProvider().EnumerateProviders<IFileSystemProvider>();

	[TestMethod]
	[DynamicData(nameof(FileSystemProviders))]
	public void FileSystem_Can_Create_And_Read_File(IFileSystemProvider fileSystem, string providerName)
	{
		string tempDir = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), "ktsu-tests");
		fileSystem.Directory.CreateDirectory(tempDir);
		string path = fileSystem.Path.Combine(tempDir, Guid.NewGuid() + ".txt");

		string content = "test data for " + providerName;
		fileSystem.File.WriteAllText(path, content);
		string read = fileSystem.File.ReadAllText(path);
		Assert.AreEqual(content, read, $"{providerName} should read back the same content that was written");

		fileSystem.File.Delete(path);
	}

	[TestMethod]
	[DynamicData(nameof(FileSystemProviders))]
	public void FileSystem_Directory_Operations(IFileSystemProvider fileSystem, string providerName)
	{
		string tempDir = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), "ktsu-tests", Guid.NewGuid().ToString());

		// Test directory creation
		fileSystem.Directory.CreateDirectory(tempDir);
		Assert.IsTrue(fileSystem.Directory.Exists(tempDir), $"{providerName} should create directories");

		// Test directory deletion
		fileSystem.Directory.Delete(tempDir);
		Assert.IsFalse(fileSystem.Directory.Exists(tempDir), $"{providerName} should delete directories");
	}

	[TestMethod]
	[DynamicData(nameof(FileSystemProviders))]
	public void FileSystem_File_Operations(IFileSystemProvider fileSystem, string providerName)
	{
		string tempDir = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), "ktsu-tests");
		fileSystem.Directory.CreateDirectory(tempDir);
		string filePath = fileSystem.Path.Combine(tempDir, Guid.NewGuid() + ".txt");

		// Test file existence check
		Assert.IsFalse(fileSystem.File.Exists(filePath), $"{providerName} should report non-existent files correctly");

		// Test file creation and existence
		fileSystem.File.WriteAllText(filePath, "test content");
		Assert.IsTrue(fileSystem.File.Exists(filePath), $"{providerName} should report existing files correctly");

		// Test file deletion
		fileSystem.File.Delete(filePath);
		Assert.IsFalse(fileSystem.File.Exists(filePath), $"{providerName} should delete files");
	}

	[TestMethod]
	[DynamicData(nameof(FileSystemProviders))]
	public void FileSystem_Path_Operations(IFileSystemProvider fileSystem, string providerName)
	{
		// Test path combination
		string combined = fileSystem.Path.Combine("folder", "subfolder", "file.txt");
		Assert.Contains("folder", combined, $"{providerName} should combine paths correctly - folder");
		Assert.Contains("subfolder", combined, $"{providerName} should combine paths correctly - subfolder");
		Assert.Contains("file.txt", combined, $"{providerName} should combine paths correctly - file.txt");

		// Test temp path
		string tempPath = fileSystem.Path.GetTempPath();
		Assert.IsFalse(string.IsNullOrEmpty(tempPath), $"{providerName} should provide temp path");
	}
}
