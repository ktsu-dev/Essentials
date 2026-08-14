// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.Temp;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the temporary-directory persistence provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the temporary-directory persistence provider for a specific key type.
	/// </summary>
	/// <remarks>
	/// This provider takes an optional application name, so it is registered as a closed generic per key
	/// type rather than as an open generic. An <see cref="IFileSystemProvider"/> and an
	/// <see cref="ISerializationProvider"/> must also be registered. Calling this more than once for the
	/// same key type is a no-op.
	/// </remarks>
	/// <typeparam name="TKey">The key type used to address stored objects.</typeparam>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="applicationName">Optional application name used to namespace the temporary subdirectory.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddTempPersistenceProvider<TKey>(this IServiceCollection services, string? applicationName = null)
		where TKey : notnull
	{
		Ensure.NotNull(services);

		services.TryAddSingleton<IPersistenceProvider<TKey>>(sp => new TempPersistenceProvider<TKey>(
			sp.GetRequiredService<IFileSystemProvider>(),
			sp.GetRequiredService<ISerializationProvider>(),
			applicationName));
		return services;
	}
}
