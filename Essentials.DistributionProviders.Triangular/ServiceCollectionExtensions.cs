// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.DistributionProviders.Triangular;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection registration for the triangular distribution provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers a triangular distribution provider over the given three-point estimate.
	/// </summary>
	/// <remarks>
	/// The parameters are required rather than defaulted, because a triangular distribution has no
	/// standard form to fall back on — the three points are the whole of what it knows. That is also why
	/// this provider is left out of the bundled <c>AddEssentials</c> registration. It is a singleton,
	/// safe because a distribution is immutable once constructed.
	/// A later call replaces an earlier registration of this provider in place, so arguments passed
	/// after <c>AddEssentials()</c> take effect.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="minimum">The lowest value the quantity can take.</param>
	/// <param name="mode">The most likely value.</param>
	/// <param name="maximum">The highest value the quantity can take.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddTriangularDistributionProvider(this IServiceCollection services, double minimum, double mode, double maximum)
	{
		Ensure.NotNull(services);

		TriangularDistributionProvider provider = new(minimum, mode, maximum);
		ProviderRegistration.AddOrReplaceSingleton<IContinuousDistribution, TriangularDistributionProvider>(services, provider);
		return services;
	}
}
