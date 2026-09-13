// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.RandomProviders.Crypto;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the cryptographic random provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the cryptographic random provider.
	/// </summary>
	/// <remarks>
	/// Registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IRandomProvider"/> in the resolvable set. A singleton is safe here and not for the
	/// other random providers because this one holds no state: every draw goes to the operating system.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCryptoRandomProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton<CryptoRandomProvider>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IRandomProvider, CryptoRandomProvider>());
		return services;
	}
}
