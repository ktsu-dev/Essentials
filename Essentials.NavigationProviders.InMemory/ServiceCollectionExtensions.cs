// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.NavigationProviders.InMemory;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the in-memory navigation provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the in-memory navigation provider as the open generic <see cref="INavigationProvider{T}"/>.
	/// </summary>
	/// <remarks>
	/// Registered as transient, because navigation history is per-consumer state rather than something
	/// to share across the application. Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddInMemoryNavigationProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAdd(ServiceDescriptor.Transient(typeof(INavigationProvider<>), typeof(InMemoryNavigationProvider<>)));
		return services;
	}
}
