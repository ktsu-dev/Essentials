// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Categorical;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Generic;

/// <summary>
/// Dependency injection registration for the categorical distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers a categorical distribution provider over the given weights.
	/// </summary>
	/// <remarks>
	/// The weights are required rather than defaulted, because they are the entire distribution — there
	/// is nothing left of it to default. That is also why this provider is left out of the bundled
	/// <c>AddEssentials</c> registration. It is a singleton, safe because the weights are normalised and
	/// the cumulative sums built once at construction and never touched again. Calling this more than
	/// once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="weights">The weight of each outcome, where the outcome is its index.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCategoricalDistributionProvider(this IServiceCollection services, IReadOnlyList<double> weights)
	{
		Ensure.NotNull(services);

		CategoricalDistributionProvider provider = new(weights);
		services.TryAddSingleton(provider);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IDiscreteDistribution>(provider));
		return services;
	}
}
