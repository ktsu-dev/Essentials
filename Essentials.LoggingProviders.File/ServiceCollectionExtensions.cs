// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.LoggingProviders.File;

using ktsu.Essentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection registration for the rolling file logging provider.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the rolling file logging provider.
	/// </summary>
	/// <remarks>
	/// The provider is registered as a singleton, both as its concrete type and as an additional
	/// <see cref="ILoggingProvider"/> in the resolvable set, so it sits beside any other logger, such as the
	/// console one, and one instance serves both. It writes through the container's
	/// <see cref="IFileSystemProvider"/>, which must be registered as well. It is not part of
	/// <c>AddEssentials()</c>, because where the log goes belongs to the application.
	/// Calling this more than once is a no-op; the first options win.
	/// </remarks>
	/// <param name="services">The service collection to add the provider to.</param>
	/// <param name="options">Where to write and when to roll.</param>
	/// <returns>The same service collection, to allow chaining.</returns>
	public static IServiceCollection AddFileLoggingProvider(this IServiceCollection services, FileLoggingOptions options)
	{
		Ensure.NotNull(services);
		Ensure.NotNull(options);

		// Copied now, so a caller changing the options before the container resolves the provider
		// cannot change what it was registered with.
		FileLoggingOptions snapshot = new()
		{
			Directory = options.Directory,
			FileName = options.FileName,
			MaxFileSizeBytes = options.MaxFileSizeBytes,
			MaxFileCount = options.MaxFileCount,
			MinimumLevel = options.MinimumLevel,
		};

		services.TryAddSingleton(serviceProvider =>
			new FileLoggingProvider(serviceProvider.GetRequiredService<IFileSystemProvider>(), snapshot));
		services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggingProvider, FileLoggingProvider>(
			static serviceProvider => serviceProvider.GetRequiredService<FileLoggingProvider>()));
		return services;
	}
}
