// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Essentials.CacheProviders.InMemory;

using ktsu.Essentials;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

/// <summary>
/// An in-memory cache provider that stores key-value pairs with optional expiration support.
/// </summary>
/// <remarks>
/// <para>
/// A time-to-live is measured on a monotonic clock, not the wall clock, so a clock correction, a manual change
/// or a virtual machine resuming can neither keep an entry past its lifetime nor expire one early.
/// </para>
/// <para>
/// Expired entries are swept away as entries are set, so a cache of short-lived keys that are seldom read again
/// does not grow without bound. A sweep visits every entry, and runs once a second at most or once as many
/// entries have been set as the cache holds, whichever comes first, so its cost is spread across those sets.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the cache key.</typeparam>
/// <typeparam name="TValue">The type of the cached value.</typeparam>
public class InMemoryCacheProvider<TKey, TValue> : ICacheProvider<TKey, TValue> where TKey : notnull
{
	/// <summary>The fewest sets between two sweeps, so a small cache is not swept on every set.</summary>
	private const int SweepMinimumSets = 1024;

	/// <summary>The expiration of an entry that never expires.</summary>
	private const long Never = long.MaxValue;

	private readonly ConcurrentDictionary<TKey, CacheEntry> cache = new();
	private readonly Func<long> clock;
	private readonly long sweepInterval;
	private long lastSweep;
	private int setsSinceSweep;
	private int sweeping;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCacheProvider{TKey, TValue}"/> class.
	/// </summary>
	public InMemoryCacheProvider()
		: this(Stopwatch.GetTimestamp)
	{
	}

	/// <summary>
	/// Initializes a new instance over a clock other than <see cref="Stopwatch.GetTimestamp"/>, so tests can move
	/// time on rather than sleep.
	/// </summary>
	/// <param name="clock">Returns the current time in <see cref="Stopwatch"/> ticks, never going backwards.</param>
	internal InMemoryCacheProvider(Func<long> clock)
	{
		this.clock = clock;
		sweepInterval = Stopwatch.Frequency;
		lastSweep = clock();
	}

	/// <summary>Gets how many entries the cache holds, expired or not.</summary>
	internal int Count => cache.Count;

	/// <summary>
	/// Tries to get a cached value by key.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">When this method returns, contains the cached value if found, or the default value if not found.</param>
	/// <returns>True if the value was found in the cache and has not expired, false otherwise.</returns>
	public bool TryGet(TKey key, out TValue? value)
	{
		if (cache.TryGetValue(key, out CacheEntry? entry))
		{
			if (!entry.IsExpired(clock()))
			{
				value = entry.Value;
				return true;
			}

			// Entry has expired. Remove only the entry that was observed, so a fresh value another thread
			// has just set under the same key is left in place.
			RemoveEntry(key, entry);
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Sets a value in the cache with an optional expiration time.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="value">The value to cache.</param>
	/// <param name="expiration">The optional time-to-live for the cached entry. If null, the entry does not expire.</param>
	public void Set(TKey key, TValue value, TimeSpan? expiration = null)
	{
		long now = clock();
		cache[key] = new CacheEntry(value, ComputeExpiration(expiration, now));
		SweepIfDue(now);
	}

	/// <summary>
	/// Gets the value cached under <paramref name="key"/>, or adds and returns the one <paramref name="factory"/> makes.
	/// </summary>
	/// <remarks>
	/// Adds only if nothing live is cached by the time the factory returns: a value set while the factory ran is
	/// kept and returned, and callers racing on one missing key all get the one value left cached. The factory
	/// may therefore run and have its result discarded, as with <see cref="ConcurrentDictionary{TKey, TValue}"/>.
	/// </remarks>
	/// <param name="key">The cache key.</param>
	/// <param name="factory">Makes the value to add when none is cached.</param>
	/// <param name="expiration">The optional time-to-live for an added entry. If null, the entry does not expire.</param>
	/// <returns>The cached value.</returns>
	public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory, TimeSpan? expiration = null)
	{
		Ensure.NotNull(factory);

		while (true)
		{
			if (cache.TryGetValue(key, out CacheEntry? observed) && !observed.IsExpired(clock()))
			{
				return observed.Value;
			}

			TValue value = factory(key);
			long now = clock();
			CacheEntry created = new(value, ComputeExpiration(expiration, now));

			// Replace only what was seen to be missing or expired; anything else means another writer got there
			// first, and the loop returns its value.
			bool stored = observed is null ? cache.TryAdd(key, created) : cache.TryUpdate(key, created, observed);
			if (stored)
			{
				SweepIfDue(now);
				return value;
			}
		}
	}

	/// <summary>
	/// Removes a cached value by key.
	/// </summary>
	/// <param name="key">The cache key to remove.</param>
	/// <returns>
	/// True if the value was found and removed, false if it didn't exist. An expired entry is removed but
	/// reports false, matching <see cref="TryGet"/>, which treats it as absent.
	/// </returns>
	public bool Remove(TKey key) =>
		cache.TryRemove(key, out CacheEntry? entry) && !entry.IsExpired(clock());

	/// <summary>
	/// Clears all entries from the cache.
	/// </summary>
	public void Clear() => cache.Clear();

	/// <summary>
	/// Converts a time-to-live into an expiration on the clock, saturating rather than overflowing. A time-to-live
	/// too long to represent never expires, and one reaching before the clock's start has already expired.
	/// </summary>
	private static long ComputeExpiration(TimeSpan? expiration, long now)
	{
		if (expiration is null)
		{
			return Never;
		}

		// In double, which holds every TimeSpan's tick count to within a part in 2^53 and cannot overflow here.
		double ticks = expiration.Value.Ticks * ((double)Stopwatch.Frequency / TimeSpan.TicksPerSecond);
		double at = now + ticks;
		if (at >= Never)
		{
			return Never;
		}

		return at <= long.MinValue ? long.MinValue : (long)at;
	}

	private void SweepIfDue(long now)
	{
		int sets = Interlocked.Increment(ref setsSinceSweep);
		bool due = sets >= Math.Max(SweepMinimumSets, cache.Count) || now - Interlocked.Read(ref lastSweep) >= sweepInterval;
		if (!due || Interlocked.CompareExchange(ref sweeping, 1, 0) != 0)
		{
			return;
		}

		try
		{
			Interlocked.Exchange(ref setsSinceSweep, 0);
			Interlocked.Exchange(ref lastSweep, now);
			foreach (KeyValuePair<TKey, CacheEntry> pair in cache)
			{
				if (pair.Value.IsExpired(now))
				{
					RemoveEntry(pair.Key, pair.Value);
				}
			}
		}
		finally
		{
			Interlocked.Exchange(ref sweeping, 0);
		}
	}

	private void RemoveEntry(TKey key, CacheEntry entry)
	{
		KeyValuePair<TKey, CacheEntry> observed = new(key, entry);
#if NET5_0_OR_GREATER
		cache.TryRemove(observed);
#else
		((ICollection<KeyValuePair<TKey, CacheEntry>>)cache).Remove(observed);
#endif
	}

	private sealed class CacheEntry(TValue value, long expiration)
	{
		public TValue Value { get; } = value;

		public bool IsExpired(long now) => expiration <= now;
	}
}
