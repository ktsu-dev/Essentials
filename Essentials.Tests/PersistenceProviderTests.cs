// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.Tests;

using System;
using System.IO;
using ktsu.Essentials;
using ktsu.Essentials.PersistenceProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PersistenceProviderTests
{
	public TestContext TestContext { get; set; } = null!;

	private static IPersistenceProvider<string> CreatePersistence()
	{
		ServiceCollection services = new();
		services.AddPersistenceProviders();
		using ServiceProvider provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IPersistenceProvider<string>>();
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_Store_And_Retrieve()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		await persistence.StoreAsync("key1", "value1", TestContext.CancellationToken).ConfigureAwait(false);
		string? result = await persistence.RetrieveAsync<string>("key1", TestContext.CancellationToken).ConfigureAwait(false);

		Assert.AreEqual("value1", result, "Should retrieve stored value");
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_Retrieve_Missing_Returns_Default()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		string? result = await persistence.RetrieveAsync<string>("nonexistent", TestContext.CancellationToken).ConfigureAwait(false);

		Assert.IsNull(result, "Should return null for missing key");
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_Exists()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		await persistence.StoreAsync("key1", "value1", TestContext.CancellationToken).ConfigureAwait(false);

		bool exists = await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsTrue(exists, "Should find stored key");

		bool notExists = await persistence.ExistsAsync("nonexistent", TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsFalse(notExists, "Should not find missing key");
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_Remove()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		await persistence.StoreAsync("key1", "value1", TestContext.CancellationToken).ConfigureAwait(false);
		bool removed = await persistence.RemoveAsync("key1", TestContext.CancellationToken).ConfigureAwait(false);

		Assert.IsTrue(removed, "Should remove existing key");
		Assert.IsFalse(await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false), "Should not exist after removal");
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_Remove_Missing_Returns_False()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		bool removed = await persistence.RemoveAsync("nonexistent", TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsFalse(removed, "Should return false for missing key");
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_GetAllKeys()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		await persistence.StoreAsync("a", "1", TestContext.CancellationToken).ConfigureAwait(false);
		await persistence.StoreAsync("b", "2", TestContext.CancellationToken).ConfigureAwait(false);
		await persistence.StoreAsync("c", "3", TestContext.CancellationToken).ConfigureAwait(false);

		string[] keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
		Assert.AreEqual(3, keys.Length, "Should return all stored keys");
		CollectionAssert.Contains(keys, "a");
		CollectionAssert.Contains(keys, "b");
		CollectionAssert.Contains(keys, "c");
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_Clear()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		await persistence.StoreAsync("a", "1", TestContext.CancellationToken).ConfigureAwait(false);
		await persistence.StoreAsync("b", "2", TestContext.CancellationToken).ConfigureAwait(false);
		await persistence.ClearAsync(TestContext.CancellationToken).ConfigureAwait(false);

		string[] keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
		Assert.AreEqual(0, keys.Length, "Should have no keys after clear");
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Persistence_RetrieveOrCreate()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		TestData result = await persistence.RetrieveOrCreateAsync<TestData>("new_key", TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsNotNull(result, "Should create new instance");

		bool exists = await persistence.ExistsAsync("new_key", TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsTrue(exists, "Should store created instance");
	}

	[TestMethod]
	public void Persistence_Properties()
	{
		IPersistenceProvider<string> persistence = CreatePersistence();

		Assert.AreEqual("InMemory", persistence.ProviderName);
		Assert.IsFalse(persistence.IsPersistent, "InMemory provider should not be persistent");
	}

	// --- FileSystem Provider Tests ---

	private static void CleanupDirectory(string path)
	{
		if (Directory.Exists(path))
		{
			Directory.Delete(path, true);
		}
	}

	[TestMethod]
	public async System.Threading.Tasks.Task FileSystem_Store_And_Retrieve()
	{
		string tempDir = Path.Combine(Path.GetTempPath(), "PersistenceTests_FS_" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			FileSystemProviders.Native fs = new();
			SerializationProviders.Json serializer = new();
			FileSystem<string> persistence = new(fs, serializer, tempDir);

			await persistence.StoreAsync("key1", new TestData { Name = "test" }, TestContext.CancellationToken).ConfigureAwait(false);
			TestData? result = await persistence.RetrieveAsync<TestData>("key1", TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsNotNull(result);
			Assert.AreEqual("test", result.Name);
		}
		finally
		{
			CleanupDirectory(tempDir);
		}
	}

	[TestMethod]
	public async System.Threading.Tasks.Task FileSystem_Exists_And_Remove()
	{
		string tempDir = Path.Combine(Path.GetTempPath(), "PersistenceTests_FS_" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			FileSystemProviders.Native fs = new();
			SerializationProviders.Json serializer = new();
			FileSystem<string> persistence = new(fs, serializer, tempDir);

			await persistence.StoreAsync("key1", "value1", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsTrue(await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false));

			bool removed = await persistence.RemoveAsync("key1", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsTrue(removed);
			Assert.IsFalse(await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false));
		}
		finally
		{
			CleanupDirectory(tempDir);
		}
	}

	[TestMethod]
	public async System.Threading.Tasks.Task FileSystem_GetAllKeys_And_Clear()
	{
		string tempDir = Path.Combine(Path.GetTempPath(), "PersistenceTests_FS_" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			FileSystemProviders.Native fs = new();
			SerializationProviders.Json serializer = new();
			FileSystem<string> persistence = new(fs, serializer, tempDir);

			await persistence.StoreAsync("a", "1", TestContext.CancellationToken).ConfigureAwait(false);
			await persistence.StoreAsync("b", "2", TestContext.CancellationToken).ConfigureAwait(false);

			string[] keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			Assert.AreEqual(2, keys.Length);

			await persistence.ClearAsync(TestContext.CancellationToken).ConfigureAwait(false);
			keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			Assert.AreEqual(0, keys.Length);
		}
		finally
		{
			CleanupDirectory(tempDir);
		}
	}

	[TestMethod]
	public void FileSystem_Properties()
	{
		string tempDir = Path.Combine(Path.GetTempPath(), "PersistenceTests_FS_" + Guid.NewGuid().ToString("N")[..8]);
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		FileSystem<string> persistence = new(fs, serializer, tempDir);

		Assert.AreEqual("FileSystem", persistence.ProviderName);
		Assert.IsTrue(persistence.IsPersistent);
	}

	// --- AppData Provider Tests ---

	[TestMethod]
	public async System.Threading.Tasks.Task AppData_Store_And_Retrieve()
	{
		string appName = "PersistenceTests_AD_" + Guid.NewGuid().ToString("N")[..8];
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		AppData<string> persistence = new(fs, serializer, appName);

		try
		{
			await persistence.StoreAsync("key1", new TestData { Name = "appdata_test" }, TestContext.CancellationToken).ConfigureAwait(false);
			TestData? result = await persistence.RetrieveAsync<TestData>("key1", TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsNotNull(result);
			Assert.AreEqual("appdata_test", result.Name);
		}
		finally
		{
			await persistence.ClearAsync(TestContext.CancellationToken).ConfigureAwait(false);
			string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
			CleanupDirectory(appDataPath);
		}
	}

	[TestMethod]
	public async System.Threading.Tasks.Task AppData_Exists_And_Remove()
	{
		string appName = "PersistenceTests_AD_" + Guid.NewGuid().ToString("N")[..8];
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		AppData<string> persistence = new(fs, serializer, appName);

		try
		{
			await persistence.StoreAsync("key1", "value1", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsTrue(await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false));

			bool removed = await persistence.RemoveAsync("key1", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsTrue(removed);
			Assert.IsFalse(await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false));
		}
		finally
		{
			string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
			CleanupDirectory(appDataPath);
		}
	}

	[TestMethod]
	public void AppData_Properties()
	{
		string appName = "PersistenceTests_AD_" + Guid.NewGuid().ToString("N")[..8];
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		AppData<string> persistence = new(fs, serializer, appName);

		Assert.AreEqual("AppData", persistence.ProviderName);
		Assert.IsTrue(persistence.IsPersistent);
	}

	// --- Temp Provider Tests ---

	[TestMethod]
	public async System.Threading.Tasks.Task Temp_Store_And_Retrieve()
	{
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		using Temp<string> persistence = new(fs, serializer, "PersistenceTests_Temp");

		try
		{
			await persistence.StoreAsync("key1", new TestData { Name = "temp_test" }, TestContext.CancellationToken).ConfigureAwait(false);
			TestData? result = await persistence.RetrieveAsync<TestData>("key1", TestContext.CancellationToken).ConfigureAwait(false);

			Assert.IsNotNull(result);
			Assert.AreEqual("temp_test", result.Name);
		}
		finally
		{
			persistence.CleanupDirectory();
		}
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Temp_Exists_And_Remove()
	{
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		using Temp<string> persistence = new(fs, serializer, "PersistenceTests_Temp");

		try
		{
			await persistence.StoreAsync("key1", "value1", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsTrue(await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false));

			bool removed = await persistence.RemoveAsync("key1", TestContext.CancellationToken).ConfigureAwait(false);
			Assert.IsTrue(removed);
			Assert.IsFalse(await persistence.ExistsAsync("key1", TestContext.CancellationToken).ConfigureAwait(false));
		}
		finally
		{
			persistence.CleanupDirectory();
		}
	}

	[TestMethod]
	public async System.Threading.Tasks.Task Temp_GetAllKeys_And_Clear()
	{
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		using Temp<string> persistence = new(fs, serializer, "PersistenceTests_Temp");

		try
		{
			await persistence.StoreAsync("a", "1", TestContext.CancellationToken).ConfigureAwait(false);
			await persistence.StoreAsync("b", "2", TestContext.CancellationToken).ConfigureAwait(false);

			string[] keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			Assert.AreEqual(2, keys.Length);

			await persistence.ClearAsync(TestContext.CancellationToken).ConfigureAwait(false);
			keys = [.. await persistence.GetAllKeysAsync(TestContext.CancellationToken).ConfigureAwait(false)];
			Assert.AreEqual(0, keys.Length);
		}
		finally
		{
			persistence.CleanupDirectory();
		}
	}

	[TestMethod]
	public void Temp_Properties()
	{
		FileSystemProviders.Native fs = new();
		SerializationProviders.Json serializer = new();
		using Temp<string> persistence = new(fs, serializer, "PersistenceTests_Temp");

		try
		{
			Assert.AreEqual("Temp", persistence.ProviderName);
			Assert.IsFalse(persistence.IsPersistent);
		}
		finally
		{
			persistence.CleanupDirectory();
		}
	}

	public sealed class TestData
	{
		public string Name { get; set; } = string.Empty;
	}
}
