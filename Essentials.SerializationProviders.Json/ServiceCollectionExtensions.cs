// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.SerializationProviders.Json;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;

/// <summary>
/// Dependency injection registration for the System.Text.Json serialization provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the System.Text.Json serialization provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="ISerializationProvider"/> in the resolvable set, so it can be resolved either way. The container
	/// constructs and owns each registration, which means disposable providers are disposed with it.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddJsonSerializationProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton(static _ => new JsonSerializationProvider());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ISerializationProvider, JsonSerializationProvider>(
			static serviceProvider => serviceProvider.GetRequiredService<JsonSerializationProvider>()));
		return services;
	}

	/// <summary>
	/// Registers the System.Text.Json serialization provider with the given serializer options.
	/// </summary>
	/// <remarks>
	/// Registered the same way as <see cref="AddJsonSerializationProvider(IServiceCollection)"/>, and like it
	/// a no-op when a JSON provider is already registered, so call this before <c>AddEssentials()</c> or
	/// <c>AddSerializationProviders()</c> for these options to be the ones used. The options are copied.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="options">The serializer options to use.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddJsonSerializationProvider(this IServiceCollection services, JsonSerializerOptions options)
	{
		Ensure.NotNull(services);
		Ensure.NotNull(options);

		JsonSerializationProvider provider = new(options);
		services.TryAddSingleton(provider);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ISerializationProvider, JsonSerializationProvider>(
			_ => provider));
		return services;
	}
}
