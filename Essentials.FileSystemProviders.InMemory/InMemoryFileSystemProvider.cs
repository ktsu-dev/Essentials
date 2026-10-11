// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.FileSystemProviders.InMemory;

using System;
using System.IO.Abstractions;
using ktsu.Essentials;
using Testably.Abstractions.Testing;

/// <summary>
/// A file system provider that keeps every file and directory in memory, for tests.
/// </summary>
/// <remarks>
/// <para>
/// Backed by Testably.Abstractions.Testing's <see cref="MockFileSystem"/>, which simulates the
/// path rules, error behaviour and timestamps of a real file system rather than a dictionary of
/// strings, so code that runs against it behaves as it would on disk. Each instance is its own
/// empty file system: nothing written to one is visible to another, or to the real disk.
/// </para>
/// <para>
/// <see cref="MockFileSystem"/> exposes what the interface does not, such as simulating another
/// operating system, initializing a tree in one call, or intercepting and failing operations. Pass
/// a configured instance to the constructor, or reach it through <see cref="MockFileSystem"/>.
/// </para>
/// </remarks>
public class InMemoryFileSystemProvider : IFileSystemProvider
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryFileSystemProvider"/> class over an
	/// empty file system that simulates the current operating system.
	/// </summary>
	public InMemoryFileSystemProvider()
		: this(new MockFileSystem())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryFileSystemProvider"/> class over an
	/// existing mock file system.
	/// </summary>
	/// <param name="mockFileSystem">The mock file system to expose through the provider.</param>
	/// <exception cref="ArgumentNullException"><paramref name="mockFileSystem"/> is null.</exception>
	public InMemoryFileSystemProvider(MockFileSystem mockFileSystem)
	{
		Ensure.NotNull(mockFileSystem);
		MockFileSystem = mockFileSystem;
	}

	/// <summary>
	/// Gets the mock file system behind this provider, for the test helpers the interface does not carry.
	/// </summary>
	public MockFileSystem MockFileSystem { get; }

	/// <summary>
	/// Gets the directory operations.
	/// </summary>
	public IDirectory Directory => MockFileSystem.Directory;

	/// <summary>
	/// Gets the directory info factory.
	/// </summary>
	public IDirectoryInfoFactory DirectoryInfo => MockFileSystem.DirectoryInfo;

	/// <summary>
	/// Gets the drive info factory.
	/// </summary>
	public IDriveInfoFactory DriveInfo => MockFileSystem.DriveInfo;

	/// <summary>
	/// Gets the file operations.
	/// </summary>
	public IFile File => MockFileSystem.File;

	/// <summary>
	/// Gets the file info factory.
	/// </summary>
	public IFileInfoFactory FileInfo => MockFileSystem.FileInfo;

	/// <summary>
	/// Gets the file stream factory.
	/// </summary>
	public IFileStreamFactory FileStream => MockFileSystem.FileStream;

	/// <summary>
	/// Gets the file system watcher factory.
	/// </summary>
	public IFileSystemWatcherFactory FileSystemWatcher => MockFileSystem.FileSystemWatcher;

	/// <summary>
	/// Gets the path operations.
	/// </summary>
	public IPath Path => MockFileSystem.Path;

	/// <summary>
	/// Gets the file version info factory.
	/// </summary>
	public IFileVersionInfoFactory FileVersionInfo => MockFileSystem.FileVersionInfo;

#if NET6_0_OR_GREATER
	/// <summary>
	/// Gets the random access operations.
	/// </summary>
	/// <remarks>
	/// Present from .NET 6 onward only, as on <see cref="IFileSystem.RandomAccess"/> itself.
	/// </remarks>
	public IRandomAccess RandomAccess => MockFileSystem.RandomAccess;
#endif
}
