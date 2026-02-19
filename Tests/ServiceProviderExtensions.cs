// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Common.Tests;

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceProviderExtensions
{
	/// <summary>
	/// Enumerates all providers of type <typeparamref name="T"/> from the service provider.
	/// </summary>
	/// <typeparam name="T">The type of provider to enumerate.</typeparam>
	/// <param name="serviceProvider">The service provider to enumerate providers from.</param>
	/// <returns>An enumerable of objects containing the provider and its name.</returns>
	public static IEnumerable<object[]> EnumerateProviders<T>(this ServiceProvider serviceProvider)
	{
		IEnumerable<T> providers = serviceProvider.GetServices<T>();
		foreach (T provider in providers)
		{
			if (provider == null)
			{
				throw new InvalidOperationException($"Provider of type {typeof(T).Name} is null.");
			}

			yield return new object[] { provider, provider.GetType().Name ?? "Unknown" };
		}
	}
}
