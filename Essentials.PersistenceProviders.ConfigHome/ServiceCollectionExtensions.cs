// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.ConfigHome;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the user configuration directory persistence provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the user configuration directory persistence provider for a specific key type.
	/// </summary>
	/// <remarks>
	/// This provider needs an application name, so it is registered as a closed generic per key type
	/// rather than as an open generic. An <see cref="IFileSystemProvider"/> and an
	/// <see cref="ISerializationProvider"/> must also be registered. Calling this more than once for the
	/// same key type is a no-op.
	/// </remarks>
	/// <typeparam name="TKey">The key type used to address stored objects.</typeparam>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="applicationName">The application name used to namespace the configuration directory.</param>
	/// <param name="subdirectory">An optional subdirectory beneath the application directory.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddConfigHomePersistenceProvider<TKey>(
		this IServiceCollection services,
		string applicationName,
		string? subdirectory = null)
		where TKey : notnull
	{
		Ensure.NotNull(services);
		Ensure.NotNull(applicationName);

		services.TryAddSingleton<IPersistenceProvider<TKey>>(sp => new ConfigHomePersistenceProvider<TKey>(
			sp.GetRequiredService<IFileSystemProvider>(),
			sp.GetRequiredService<ISerializationProvider>(),
			applicationName,
			subdirectory));
		return services;
	}
}
