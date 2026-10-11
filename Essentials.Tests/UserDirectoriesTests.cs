// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.IO;
using ktsu.Essentials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Covers the state and cache homes. The data and config homes are covered beside the persistence
/// providers that read them.
/// </summary>
[TestClass]
[DoNotParallelize]
public class UserDirectoriesTests
{
	[TestMethod]
	public void StateHome_Defaults_To_LocalState()
		=> WithVariable(UserDirectories.StateHomeVariable, null, () =>
			Assert.AreEqual(Path.Join(UserDirectories.GetHomeDirectory(), ".local", "state"), UserDirectories.GetStateHome()));

	[TestMethod]
	public void CacheHome_Defaults_To_DotCache()
		=> WithVariable(UserDirectories.CacheHomeVariable, null, () =>
			Assert.AreEqual(Path.Join(UserDirectories.GetHomeDirectory(), ".cache"), UserDirectories.GetCacheHome()));

	[TestMethod]
	public void StateHome_Honours_Absolute_Override()
	{
		string overridden = Path.Join(Path.GetTempPath(), "xdg_state_" + Guid.NewGuid().ToString("N")[..8]);
		WithVariable(UserDirectories.StateHomeVariable, overridden, () =>
		{
			Assert.AreEqual(overridden, UserDirectories.GetStateHome());
			Assert.AreEqual(Path.Join(overridden, "MyApp", "logs"), UserDirectories.GetApplicationStateDirectory("MyApp", "logs"));
		});
	}

	[TestMethod]
	public void CacheHome_Honours_Absolute_Override()
	{
		string overridden = Path.Join(Path.GetTempPath(), "xdg_cache_" + Guid.NewGuid().ToString("N")[..8]);
		WithVariable(UserDirectories.CacheHomeVariable, overridden, () =>
		{
			Assert.AreEqual(overridden, UserDirectories.GetCacheHome());
			Assert.AreEqual(Path.Join(overridden, "MyApp"), UserDirectories.GetApplicationCacheDirectory("MyApp"));
		});
	}

	[TestMethod]
	public void StateAndCache_Ignore_Relative_Overrides()
	{
		WithVariable(UserDirectories.StateHomeVariable, "relative/state", () =>
			Assert.AreEqual(Path.Join(UserDirectories.GetHomeDirectory(), ".local", "state"), UserDirectories.GetStateHome()));
		WithVariable(UserDirectories.CacheHomeVariable, "relative/cache", () =>
			Assert.AreEqual(Path.Join(UserDirectories.GetHomeDirectory(), ".cache"), UserDirectories.GetCacheHome()));
	}

	[TestMethod]
	public void Application_Directories_Reject_A_Blank_Name()
	{
		Assert.ThrowsExactly<ArgumentException>(() => UserDirectories.GetApplicationStateDirectory(" "));
		Assert.ThrowsExactly<ArgumentException>(() => UserDirectories.GetApplicationCacheDirectory(string.Empty));
	}

	[TestMethod]
	public void All_Four_Homes_Are_Distinct()
		=> WithVariable(UserDirectories.StateHomeVariable, null, () =>
			WithVariable(UserDirectories.CacheHomeVariable, null, () =>
			{
				string[] homes = [UserDirectories.GetDataHome(), UserDirectories.GetConfigHome(), UserDirectories.GetStateHome(), UserDirectories.GetCacheHome()];
				CollectionAssert.AllItemsAreUnique(homes);
			}));

	private static void WithVariable(string variable, string? value, Action body)
	{
		string? original = Environment.GetEnvironmentVariable(variable);
		try
		{
			Environment.SetEnvironmentVariable(variable, value);
			body();
		}
		finally
		{
			Environment.SetEnvironmentVariable(variable, original);
		}
	}
}
