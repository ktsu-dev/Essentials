// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Bernoulli;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the Bernoulli distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Bernoulli distribution provider, fair by default.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IDiscreteDistribution"/> in the resolvable set, so it can be resolved either way. A
	/// singleton is safe because a distribution is immutable: its parameters are fixed at construction
	/// and sampling changes nothing, so the randomness passed in at the call site is the only state
	/// involved. Registration is explicit rather than reflection-based so the parameters below are used
	/// rather than whichever constructor happens to be greediest. Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="probability">The probability of success. Defaults to 0.5.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddBernoulliDistributionProvider(this IServiceCollection services, double probability = 0.5)
	{
		Ensure.NotNull(services);

		BernoulliDistributionProvider provider = new(probability);
		services.TryAddSingleton(provider);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiscreteDistribution>(provider));
		return services;
	}
}
