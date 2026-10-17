// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Geometric;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the geometric distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the geometric distribution provider, with an even chance of success by default.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IDiscreteDistribution"/> in the resolvable set, so it can be resolved either way. A
	/// singleton is safe because a distribution is immutable: its parameters are fixed at construction
	/// and sampling changes nothing, so the randomness passed in at the call site is the only state
	/// involved. Registration is explicit rather than reflection-based so the parameters below are used
	/// rather than whichever constructor happens to be greediest.
	/// A later call replaces an earlier registration of this provider in place, so arguments passed
	/// after <c>AddEssentials()</c> take effect.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="probability">The probability of success on each trial. Defaults to 0.5.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddGeometricDistributionProvider(this IServiceCollection services, double probability = 0.5)
	{
		Ensure.NotNull(services);

		GeometricDistributionProvider provider = new(probability);
		ProviderRegistration.AddOrReplaceSingleton<IDiscreteDistribution, GeometricDistributionProvider>(services, provider);
		return services;
	}
}
