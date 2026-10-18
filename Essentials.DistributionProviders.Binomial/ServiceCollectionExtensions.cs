// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Binomial;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the binomial distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers a binomial distribution provider over the given trial count and success probability.
	/// </summary>
	/// <remarks>
	/// The parameters are required rather than defaulted, because there is no standard binomial to fall
	/// back on — a trial count belongs to the situation being modelled. That is also why this provider is
	/// left out of the bundled <c>AddEssentials</c> registration. It is a singleton, safe because a
	/// distribution is immutable once constructed.
	/// A later call replaces an earlier registration of this provider in place, so arguments passed
	/// after <c>AddEssentials()</c> take effect.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="trials">The number of trials.</param>
	/// <param name="probability">The probability that each trial succeeds.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddBinomialDistributionProvider(this IServiceCollection services, int trials, double probability)
	{
		Ensure.NotNull(services);

		BinomialDistributionProvider provider = new(trials, probability);
		ProviderRegistration.AddOrReplaceSingleton<IDiscreteDistribution, BinomialDistributionProvider>(services, provider);
		return services;
	}
}
