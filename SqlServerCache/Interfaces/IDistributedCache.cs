using System;
using System.Collections.Generic;

namespace SqlServerCache.Interfaces
{
    /// <summary>
    /// Represents a distributed cache that can be shared across multiple servers.
    /// </summary>
    public interface IDistributedCache
    {
        /// <summary>
        /// Gets an item from the cache with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <returns>The cached item, or null if the item doesn't exist.</returns>
        T Get<T>(string key) where T : class;

        /// <summary>
        /// Tries to get an item from the cache with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The cached item if found; otherwise, null.</param>
        /// <returns>true if the item was found; otherwise, false.</returns>
        bool TryGetValue<T>(string key, out T value) where T : class;

        /// <summary>
        /// Adds or replaces an item in the cache with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The item to cache.</param>
        /// <param name="slidingExpiration">The sliding expiration timespan.</param>
        /// <param name="absoluteExpiration">The absolute expiration date.</param>
        void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) where T : class;

        /// <summary>
        /// Removes an item from the cache with the specified key.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>true if the item was removed; otherwise, false.</returns>
        bool Remove(string key);

        /// <summary>
        /// Checks if an item exists in the cache with the specified key.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>true if the item exists; otherwise, false.</returns>
        bool Exists(string key);

        /// <summary>
        /// Gets an item from the cache or adds it if it doesn't exist.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="valueFactory">A function that produces the value if it doesn't exist.</param>
        /// <param name="slidingExpiration">The sliding expiration timespan.</param>
        /// <param name="absoluteExpiration">The absolute expiration date.</param>
        /// <returns>The cached item.</returns>
        T GetOrAdd<T>(string key, Func<T> valueFactory, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) where T : class;

        /// <summary>
        /// Refreshes an item in the cache, resetting its sliding expiration.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>true if the item was refreshed; otherwise, false.</returns>
        bool Refresh(string key);

        /// <summary>
        /// Gets multiple items from the cache with the specified keys.
        /// </summary>
        /// <typeparam name="T">The type of the items.</typeparam>
        /// <param name="keys">The cache keys.</param>
        /// <returns>A dictionary of keys and their corresponding cached items (if found).</returns>
        IDictionary<string, T> GetMany<T>(IEnumerable<string> keys) where T : class;

        /// <summary>
        /// Adds or replaces multiple items in the cache.
        /// </summary>
        /// <typeparam name="T">The type of the items.</typeparam>
        /// <param name="items">A dictionary of keys and values to cache.</param>
        /// <param name="slidingExpiration">The sliding expiration timespan.</param>
        /// <param name="absoluteExpiration">The absolute expiration date.</param>
        void SetMany<T>(IDictionary<string, T> items, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) where T : class;

        /// <summary>
        /// Removes expired items from the cache.
        /// </summary>
        void FlushExpired();

        /// <summary>
        /// Removes all items from the cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets statistics about the cache.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        CacheStatistics GetStatistics();
    }

    /// <summary>
    /// Represents statistics about the cache.
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Gets the number of items in the cache.
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Gets the total size of all cached items in bytes.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Gets the number of items that are expiring soon.
        /// </summary>
        public int ExpiringWithin10Minutes { get; set; }

        /// <summary>
        /// Gets the cache hit ratio (hits / (hits + misses)).
        /// </summary>
        public double CacheHitRatio { get; set; }
    }
}
