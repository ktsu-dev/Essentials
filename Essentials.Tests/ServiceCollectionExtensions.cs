// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using ktsu.Essentials.All;
using ktsu.Essentials.ObfuscationProviders.BitRotate;
using ktsu.Essentials.ObfuscationProviders.Composite;
using ktsu.Essentials.ObfuscationProviders.Xor;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Test-side composition over the registration extensions shipped by the provider packages.
/// </summary>
/// <remarks>
/// The per-provider and per-category registrations now live in the packages themselves
/// (<c>ktsu.Essentials.&lt;Category&gt;.&lt;Impl&gt;</c>) and are aggregated by <c>ktsu.Essentials.All</c>.
/// This file only adds what the aggregate deliberately leaves out — the composite obfuscator, which has
/// no meaningful default configuration.
/// </remarks>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers every provider the tests exercise.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddCommon(this IServiceCollection services)
		=> services
			.AddEssentials()
			.AddTestCompositeObfuscationProvider();

	/// <summary>
	/// Registers the obfuscation providers, including a composite configured for the tests.
	/// </summary>
	/// <param name="services">The service collection to add the providers to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddObfuscationProvidersWithComposite(this IServiceCollection services)
		=> services
			.AddObfuscationProviders()
			.AddTestCompositeObfuscationProvider();

	private static IServiceCollection AddTestCompositeObfuscationProvider(this IServiceCollection services)
		=> services.AddCompositeObfuscationProvider([new XorObfuscationProvider(), new BitRotateObfuscationProvider()]);
}
