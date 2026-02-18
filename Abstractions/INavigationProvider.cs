// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System.Collections.Generic;

/// <summary>
/// Interface for navigation providers that manage back/forward navigation with history.
/// Provides browser-like navigation semantics for any type of destination.
/// </summary>
/// <typeparam name="T">The type of the navigation destination.</typeparam>
public interface INavigationProvider<T>
{
	/// <summary>
	/// Gets the current navigation destination, or the default value if no navigation has occurred.
	/// </summary>
	public T? Current { get; }

	/// <summary>
	/// Gets a value indicating whether backward navigation is possible.
	/// </summary>
	public bool CanGoBack { get; }

	/// <summary>
	/// Gets a value indicating whether forward navigation is possible.
	/// </summary>
	public bool CanGoForward { get; }

	/// <summary>
	/// Gets the back navigation stack. The most recently visited destination is at the end of the list.
	/// </summary>
	public IReadOnlyList<T> BackStack { get; }

	/// <summary>
	/// Gets the forward navigation stack. The next destination is at the end of the list.
	/// </summary>
	public IReadOnlyList<T> ForwardStack { get; }

	/// <summary>
	/// Navigates to the specified destination. The current destination is pushed onto the back stack,
	/// and the forward stack is cleared.
	/// </summary>
	/// <param name="destination">The destination to navigate to.</param>
	public void NavigateTo(T destination);

	/// <summary>
	/// Navigates backward to the previous destination in the back stack.
	/// The current destination is pushed onto the forward stack.
	/// </summary>
	/// <returns>The previous destination, or the default value if the back stack is empty.</returns>
	public T? GoBack();

	/// <summary>
	/// Navigates forward to the next destination in the forward stack.
	/// The current destination is pushed onto the back stack.
	/// </summary>
	/// <returns>The next destination, or the default value if the forward stack is empty.</returns>
	public T? GoForward();

	/// <summary>
	/// Clears all navigation history including the back stack, forward stack, and current destination.
	/// </summary>
	public void Clear();
}
