// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.HashProviders.CRC64;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the CRC-64 hashing provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the CRC-64 hashing provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IHashProvider"/> in the resolvable set, so it can be resolved either way. The container
	/// constructs and owns each registration, which means disposable providers are disposed with it.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCRC64HashProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton<CRC64HashProvider>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHashProvider, CRC64HashProvider>());
		return services;
	}
}
