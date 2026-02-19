// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.Tests;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NavigationProviderTests
{
	public TestContext TestContext { get; set; } = null!;

	private static INavigationProvider<string> CreateNavigation()
	{
		ServiceCollection services = new();
		services.AddNavigationProviders();
		using ServiceProvider provider = services.BuildServiceProvider();
		return provider.GetRequiredService<INavigationProvider<string>>();
	}

	[TestMethod]
	public void Navigation_Initial_State()
	{
		INavigationProvider<string> nav = CreateNavigation();

		Assert.IsNull(nav.Current, "Current should be null initially");
		Assert.IsFalse(nav.CanGoBack, "Should not be able to go back initially");
		Assert.IsFalse(nav.CanGoForward, "Should not be able to go forward initially");
		Assert.AreEqual(0, nav.BackStack.Count, "Back stack should be empty");
		Assert.AreEqual(0, nav.ForwardStack.Count, "Forward stack should be empty");
	}

	[TestMethod]
	public void Navigation_NavigateTo_Sets_Current()
	{
		INavigationProvider<string> nav = CreateNavigation();

		nav.NavigateTo("page1");

		Assert.AreEqual("page1", nav.Current);
		Assert.IsFalse(nav.CanGoBack, "Should not be able to go back with one entry");
	}

	[TestMethod]
	public void Navigation_NavigateTo_Pushes_To_BackStack()
	{
		INavigationProvider<string> nav = CreateNavigation();

		nav.NavigateTo("page1");
		nav.NavigateTo("page2");

		Assert.AreEqual("page2", nav.Current);
		Assert.IsTrue(nav.CanGoBack, "Should be able to go back");
		Assert.AreEqual(1, nav.BackStack.Count, "Back stack should have one entry");
		Assert.AreEqual("page1", nav.BackStack[0]);
	}

	[TestMethod]
	public void Navigation_NavigateTo_Clears_ForwardStack()
	{
		INavigationProvider<string> nav = CreateNavigation();

		nav.NavigateTo("page1");
		nav.NavigateTo("page2");
		nav.GoBack();
		nav.NavigateTo("page3");

		Assert.IsFalse(nav.CanGoForward, "Forward stack should be cleared after new navigation");
		Assert.AreEqual("page3", nav.Current);
	}

	[TestMethod]
	public void Navigation_GoBack()
	{
		INavigationProvider<string> nav = CreateNavigation();

		nav.NavigateTo("page1");
		nav.NavigateTo("page2");
		string? result = nav.GoBack();

		Assert.AreEqual("page1", result, "GoBack should return previous page");
		Assert.AreEqual("page1", nav.Current, "Current should be updated");
		Assert.IsTrue(nav.CanGoForward, "Should be able to go forward");
		Assert.AreEqual("page2", nav.ForwardStack[0]);
	}

	[TestMethod]
	public void Navigation_GoBack_Empty_Returns_Default()
	{
		INavigationProvider<string> nav = CreateNavigation();

		string? result = nav.GoBack();

		Assert.IsNull(result, "GoBack on empty stack should return default");
	}

	[TestMethod]
	public void Navigation_GoForward()
	{
		INavigationProvider<string> nav = CreateNavigation();

		nav.NavigateTo("page1");
		nav.NavigateTo("page2");
		nav.GoBack();
		string? result = nav.GoForward();

		Assert.AreEqual("page2", result, "GoForward should return next page");
		Assert.AreEqual("page2", nav.Current, "Current should be updated");
		Assert.IsFalse(nav.CanGoForward, "Forward stack should be empty");
	}

	[TestMethod]
	public void Navigation_GoForward_Empty_Returns_Default()
	{
		INavigationProvider<string> nav = CreateNavigation();

		string? result = nav.GoForward();
		Assert.IsNull(result, "GoForward on empty stack should return default");
	}

	[TestMethod]
	public void Navigation_Clear_Resets_Everything()
	{
		INavigationProvider<string> nav = CreateNavigation();

		nav.NavigateTo("page1");
		nav.NavigateTo("page2");
		nav.NavigateTo("page3");
		nav.GoBack();
		nav.Clear();

		Assert.IsNull(nav.Current);
		Assert.IsFalse(nav.CanGoBack);
		Assert.IsFalse(nav.CanGoForward);
		Assert.AreEqual(0, nav.BackStack.Count);
		Assert.AreEqual(0, nav.ForwardStack.Count);
	}

	[TestMethod]
	public void Navigation_Multiple_BackForward()
	{
		INavigationProvider<string> nav = CreateNavigation();

		nav.NavigateTo("A");
		nav.NavigateTo("B");
		nav.NavigateTo("C");
		nav.NavigateTo("D");

		Assert.AreEqual("D", nav.Current);
		Assert.AreEqual(3, nav.BackStack.Count);

		nav.GoBack(); // C
		nav.GoBack(); // B

		Assert.AreEqual("B", nav.Current);
		Assert.AreEqual(1, nav.BackStack.Count);
		Assert.AreEqual(2, nav.ForwardStack.Count);

		nav.GoForward(); // C
		Assert.AreEqual("C", nav.Current);
	}
}
