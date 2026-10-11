// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.FileSystemProviders.InMemory;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the in-memory filesystem provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the in-memory filesystem provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="IFileSystemProvider"/> in the resolvable set, so it can be resolved either way and every
	/// consumer in the container shares one in-memory file system. It is deliberately not part of
	/// <c>AddEssentials()</c>: resolving a single <see cref="IFileSystemProvider"/> returns the last one
	/// registered, and an application that silently wrote to memory instead of disk would lose its data.
	/// Register it in a test's container instead of, not beside, the native provider.
	/// Calling this more than once is a no-op.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddInMemoryFileSystemProvider(this IServiceCollection services)
	{
		Ensure.NotNull(services);

		services.TryAddSingleton(static _ => new InMemoryFileSystemProvider());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IFileSystemProvider, InMemoryFileSystemProvider>(
			static serviceProvider => serviceProvider.GetRequiredService<InMemoryFileSystemProvider>()));
		return services;
	}
}
