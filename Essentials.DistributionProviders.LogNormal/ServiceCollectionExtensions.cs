// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.LogNormal;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the log-normal distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the log-normal distribution provider, standard by default.
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
	/// <param name="logMean">The mean of the underlying normal distribution. Defaults to 0.0.</param>
	/// <param name="logStandardDeviation">The standard deviation of the underlying normal distribution. Defaults to 1.0.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddLogNormalDistributionProvider(this IServiceCollection services, double logMean = 0.0, double logStandardDeviation = 1.0)
	{
		Ensure.NotNull(services);

		LogNormalDistributionProvider provider = new(logMean, logStandardDeviation);
		ProviderRegistration.AddOrReplaceSingleton<IContinuousDistribution, LogNormalDistributionProvider>(services, provider);
		return services;
	}
}
