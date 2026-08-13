// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Caesar;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the Caesar obfuscation provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Caesar obfuscation provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IObfuscationProvider"/> in the resolvable set. Calling this more than once is a no-op.
	/// Registration is explicit rather than reflection-based so the shift is not resolved from the container.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="shift">The byte shift to apply. Defaults to 13.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCaesarObfuscationProvider(this IServiceCollection services, byte shift = 13)
	{
		Ensure.NotNull(services);

		CaesarObfuscationProvider provider = new(shift);
		services.TryAddSingleton(provider);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IObfuscationProvider>(provider));
		return services;
	}
}
