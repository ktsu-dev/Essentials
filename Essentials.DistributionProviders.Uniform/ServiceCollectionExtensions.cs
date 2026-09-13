// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Uniform;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the continuous uniform distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the continuous uniform distribution provider over the unit interval by default.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IContinuousDistribution"/> in the resolvable set, so it can be resolved either way. A
	/// singleton is safe because a distribution is immutable: its parameters are fixed at construction
	/// and sampling changes nothing, so the randomness passed in at the call site is the only state
	/// involved. Registration is explicit rather than reflection-based so the parameters below are used
	/// rather than whichever constructor happens to be greediest. Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="minimum">The lower bound of the interval. Defaults to 0.0.</param>
	/// <param name="maximum">The upper bound of the interval. Defaults to 1.0.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddUniformDistributionProvider(this IServiceCollection services, double minimum = 0.0, double maximum = 1.0)
	{
		Ensure.NotNull(services);

		UniformDistributionProvider provider = new(minimum, maximum);
		services.TryAddSingleton(provider);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IContinuousDistribution>(provider));
		return services;
	}
}
