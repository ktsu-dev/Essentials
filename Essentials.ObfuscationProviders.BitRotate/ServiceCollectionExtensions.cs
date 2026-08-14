// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.BitRotate;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
	/// <see cref="IObfuscationProvider"/> in the resolvable set. Calling this more than once is a no-op.
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
		services.TryAddSingleton(provider);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IObfuscationProvider>(provider));
		return services;
	}
}
