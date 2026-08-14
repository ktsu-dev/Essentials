// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CompressionProviders.ZLib;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the ZLib compression provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the ZLib compression provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="ICompressionProvider"/> in the resolvable set, so it can be resolved either way. The container
	/// constructs and owns each registration, which means disposable providers are disposed with it.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddZLibCompressionProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton<ZLibCompressionProvider>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ICompressionProvider, ZLibCompressionProvider>());
		return services;
	}
}
