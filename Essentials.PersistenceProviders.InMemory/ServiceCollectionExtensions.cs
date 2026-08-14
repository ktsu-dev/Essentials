// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.InMemory;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the in-memory persistence provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the in-memory persistence provider as the open generic <see cref="IPersistenceProvider{TKey}"/>.
	/// </summary>
	/// <remarks>
	/// Registered as a singleton so stored objects survive for the lifetime of the container.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddInMemoryPersistenceProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAdd(ServiceDescriptor.Singleton(typeof(IPersistenceProvider<>), typeof(InMemoryPersistenceProvider<>)));
		return services;
	}
}
