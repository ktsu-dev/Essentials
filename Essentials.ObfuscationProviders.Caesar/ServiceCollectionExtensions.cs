// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Caesar;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

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
	/// <see cref="IObfuscationProvider"/> in the resolvable set.
	/// A later call replaces an earlier registration of this provider in place, so arguments passed
	/// after <c>AddEssentials()</c> take effect.
	/// Registration is explicit rather than reflection-based so the shift is not resolved from the container.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="shift">The byte shift to apply. Defaults to 13.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCaesarObfuscationProvider(this IServiceCollection services, byte shift = 13)
	{
		Ensure.NotNull(services);

		CaesarObfuscationProvider provider = new(shift);
		ProviderRegistration.AddOrReplaceSingleton<IObfuscationProvider, CaesarObfuscationProvider>(services, provider);
		return services;
	}
}
