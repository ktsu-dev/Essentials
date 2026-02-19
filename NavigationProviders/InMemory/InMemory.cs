// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.NavigationProviders;

using ktsu.Essentials;
using System.Collections.Generic;

/// <summary>
/// An in-memory navigation provider that manages back/forward navigation with history using lists.
/// </summary>
/// <typeparam name="T">The type of the navigation destination.</typeparam>
public class InMemory<T> : INavigationProvider<T>
{
	private readonly List<T> backStack = [];
	private readonly List<T> forwardStack = [];

	/// <summary>
	/// Gets the current navigation destination, or the default value if no navigation has occurred.
	/// </summary>
	public T? Current { get; private set; }

	/// <summary>
	/// Gets a value indicating whether backward navigation is possible.
	/// </summary>
	public bool CanGoBack => backStack.Count > 0;

	/// <summary>
	/// Gets a value indicating whether forward navigation is possible.
	/// </summary>
	public bool CanGoForward => forwardStack.Count > 0;

	/// <summary>
	/// Gets the back navigation stack. The most recently visited destination is at the end of the list.
	/// </summary>
	public IReadOnlyList<T> BackStack => backStack.AsReadOnly();

	/// <summary>
	/// Gets the forward navigation stack. The next destination is at the end of the list.
	/// </summary>
	public IReadOnlyList<T> ForwardStack => forwardStack.AsReadOnly();

	/// <summary>
	/// Navigates to the specified destination. The current destination is pushed onto the back stack,
	/// and the forward stack is cleared.
	/// </summary>
	/// <param name="destination">The destination to navigate to.</param>
	public void NavigateTo(T destination)
	{
		if (Current is not null)
		{
			backStack.Add(Current);
		}

		Current = destination;
		forwardStack.Clear();
	}

	/// <summary>
	/// Navigates backward to the previous destination in the back stack.
	/// The current destination is pushed onto the forward stack.
	/// </summary>
	/// <returns>The previous destination, or the default value if the back stack is empty.</returns>
	public T? GoBack()
	{
		if (backStack.Count == 0)
		{
			return default;
		}

		if (Current is not null)
		{
			forwardStack.Add(Current);
		}

		int lastIndex = backStack.Count - 1;
		Current = backStack[lastIndex];
		backStack.RemoveAt(lastIndex);
		return Current;
	}

	/// <summary>
	/// Navigates forward to the next destination in the forward stack.
	/// The current destination is pushed onto the back stack.
	/// </summary>
	/// <returns>The next destination, or the default value if the forward stack is empty.</returns>
	public T? GoForward()
	{
		if (forwardStack.Count == 0)
		{
			return default;
		}

		if (Current is not null)
		{
			backStack.Add(Current);
		}

		int lastIndex = forwardStack.Count - 1;
		Current = forwardStack[lastIndex];
		forwardStack.RemoveAt(lastIndex);
		return Current;
	}

	/// <summary>
	/// Clears all navigation history including the back stack, forward stack, and current destination.
	/// </summary>
	public void Clear()
	{
		backStack.Clear();
		forwardStack.Clear();
		Current = default;
	}
}
