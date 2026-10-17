// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration shared by the providers whose <c>Add…Provider</c> overloads take construction arguments.
/// </summary>
/// <remarks>
/// <para>
/// Linked into each such provider project rather than placed in the interfaces package, following
/// <c>DistributionArguments</c>. <c>TryAddSingleton</c> and <c>TryAddEnumerable</c> suit a provider with
/// nothing to configure, but for one with arguments they made a second call silently keep the first
/// instance: <c>AddEssentials()</c> registers the defaults, so a later <c>AddXorObfuscationProvider(key)</c>
/// went on obfuscating with the public default key. See issue #132.
/// </para>
/// <para>
/// So these registrations replace an existing one of the same concrete type, in place. Keeping the
/// position matters for the interface: resolving a single <c>IContinuousDistribution</c> returns the
/// last one registered, and <c>AddEssentials()</c> orders its registrations so that is the default.
/// </para>
/// </remarks>
internal static class ProviderRegistration
{
	/// <summary>
	/// Registers <paramref name="instance"/> as a singleton, both as its concrete type and as one of the
	/// resolvable <typeparamref name="TService"/> set, replacing any earlier registration of that concrete type.
	/// </summary>
	/// <typeparam name="TService">The provider interface to register in the resolvable set.</typeparam>
	/// <typeparam name="TImplementation">The concrete provider type.</typeparam>
	/// <param name="services">The service collection to register in.</param>
	/// <param name="instance">The configured provider.</param>
	internal static void AddOrReplaceSingleton<TService, TImplementation>(IServiceCollection services, TImplementation instance)
		where TService : class
		where TImplementation : class, TService
	{
		AddOrReplace(services, ServiceDescriptor.Singleton(instance), typeof(TImplementation));
		AddOrReplace(services, ServiceDescriptor.Singleton<TService>(instance), typeof(TImplementation));
	}

	/// <summary>
	/// Registers a provider's default configuration, or, if the caller already registered that provider,
	/// moves the caller's registration to where the default would have gone.
	/// </summary>
	/// <remarks>
	/// Used by the bundled <c>AddEssentials()</c> registrations, which must not replace arguments passed
	/// before them. Moving rather than skipping keeps the order those registrations choose, so the
	/// caller's instance is still the one a single interface resolves to when it is the category default.
	/// </remarks>
	/// <typeparam name="TImplementation">The concrete provider type the registration adds.</typeparam>
	/// <param name="services">The service collection to register in.</param>
	/// <param name="register">The provider's own registration, called with its defaults.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	internal static IServiceCollection AddDefaultOrKeep<TImplementation>(IServiceCollection services, Func<IServiceCollection, IServiceCollection> register)
		where TImplementation : class
	{
		bool registered = false;
		List<ServiceDescriptor> members = [];
		for (int i = 0; i < services.Count; i++)
		{
			ServiceDescriptor existing = services[i];
			if (existing.IsKeyedService)
			{
				continue;
			}

			if (existing.ServiceType == typeof(TImplementation))
			{
				registered = true;
			}
			else if (ImplementationTypeOf(existing) == typeof(TImplementation))
			{
				members.Add(existing);
			}
		}

		if (!registered)
		{
			return register(services);
		}

		foreach (ServiceDescriptor member in members)
		{
			services.Remove(member);
			services.Add(member);
		}

		return services;
	}

	private static void AddOrReplace(IServiceCollection services, ServiceDescriptor descriptor, Type implementationType)
	{
		for (int i = 0; i < services.Count; i++)
		{
			ServiceDescriptor existing = services[i];
			if (existing.ServiceType == descriptor.ServiceType && !existing.IsKeyedService && ImplementationTypeOf(existing) == implementationType)
			{
				services[i] = descriptor;
				return;
			}
		}

		services.Add(descriptor);
	}

	/// <summary>
	/// Gets the implementation type a descriptor stands for, as <c>TryAddEnumerable</c> compares them.
	/// </summary>
	private static Type? ImplementationTypeOf(ServiceDescriptor descriptor)
		=> descriptor.ImplementationType
			?? descriptor.ImplementationInstance?.GetType()
			?? (descriptor.ImplementationFactory?.GetType().GenericTypeArguments is [_, Type returned] ? returned : null);
}
