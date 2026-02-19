// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;

/// <summary>
/// Exception thrown when a persistence provider operation fails.
/// </summary>
public sealed class PersistenceProviderException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PersistenceProviderException"/> class.
	/// </summary>
	public PersistenceProviderException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PersistenceProviderException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public PersistenceProviderException(string message) : base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PersistenceProviderException"/> class with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public PersistenceProviderException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
