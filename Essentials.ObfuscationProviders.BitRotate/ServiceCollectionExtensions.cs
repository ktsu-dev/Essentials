// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.BitRotate;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the bit-rotation obfuscation provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the bit-rotation obfuscation provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IObfuscationProvider"/> in the resolvable set.
	/// A later call replaces an earlier registration of this provider in place, so arguments passed
	/// after <c>AddEssentials()</c> take effect.
	/// Registration is explicit rather than reflection-based so the rotation count is not resolved
	/// from the container.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="bits">The number of bits to rotate by. Defaults to 3.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddBitRotateObfuscationProvider(this IServiceCollection services, int bits = 3)
	{
		Ensure.NotNull(services);

		BitRotateObfuscationProvider provider = new(bits);
		ProviderRegistration.AddOrReplaceSingleton<IObfuscationProvider, BitRotateObfuscationProvider>(services, provider);
		return services;
	}
}
