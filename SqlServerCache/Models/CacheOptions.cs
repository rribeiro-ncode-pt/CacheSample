using System;

namespace SqlServerCache.Models
{
    /// <summary>
    /// Represents configuration options for the SQL Server distributed cache.
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Gets or sets the table name for the cache. Default is "DistributedCache".
        /// </summary>
        public string TableName { get; set; } = "DistributedCache";

        /// <summary>
        /// Gets or sets the schema name for the cache table. Default is "dbo".
        /// </summary>
        public string SchemaName { get; set; } = "dbo";

        /// <summary>
        /// Gets or sets the command timeout in seconds. Default is 30 seconds.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether compression is enabled. Default is true.
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Gets or sets the default sliding expiration for cache items.
        /// </summary>
        public TimeSpan? DefaultSlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the default absolute expiration for cache items.
        /// </summary>
        public DateTimeOffset? DefaultAbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether automatic cleanup of expired items is enabled. Default is true.
        /// </summary>
        public bool AutoCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets the cleanup interval for expired items. Default is 5 minutes.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the fully qualified cache table name.
        /// </summary>
        public string FullTableName => $"[{SchemaName}].[{TableName}]";
    }
}
