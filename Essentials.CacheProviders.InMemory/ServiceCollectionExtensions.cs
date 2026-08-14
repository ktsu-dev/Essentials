// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CacheProviders.InMemory;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the in-memory cache provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the in-memory cache provider as the open generic <see cref="ICacheProvider{TKey, TValue}"/>.
	/// </summary>
	/// <remarks>
	/// Registered as a singleton so cached entries are shared for the lifetime of the container.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddInMemoryCacheProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAdd(ServiceDescriptor.Singleton(typeof(ICacheProvider<,>), typeof(InMemoryCacheProvider<,>)));
		return services;
	}
}
