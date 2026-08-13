// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.ObfuscationProviders.Composite;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Generic;

/// <summary>
/// Dependency injection registration for the composite obfuscation provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers a composite obfuscation provider that pipelines the given stages in order.
	/// </summary>
	/// <remarks>
	/// Unlike the other obfuscation providers, this one has no meaningful default — the caller must
	/// supply the stages to compose. The provider is registered as a singleton, both as its concrete
	/// type and as an additional <see cref="IObfuscationProvider"/> in the resolvable set. Calling this
	/// more than once is a no-op; the stages from the first call win.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="stages">The obfuscation stages to pipeline, applied in order.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCompositeObfuscationProvider(this IServiceCollection services, IReadOnlyList<IObfuscationProvider> stages)
	{
		Ensure.NotNull(services);
		Ensure.NotNull(stages);

		CompositeObfuscationProvider provider = new(stages);
		services.TryAddSingleton(provider);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IObfuscationProvider>(provider));
		return services;
	}
}
