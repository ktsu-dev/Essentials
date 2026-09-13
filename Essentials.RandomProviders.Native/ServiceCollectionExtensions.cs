// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.RandomProviders.Native;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the platform random provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the platform random provider.
	/// </summary>
	/// <remarks>
	/// Registered as a transient, unlike most providers here, because the provider carries generator
	/// state and is not thread-safe. A transient hands each consumer its own instance, which is the only
	/// registration that stays correct when two of them run at once. Registration is explicit rather
	/// than reflection-based so the parameterless constructor is used rather than the seeded overload.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddNativeRandomProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddTransient(_ => new NativeRandomProvider());
		services.TryAddEnumerable(ServiceDescriptor.Transient<IRandomProvider, NativeRandomProvider>(_ => new NativeRandomProvider()));
		return services;
	}
}
