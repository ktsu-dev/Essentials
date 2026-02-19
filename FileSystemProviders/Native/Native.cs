// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Essentials.FileSystemProviders;

using ktsu.Essentials;
using System.IO.Abstractions;
using Testably.Abstractions;

/// <summary>
/// A default file system provider that provides access to the real file system.
/// </summary>
public class Native : IFileSystemProvider
{
	private readonly RealFileSystem fileSystem;

	/// <summary>
	/// Initializes a new instance of the <see cref="Native"/> class.
	/// </summary>
	public Native() => fileSystem = new RealFileSystem();

	/// <summary>
	/// Gets the directory operations.
	/// </summary>
	public IDirectory Directory => fileSystem.Directory;

	/// <summary>
	/// Gets the directory info factory.
	/// </summary>
	public IDirectoryInfoFactory DirectoryInfo => fileSystem.DirectoryInfo;

	/// <summary>
	/// Gets the drive info factory.
	/// </summary>
	public IDriveInfoFactory DriveInfo => fileSystem.DriveInfo;

	/// <summary>
	/// Gets the file operations.
	/// </summary>
	public IFile File => fileSystem.File;

	/// <summary>
	/// Gets the file info factory.
	/// </summary>
	public IFileInfoFactory FileInfo => fileSystem.FileInfo;

	/// <summary>
	/// Gets the file stream factory.
	/// </summary>
	public IFileStreamFactory FileStream => fileSystem.FileStream;

	/// <summary>
	/// Gets the file system watcher factory.
	/// </summary>
	public IFileSystemWatcherFactory FileSystemWatcher => fileSystem.FileSystemWatcher;

	/// <summary>
	/// Gets the path operations.
	/// </summary>
	public IPath Path => fileSystem.Path;

	/// <summary>
	/// Gets the file version info factory.
	/// </summary>
	public IFileVersionInfoFactory FileVersionInfo => fileSystem.FileVersionInfo;
}
