// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Exponential;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the exponential distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the exponential distribution provider, with unit rate by default.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IContinuousDistribution"/> in the resolvable set, so it can be resolved either way. A
	/// singleton is safe because a distribution is immutable: its parameters are fixed at construction
	/// and sampling changes nothing, so the randomness passed in at the call site is the only state
	/// involved. Registration is explicit rather than reflection-based so the parameters below are used
	/// rather than whichever constructor happens to be greediest.
	/// A later call replaces an earlier registration of this provider in place, so arguments passed
	/// after <c>AddEssentials()</c> take effect.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="rate">The average number of events per unit of time. Defaults to 1.0.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddExponentialDistributionProvider(this IServiceCollection services, double rate = 1.0)
	{
		Ensure.NotNull(services);

		ExponentialDistributionProvider provider = new(rate);
		ProviderRegistration.AddOrReplaceSingleton<IContinuousDistribution, ExponentialDistributionProvider>(services, provider);
		return services;
	}
}
