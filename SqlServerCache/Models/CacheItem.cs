using System;

namespace SqlServerCache.Models
{
    /// <summary>
    /// Represents a cache item stored in the SQL Server distributed cache.
    /// </summary>
    internal class CacheItem
    {
        /// <summary>
        /// Gets or sets the primary key identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the cache key.
        /// </summary>
        public string CacheKey { get; set; }

        /// <summary>
        /// Gets or sets the serialized value.
        /// </summary>
        public byte[] Value { get; set; }

        /// <summary>
        /// Gets or sets the expiration date and time.
        /// </summary>
        public DateTimeOffset ExpiresAtTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether sliding expiration is enabled.
        /// </summary>
        public bool SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the absolute expiration date and time.
        /// </summary>
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets the last time the item was accessed.
        /// </summary>
        public DateTimeOffset LastAccessTime { get; set; }

        /// <summary>
        /// Gets or sets the time the item was created.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// Determines if the cache item is expired.
        /// </summary>
        /// <returns>true if the item is expired; otherwise, false.</returns>
        public bool IsExpired()
        {
            return DateTimeOffset.UtcNow >= ExpiresAtTime;
        }

        /// <summary>
        /// Updates the expiration time for sliding expiration.
        /// </summary>
        /// <param name="slidingExpirationInterval">The sliding expiration interval.</param>
        public void UpdateExpirationForSliding(TimeSpan slidingExpirationInterval)
        {
            if (!SlidingExpiration)
                return;

            var newExpiration = DateTimeOffset.UtcNow.Add(slidingExpirationInterval);
            
            // If there's an absolute expiration, don't extend beyond it
            if (AbsoluteExpiration.HasValue && newExpiration > AbsoluteExpiration.Value)
            {
                ExpiresAtTime = AbsoluteExpiration.Value;
            }
            else
            {
                ExpiresAtTime = newExpiration;
            }

            LastAccessTime = DateTimeOffset.UtcNow;
        }
    }
}
