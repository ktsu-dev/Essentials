// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using ktsu.Essentials;
using ktsu.Essentials.All;
using ktsu.Essentials.NavigationProviders.InMemory;
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

	// The DI registration is an open generic, and nullable annotations do not survive to runtime, so a
	// provider over a nullable destination type is constructed directly rather than resolved.
	private static INavigationProvider<string?> CreateNullableNavigation() => new InMemoryNavigationProvider<string?>();

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

	[TestMethod]
	public void Navigation_NavigateTo_Pushes_Null_Current_To_BackStack()
	{
		INavigationProvider<string?> nav = CreateNullableNavigation();

		nav.NavigateTo("a");
		nav.NavigateTo(null);
		nav.NavigateTo("b");

		Assert.AreEqual("b", nav.Current);
		Assert.AreEqual(2, nav.BackStack.Count, "A null destination is a step in the history, not an absent one");
		Assert.AreEqual("a", nav.BackStack[0]);
		Assert.IsNull(nav.BackStack[1], "The null destination should be the most recent back entry");
	}

	[TestMethod]
	public void Navigation_GoBack_Lands_On_Null_Destination()
	{
		INavigationProvider<string?> nav = CreateNullableNavigation();

		nav.NavigateTo("a");
		nav.NavigateTo(null);
		nav.NavigateTo("b");
		nav.GoBack();

		Assert.IsNull(nav.Current, "GoBack should land on the null destination, not skip past it");
		Assert.AreEqual(1, nav.BackStack.Count, "Only 'a' should remain behind the null destination");
		Assert.AreEqual("a", nav.BackStack[0]);
		Assert.AreEqual(1, nav.ForwardStack.Count);
		Assert.AreEqual("b", nav.ForwardStack[0]);
	}

	[TestMethod]
	public void Navigation_GoBack_From_Null_Destination_Returns_Previous()
	{
		INavigationProvider<string?> nav = CreateNullableNavigation();

		nav.NavigateTo("a");
		nav.NavigateTo(null);
		nav.NavigateTo("b");
		nav.GoBack();
		string? result = nav.GoBack();

		Assert.AreEqual("a", result, "The step before the null destination should still be reachable");
		Assert.AreEqual("a", nav.Current);
		Assert.AreEqual(2, nav.ForwardStack.Count, "Both the null destination and 'b' should be ahead");
		Assert.IsNull(nav.ForwardStack[1], "The null destination should be the next forward entry");
		Assert.AreEqual("b", nav.ForwardStack[0]);
	}

	[TestMethod]
	public void Navigation_GoForward_Pushes_Null_Current_To_BackStack()
	{
		INavigationProvider<string?> nav = CreateNullableNavigation();

		nav.NavigateTo("a");
		nav.NavigateTo(null);
		nav.NavigateTo("b");
		nav.GoBack();
		string? result = nav.GoForward();

		Assert.AreEqual("b", result);
		Assert.AreEqual(2, nav.BackStack.Count, "Going forward off the null destination should push it back");
		Assert.AreEqual("a", nav.BackStack[0]);
		Assert.IsNull(nav.BackStack[1]);
	}

	[TestMethod]
	public void Navigation_First_NavigateTo_Does_Not_Push_Unset_Current()
	{
		INavigationProvider<string?> nav = CreateNullableNavigation();

		nav.NavigateTo(null);

		Assert.IsNull(nav.Current);
		Assert.AreEqual(0, nav.BackStack.Count, "An unset current destination is not a history entry");
		Assert.IsFalse(nav.CanGoBack);
	}

	[TestMethod]
	public void Navigation_Clear_Forgets_Null_Current()
	{
		INavigationProvider<string?> nav = CreateNullableNavigation();

		nav.NavigateTo(null);
		nav.Clear();
		nav.NavigateTo("a");

		Assert.AreEqual("a", nav.Current);
		Assert.AreEqual(0, nav.BackStack.Count, "Clear should forget that a destination was ever set");
		Assert.IsFalse(nav.CanGoBack);
	}
}
