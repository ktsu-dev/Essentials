// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Xor;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the XOR obfuscation provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the XOR obfuscation provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IObfuscationProvider"/> in the resolvable set.
	/// A later call replaces an earlier registration of this provider in place, so arguments passed
	/// after <c>AddEssentials()</c> take effect.
	/// Registration is explicit rather than reflection-based so that the parameterless constructor is
	/// used rather than the greedy <c>byte[]</c> overload.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="key">The XOR key to use. Defaults to the provider's built-in key when omitted.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddXorObfuscationProvider(this IServiceCollection services, byte[]? key = null)
	{
		Ensure.NotNull(services);

		XorObfuscationProvider provider = key is null ? new() : new(key);
		ProviderRegistration.AddOrReplaceSingleton<IObfuscationProvider, XorObfuscationProvider>(services, provider);
		return services;
	}
}
