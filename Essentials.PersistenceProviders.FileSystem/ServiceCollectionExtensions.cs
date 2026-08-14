// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.PersistenceProviders.FileSystem;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the filesystem persistence provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the filesystem persistence provider for a specific key type.
	/// </summary>
	/// <remarks>
	/// This provider needs an explicit base directory, so it is registered as a closed generic per key
	/// type rather than as an open generic. An <see cref="IFileSystemProvider"/> and an
	/// <see cref="ISerializationProvider"/> must also be registered. Calling this more than once for the
	/// same key type is a no-op.
	/// </remarks>
	/// <typeparam name="TKey">The key type used to address stored objects.</typeparam>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="baseDirectory">The directory under which objects are stored.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddFileSystemPersistenceProvider<TKey>(this IServiceCollection services, string baseDirectory)
		where TKey : notnull
	{
		Ensure.NotNull(services);
		Ensure.NotNull(baseDirectory);

		services.TryAddSingleton<IPersistenceProvider<TKey>>(sp => new FileSystemPersistenceProvider<TKey>(
			sp.GetRequiredService<IFileSystemProvider>(),
			sp.GetRequiredService<ISerializationProvider>(),
			baseDirectory));
		return services;
	}
}
