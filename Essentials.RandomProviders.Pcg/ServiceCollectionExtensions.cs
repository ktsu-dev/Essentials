// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.RandomProviders.Pcg;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the PCG random provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the PCG random provider.
	/// </summary>
	/// <remarks>
	/// Registered as a transient because the provider carries generator state and is not thread-safe; a
	/// transient hands each consumer its own instance. Registration is explicit rather than
	/// reflection-based so the parameterless constructor is used rather than the seeded overloads.
	/// A container-resolved instance is therefore seeded from system entropy: construct the provider
	/// yourself with a seed and a stream when the point is to reproduce a sequence. Calling this more
	/// than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddPcgRandomProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddTransient(_ => new PcgRandomProvider());
		services.TryAddEnumerable(ServiceDescriptor.Transient<IRandomProvider, PcgRandomProvider>(_ => new PcgRandomProvider()));
		return services;
	}
}
