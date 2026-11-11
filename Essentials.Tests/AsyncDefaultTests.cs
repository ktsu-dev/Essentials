// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The derived async defaults of <see cref="IValidationProvider{T}"/> and <see cref="ICacheProvider{TKey, TValue}"/>
/// are built on the async primitives, not the sync members (issue #196). Each double here overrides only the
/// async primitive and makes the sync one throw, so a default that reaches the sync side fails the test.
/// </summary>
[TestClass]
public class AsyncDefaultTests
{
	public TestContext TestContext { get; set; } = null!;

	[TestMethod]
	public async Task IsValidAsync_Uses_An_Overridden_ValidateAsync()
	{
		IValidationProvider<string> validator = new AsyncOnlyValidator();

		Assert.IsFalse(await validator.IsValidAsync("taken", TestContext.CancellationToken).ConfigureAwait(false));
		Assert.IsTrue(await validator.IsValidAsync("free", TestContext.CancellationToken).ConfigureAwait(false));
	}

	[TestMethod]
	public async Task ValidateAndThrowAsync_Uses_An_Overridden_ValidateAsync()
	{
		IValidationProvider<string> validator = new AsyncOnlyValidator();

		ValidationException exception = await Assert.ThrowsExactlyAsync<ValidationException>(
			() => validator.ValidateAndThrowAsync("taken", TestContext.CancellationToken)).ConfigureAwait(false);
		Assert.AreEqual("Name", exception.ValidationResult.Errors[0].PropertyName);

		await validator.ValidateAndThrowAsync("free", TestContext.CancellationToken).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task GetAsync_Uses_An_Overridden_TryGetAsync()
	{
		AsyncOnlyCache cache = new();
		cache.Entries["present"] = 7;
		cache.Entries["null"] = null;
		ICacheProvider<string, int?> provider = cache;

		Assert.AreEqual(7, await provider.GetAsync("present", TestContext.CancellationToken).ConfigureAwait(false));
		Assert.IsNull(await provider.GetAsync("null", TestContext.CancellationToken).ConfigureAwait(false));
		await Assert.ThrowsExactlyAsync<KeyNotFoundException>(
			() => provider.GetAsync("missing", TestContext.CancellationToken)).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task GetOrAddAsync_Uses_Overridden_TryGetAsync_And_SetAsync()
	{
		AsyncOnlyCache cache = new();
		cache.Entries["present"] = 7;
		cache.Entries["null"] = null;
		ICacheProvider<string, int?> provider = cache;
		int factoryCalls = 0;
		int? Factory(string key)
		{
			factoryCalls++;
			return key.Length;
		}

		Assert.AreEqual(7, await provider.GetOrAddAsync("present", Factory, cancellationToken: TestContext.CancellationToken).ConfigureAwait(false));
		Assert.IsNull(await provider.GetOrAddAsync("null", Factory, cancellationToken: TestContext.CancellationToken).ConfigureAwait(false));
		Assert.AreEqual(0, factoryCalls, "A cached entry, even a null one, must not invoke the factory");

		TimeSpan expiration = TimeSpan.FromMinutes(5);
		Assert.AreEqual(7, await provider.GetOrAddAsync("missing", Factory, expiration, TestContext.CancellationToken).ConfigureAwait(false));
		Assert.AreEqual(1, factoryCalls);
		Assert.AreEqual(7, cache.Entries["missing"], "The created value is stored through SetAsync");
		Assert.AreEqual(expiration, cache.LastExpiration);
	}

	private sealed class AsyncOnlyValidator : IValidationProvider<string>
	{
		public ValidationResult Validate(string value) => throw new NotSupportedException("Only ValidateAsync is supported.");

		public Task<ValidationResult> ValidateAsync(string value, CancellationToken cancellationToken = default)
			=> Task.FromResult(value == "taken"
				? new ValidationResult(false, [new ValidationError("Name", "That name is already taken.")])
				: ValidationResult.Success);
	}

	private sealed class AsyncOnlyCache : ICacheProvider<string, int?>
	{
		public Dictionary<string, int?> Entries { get; } = [];

		public TimeSpan? LastExpiration { get; private set; }

		public bool TryGet(string key, out int? value) => throw new NotSupportedException("Only TryGetAsync is supported.");

		public void Set(string key, int? value, TimeSpan? expiration = null) => throw new NotSupportedException("Only SetAsync is supported.");

		public bool Remove(string key) => throw new NotSupportedException();

		public void Clear() => throw new NotSupportedException();

		public Task<(bool Found, int? Value)> TryGetAsync(string key, CancellationToken cancellationToken = default)
			=> Task.FromResult(Entries.TryGetValue(key, out int? value) ? (true, value) : (false, null));

		public Task SetAsync(string key, int? value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
		{
			Entries[key] = value;
			LastExpiration = expiration;
			return Task.CompletedTask;
		}
	}
}
