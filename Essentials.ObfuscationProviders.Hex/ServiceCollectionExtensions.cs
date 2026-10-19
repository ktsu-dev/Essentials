// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Hex;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the hexadecimal obfuscation provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the hexadecimal obfuscation provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IObfuscationProvider"/> in the resolvable set.
	/// A later call replaces an earlier registration of this provider in place, so an encoder passed
	/// after <c>AddEssentials()</c> takes effect.
	/// Registration constructs the provider explicitly so the container does not select the greedy
	/// <see cref="IEncodingProvider"/> constructor and bind whichever encoder happens to be registered.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="encoder">The encoder to compose. Defaults to the built-in hex encoder when omitted.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddHexObfuscationProvider(this IServiceCollection services, IEncodingProvider? encoder = null)
	{
		Ensure.NotNull(services);

		HexObfuscationProvider provider = encoder is null ? new() : new(encoder);
		ProviderRegistration.AddOrReplaceSingleton<IObfuscationProvider, HexObfuscationProvider>(services, provider);
		return services;
	}
}
