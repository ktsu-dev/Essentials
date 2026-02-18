// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Abstractions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents the result of a validation operation, containing the overall validity status and any errors found.
/// </summary>
/// <param name="isValid">Whether the validation passed.</param>
/// <param name="errors">The validation errors, if any.</param>
public class ValidationResult(bool isValid, IReadOnlyList<ValidationError> errors)
{
	/// <summary>
	/// Gets a value indicating whether the validated object is valid.
	/// </summary>
	public bool IsValid { get; } = isValid;

	/// <summary>
	/// Gets the collection of validation errors found during validation.
	/// </summary>
	public IReadOnlyList<ValidationError> Errors { get; } = errors ?? [];

	/// <summary>
	/// Gets a successful validation result with no errors.
	/// </summary>
	public static ValidationResult Success { get; } = new(true, []);
}

/// <summary>
/// Represents a single validation error with information about what failed and why.
/// </summary>
/// <param name="propertyName">The name of the property that failed validation.</param>
/// <param name="errorMessage">The human-readable error message.</param>
/// <param name="errorCode">An optional error code for programmatic handling.</param>
public class ValidationError(string propertyName, string errorMessage, string? errorCode = null)
{
	/// <summary>
	/// Gets the name of the property or member that failed validation.
	/// </summary>
	public string PropertyName { get; } = Ensure.NotNull(propertyName);

	/// <summary>
	/// Gets the human-readable error message describing the validation failure.
	/// </summary>
	public string ErrorMessage { get; } = Ensure.NotNull(errorMessage);

	/// <summary>
	/// Gets an optional error code that can be used for programmatic error handling.
	/// </summary>
	public string? ErrorCode { get; } = errorCode;
}

/// <summary>
/// Exception thrown when validation fails and the caller requested an exception on failure.
/// </summary>
public class ValidationException : Exception
{
	/// <summary>
	/// Gets the validation result that caused this exception.
	/// </summary>
	public ValidationResult ValidationResult { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException"/> class.
	/// </summary>
	/// <param name="validationResult">The validation result containing the errors.</param>
	public ValidationException(ValidationResult validationResult)
		: base("Validation failed. See ValidationResult for details.") =>
		ValidationResult = Ensure.NotNull(validationResult);

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException"/> class with a custom message.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="validationResult">The validation result containing the errors.</param>
	public ValidationException(string message, ValidationResult validationResult)
		: base(message) =>
		ValidationResult = Ensure.NotNull(validationResult);

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException"/> class.
	/// </summary>
	public ValidationException()
		: base("Validation failed.") =>
		ValidationResult = new ValidationResult(false, []);

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException"/> class with a custom message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public ValidationException(string message)
		: base(message) =>
		ValidationResult = new ValidationResult(false, []);

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationException"/> class with a custom message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public ValidationException(string message, Exception innerException)
		: base(message, innerException) =>
		ValidationResult = new ValidationResult(false, []);
}

/// <summary>
/// Interface for validation providers that can validate objects of a specific type and return structured results.
/// </summary>
/// <typeparam name="T">The type of object to validate.</typeparam>
public interface IValidationProvider<in T>
{
	/// <summary>
	/// Validates the specified value and returns a structured result.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A <see cref="ValidationResult"/> containing the validation outcome and any errors.</returns>
	public ValidationResult Validate(T value);

	/// <summary>
	/// Checks whether the specified value is valid.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>True if the value passes all validation rules, false otherwise.</returns>
	public bool IsValid(T value) => Validate(value).IsValid;

	/// <summary>
	/// Validates the specified value and throws a <see cref="ValidationException"/> if validation fails.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <exception cref="ValidationException">Thrown when the value fails validation.</exception>
	public void ValidateAndThrow(T value)
	{
		ValidationResult result = Validate(value);
		if (!result.IsValid)
		{
			throw new ValidationException(result);
		}
	}

	/// <summary>
	/// Validates the specified value asynchronously and returns a structured result.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="ValidationResult"/> containing the validation outcome and any errors.</returns>
	public Task<ValidationResult> ValidateAsync(T value, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<ValidationResult>(cancellationToken)
			: Task.Run(() => Validate(value), cancellationToken);

	/// <summary>
	/// Checks whether the specified value is valid asynchronously.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>True if the value passes all validation rules, false otherwise.</returns>
	public Task<bool> IsValidAsync(T value, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled<bool>(cancellationToken)
			: Task.Run(() => IsValid(value), cancellationToken);

	/// <summary>
	/// Validates the specified value asynchronously and throws a <see cref="ValidationException"/> if validation fails.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <exception cref="ValidationException">Thrown when the value fails validation.</exception>
	public Task ValidateAndThrowAsync(T value, CancellationToken cancellationToken = default)
		=> cancellationToken.IsCancellationRequested
			? Task.FromCanceled(cancellationToken)
			: Task.Run(() => ValidateAndThrow(value), cancellationToken);
}
