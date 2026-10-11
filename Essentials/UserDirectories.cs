// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials;

using System;
using System.IO;

/// <summary>
/// Resolves the per-user data and configuration directories used by persistence providers.
/// </summary>
/// <remarks>
/// <para>
/// Layout follows the XDG Base Directory Specification and is identical on every platform, so an
/// application stores its files in the same place regardless of where it runs. The home directory is
/// resolved per platform — <c>%USERPROFILE%</c> on Windows, <c>$HOME</c> elsewhere.
/// </para>
/// <list type="bullet">
/// <item><description>Data: <c>$XDG_DATA_HOME</c> if set, otherwise <c>~/.local/share</c>.</description></item>
/// <item><description>Config: <c>$XDG_CONFIG_HOME</c> if set, otherwise <c>~/.config</c>.</description></item>
/// <item><description>State: <c>$XDG_STATE_HOME</c> if set, otherwise <c>~/.local/state</c>. For data that should
/// survive a restart but is not worth backing up, such as logs, history and window layout.</description></item>
/// <item><description>Cache: <c>$XDG_CACHE_HOME</c> if set, otherwise <c>~/.cache</c>. For data that can be
/// deleted at any time and rebuilt.</description></item>
/// </list>
/// <para>
/// Per the specification, a relative or empty <c>XDG_*</c> value is ignored and the default is used.
/// </para>
/// </remarks>
public static class UserDirectories
{
	/// <summary>The environment variable that overrides the user data directory.</summary>
	public const string DataHomeVariable = "XDG_DATA_HOME";

	/// <summary>The environment variable that overrides the user configuration directory.</summary>
	public const string ConfigHomeVariable = "XDG_CONFIG_HOME";

	/// <summary>The environment variable that overrides the user state directory.</summary>
	public const string StateHomeVariable = "XDG_STATE_HOME";

	/// <summary>The environment variable that overrides the user cache directory.</summary>
	public const string CacheHomeVariable = "XDG_CACHE_HOME";

	/// <summary>
	/// Gets the base directory for user-specific data files.
	/// </summary>
	/// <returns><c>$XDG_DATA_HOME</c> when set to an absolute path, otherwise <c>~/.local/share</c>.</returns>
	public static string GetDataHome()
		=> ResolveBase(DataHomeVariable, Path.Join(".local", "share"));

	/// <summary>
	/// Gets the base directory for user-specific configuration files.
	/// </summary>
	/// <returns><c>$XDG_CONFIG_HOME</c> when set to an absolute path, otherwise <c>~/.config</c>.</returns>
	public static string GetConfigHome()
		=> ResolveBase(ConfigHomeVariable, ".config");

	/// <summary>
	/// Gets the base directory for user-specific state files, such as logs and history.
	/// </summary>
	/// <returns><c>$XDG_STATE_HOME</c> when set to an absolute path, otherwise <c>~/.local/state</c>.</returns>
	public static string GetStateHome()
		=> ResolveBase(StateHomeVariable, Path.Join(".local", "state"));

	/// <summary>
	/// Gets the base directory for user-specific cache files, which may be deleted at any time.
	/// </summary>
	/// <returns><c>$XDG_CACHE_HOME</c> when set to an absolute path, otherwise <c>~/.cache</c>.</returns>
	public static string GetCacheHome()
		=> ResolveBase(CacheHomeVariable, ".cache");

	/// <summary>
	/// Gets the data directory for a specific application.
	/// </summary>
	/// <param name="applicationName">The application name used to namespace the directory.</param>
	/// <param name="subdirectory">An optional subdirectory beneath the application directory.</param>
	/// <returns>The resolved application data directory.</returns>
	/// <exception cref="ArgumentException"><paramref name="applicationName"/> is null, empty, or whitespace.</exception>
	public static string GetApplicationDataDirectory(string applicationName, string? subdirectory = null)
		=> CombineApplication(GetDataHome(), applicationName, subdirectory);

	/// <summary>
	/// Gets the configuration directory for a specific application.
	/// </summary>
	/// <param name="applicationName">The application name used to namespace the directory.</param>
	/// <param name="subdirectory">An optional subdirectory beneath the application directory.</param>
	/// <returns>The resolved application configuration directory.</returns>
	/// <exception cref="ArgumentException"><paramref name="applicationName"/> is null, empty, or whitespace.</exception>
	public static string GetApplicationConfigDirectory(string applicationName, string? subdirectory = null)
		=> CombineApplication(GetConfigHome(), applicationName, subdirectory);

	/// <summary>
	/// Gets the state directory for a specific application.
	/// </summary>
	/// <param name="applicationName">The application name used to namespace the directory.</param>
	/// <param name="subdirectory">An optional subdirectory beneath the application directory.</param>
	/// <returns>The resolved application state directory.</returns>
	/// <exception cref="ArgumentException"><paramref name="applicationName"/> is null, empty, or whitespace.</exception>
	public static string GetApplicationStateDirectory(string applicationName, string? subdirectory = null)
		=> CombineApplication(GetStateHome(), applicationName, subdirectory);

	/// <summary>
	/// Gets the cache directory for a specific application.
	/// </summary>
	/// <param name="applicationName">The application name used to namespace the directory.</param>
	/// <param name="subdirectory">An optional subdirectory beneath the application directory.</param>
	/// <returns>The resolved application cache directory.</returns>
	/// <exception cref="ArgumentException"><paramref name="applicationName"/> is null, empty, or whitespace.</exception>
	public static string GetApplicationCacheDirectory(string applicationName, string? subdirectory = null)
		=> CombineApplication(GetCacheHome(), applicationName, subdirectory);

	/// <summary>
	/// Gets the current user's home directory.
	/// </summary>
	/// <returns><c>%USERPROFILE%</c> on Windows, <c>$HOME</c> on other platforms.</returns>
	/// <exception cref="InvalidOperationException">The home directory could not be determined.</exception>
	public static string GetHomeDirectory()
	{
		// Environment.SpecialFolder.UserProfile maps to %USERPROFILE% on Windows and $HOME elsewhere.
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

		if (string.IsNullOrEmpty(home))
		{
			// Some trimmed or sandboxed hosts return empty; fall back to the raw variables.
			home = Environment.GetEnvironmentVariable("USERPROFILE")
				?? Environment.GetEnvironmentVariable("HOME")
				?? string.Empty;
		}

		return string.IsNullOrEmpty(home)
			? throw new InvalidOperationException(
				"Unable to determine the user's home directory. Set USERPROFILE (Windows) or HOME, " +
				$"or set the {DataHomeVariable}, {ConfigHomeVariable}, {StateHomeVariable} or {CacheHomeVariable} variable to an absolute path.")
			: home;
	}

	private static string ResolveBase(string variable, string relativeDefault)
	{
		string? overridden = Environment.GetEnvironmentVariable(variable);

		// The specification says a value that is not an absolute path must be ignored.
		return !string.IsNullOrWhiteSpace(overridden) && Path.IsPathRooted(overridden)
			? overridden!
			: Path.Combine(GetHomeDirectory(), relativeDefault);
	}

	private static string CombineApplication(string baseDirectory, string applicationName, string? subdirectory)
	{
		if (string.IsNullOrWhiteSpace(applicationName))
		{
			throw new ArgumentException("Application name cannot be null or whitespace.", nameof(applicationName));
		}

		string path = Path.Combine(baseDirectory, applicationName);

		return string.IsNullOrWhiteSpace(subdirectory)
			? path
			: Path.Combine(path, subdirectory);
	}
}
