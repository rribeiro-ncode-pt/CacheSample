using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlServerCache.Interfaces;
using SqlServerCache.Models;
using SqlServerCache.Serialization;
using SqlServerCache.Utils;

namespace SqlServerCache
{
    /// <summary>
    /// Distributed cache implementation using SQL Server as the backing store.
    /// </summary>
    public class SqlServerDistributedCache : IDistributedCache, IDisposable
    {
        private readonly string _connectionString;
        private readonly CacheOptions _options;
        private readonly ICacheSerializer _serializer;
        private Timer _cleanupTimer;
        private long _cacheHits;
        private long _cacheMisses;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerDistributedCache"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string to the SQL Server database.</param>
        /// <param name="options">The cache options. If null, default options will be used.</param>
        /// <param name="serializer">The serializer to use. If null, a BinaryFormatter serializer will be used.</param>
        public SqlServerDistributedCache(string connectionString, CacheOptions options = null, ICacheSerializer serializer = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _options = options ?? new CacheOptions();
            _serializer = serializer ?? new BinaryFormatterSerializer();

            EnsureDatabaseObjectsExist();

            if (_options.AutoCleanup)
            {
                StartCleanupTimer();
            }
        }

        /// <inheritdoc />
        public T Get<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetGetItemScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddWithValue("@CacheKey", key);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var expiresAt = reader.GetDateTimeOffset(reader.GetOrdinal("ExpiresAtTime"));
                            if (expiresAt <= DateTimeOffset.UtcNow)
                            {
                                // Item is expired
                                Interlocked.Increment(ref _cacheMisses);
                                return default;
                            }

                            var slidingExpiration = reader.GetBoolean(reader.GetOrdinal("SlidingExpiration"));
                            var bytes = (byte[])reader["Value"];
                            var result = _serializer.Deserialize<T>(bytes);

                            // If it has sliding expiration, update the expiration time
                            if (slidingExpiration)
                            {
                                var slidingExpirationInterval = expiresAt - DateTimeOffset.UtcNow;
                                UpdateItemExpiration(connection, key, slidingExpirationInterval);
                            }

                            Interlocked.Increment(ref _cacheHits);
                            return result;
                        }
                    }
                }
            }

            Interlocked.Increment(ref _cacheMisses);
            return default;
        }

        /// <inheritdoc />
        public bool TryGetValue<T>(string key, out T value) where T : class
        {
            value = default;

            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                value = Get<T>(key);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            // Use default expirations if not specified
            slidingExpiration = slidingExpiration ?? _options.DefaultSlidingExpiration;
            absoluteExpiration = absoluteExpiration ?? _options.DefaultAbsoluteExpiration;

            // At least one expiration policy must be set
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                slidingExpiration = TimeSpan.FromMinutes(30); // Default to 30 minutes sliding
            }

            DateTimeOffset expiresAt;
            
            if (slidingExpiration.HasValue)
            {
                expiresAt = DateTimeOffset.UtcNow.Add(slidingExpiration.Value);
                
                // If absolute expiration is set and it's earlier, use it
                if (absoluteExpiration.HasValue && absoluteExpiration.Value < expiresAt)
                {
                    expiresAt = absoluteExpiration.Value;
                    slidingExpiration = null; // No sliding expiration needed since absolute will occur first
                }
            }
            else
            {
                // Only absolute expiration is set
                expiresAt = absoluteExpiration.Value;
            }

            // Serialize the value
            byte[] bytes = _serializer.Serialize(value);

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetSetItemScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddWithValue("@CacheKey", key);
                    command.Parameters.AddWithValue("@Value", bytes);
                    command.Parameters.AddWithValue("@ExpiresAtTime", expiresAt);
                    command.Parameters.AddWithValue("@SlidingExpiration", slidingExpiration.HasValue);
                    command.Parameters.AddWithValue("@AbsoluteExpiration", absoluteExpiration ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@LastAccessTime", DateTimeOffset.UtcNow);
                    command.Parameters.AddWithValue("@CreatedTime", DateTimeOffset.UtcNow);

                    command.ExecuteNonQuery();
                }
            }
        }

        /// <inheritdoc />
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetRemoveItemScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddWithValue("@CacheKey", key);

                    return (int)command.ExecuteScalar() > 0;
                }
            }
        }

        /// <inheritdoc />
        public bool Exists(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetExistsItemScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddWithValue("@CacheKey", key);
                    command.Parameters.AddWithValue("@CurrentTime", DateTimeOffset.UtcNow);

                    return (int)command.ExecuteScalar() > 0;
                }
            }
        }

        /// <inheritdoc />
        public T GetOrAdd<T>(string key, Func<T> valueFactory, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) where T : class
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));

            T value = Get<T>(key);
            
            if (value != null)
                return value;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Use a lock to prevent multiple instances from creating the same item
                return ConcurrencyHelper.ExecuteWithLock<T>(connection, key, () =>
                {
                    // Check again inside the lock
                    T cachedValue = Get<T>(key);
                    if (cachedValue != null)
                        return cachedValue;

                    // Not in cache, create new value
                    T newValue = valueFactory();
                    
                    // Add to cache
                    Set(key, newValue, slidingExpiration, absoluteExpiration);
                    
                    return newValue;
                });
            }
        }

        /// <inheritdoc />
        public bool Refresh(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetRefreshItemScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddWithValue("@CacheKey", key);
                    command.Parameters.AddWithValue("@CurrentTime", DateTimeOffset.UtcNow);
                    command.Parameters.AddWithValue("@SlidingExpirationMinutes", 30); // Default 30 minutes

                    return (int)command.ExecuteScalar() > 0;
                }
            }
        }

        /// <inheritdoc />
        public IDictionary<string, T> GetMany<T>(IEnumerable<string> keys) where T : class
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            var result = new Dictionary<string, T>();
            var keysList = keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
            
            if (keysList.Count == 0)
                return result;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                var paramNames = new List<string>();
                var parameters = new List<SqlParameter>();

                for (int i = 0; i < keysList.Count; i++)
                {
                    string paramName = $"@Key{i}";
                    paramNames.Add(paramName);
                    parameters.Add(new SqlParameter(paramName, keysList[i]));
                }

                string script = SqlScripts.GetGetManyItemsScript(_options)
                    .Replace("{0}", string.Join(", ", paramNames));

                using (var command = new SqlCommand(script, connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddRange(parameters.ToArray());
                    command.Parameters.AddWithValue("@CurrentTime", DateTimeOffset.UtcNow);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader.GetString(0);
                            byte[] bytes = (byte[])reader["Value"];
                            T value = _serializer.Deserialize<T>(bytes);
                            result[key] = value;
                            
                            // Update hit counter
                            Interlocked.Increment(ref _cacheHits);
                            
                            // Handle sliding expiration
                            bool slidingExpiration = reader.GetBoolean(2);
                            if (slidingExpiration)
                            {
                                DateTimeOffset expiresAt = reader.GetDateTimeOffset(3);
                                var slidingExpirationInterval = expiresAt - DateTimeOffset.UtcNow;
                                UpdateItemExpiration(connection, key, slidingExpirationInterval);
                            }
                        }
                    }
                }
            }

            // Add misses to counter
            Interlocked.Add(ref _cacheMisses, keysList.Count - result.Count);

            return result;
        }

        /// <inheritdoc />
        public void SetMany<T>(IDictionary<string, T> items, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) where T : class
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (items.Count == 0)
                return;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in items)
                        {
                            Set(item.Key, item.Value, slidingExpiration, absoluteExpiration);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void FlushExpired()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetRemoveExpiredItemsScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddWithValue("@CurrentTime", DateTimeOffset.UtcNow);
                    int removed = (int)command.ExecuteScalar();
                }
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetClearScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <inheritdoc />
        public CacheStatistics GetStatistics()
        {
            try
            {
                var stats = new CacheStatistics();
                
                // Add cache hit ratio
                long hits = Interlocked.Read(ref _cacheHits);
                long misses = Interlocked.Read(ref _cacheMisses);
                long total = hits + misses;
                
                stats.CacheHitRatio = total == 0 ? 0 : (double)hits / total;

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // First check if the table exists
                    bool tableExists = false;
                    using (var checkCommand = new SqlCommand($"IF EXISTS (SELECT 1 FROM sys.tables WHERE name = '{_options.TableName}' AND schema_id = SCHEMA_ID('{_options.SchemaName}')) SELECT 1 ELSE SELECT 0", connection))
                    {
                        tableExists = Convert.ToBoolean(checkCommand.ExecuteScalar());
                    }

                    if (!tableExists)
                    {
                        // Table doesn't exist yet, return default stats
                        return stats;
                    }
                    
                    // If table exists, get statistics
                    using (var command = new SqlCommand(SqlScripts.GetStatisticsScript(_options), connection))
                    {
                        command.CommandTimeout = _options.CommandTimeout;
                        command.Parameters.AddWithValue("@CurrentTime", DateTimeOffset.UtcNow);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stats.ItemCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                stats.TotalSizeBytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                                stats.ExpiringWithin10Minutes = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                            }
                        }
                    }
                }

                return stats;
            }
            catch (Exception ex)
            {
                // Log the error if a logger is available
                Console.WriteLine($"Error in GetStatistics: {ex.Message}");
                
                // Return a default statistics object rather than null
                return new CacheStatistics 
                { 
                    ItemCount = 0, 
                    TotalSizeBytes = 0, 
                    ExpiringWithin10Minutes = 0,
                    CacheHitRatio = 0
                };
            }
        }

        /// <summary>
        /// Disposes the resources used by the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the resources used by the instance.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cleanupTimer?.Dispose();
                    _cleanupTimer = null;
                }

                _disposed = true;
            }
        }

        private void EnsureDatabaseObjectsExist()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(SqlScripts.GetCreateTableScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.ExecuteNonQuery();
                }
            }
        }

        private void StartCleanupTimer()
        {
            _cleanupTimer = new Timer(
                state => CleanupExpiredItems(),
                null,
                TimeSpan.Zero,
                _options.CleanupInterval);
        }

        private void CleanupExpiredItems()
        {
            try
            {
                FlushExpired();
            }
            catch
            {
                // Ignore errors in the background timer
            }
        }

        private void UpdateItemExpiration(SqlConnection connection, string key, TimeSpan slidingExpirationInterval)
        {
            try
            {
                // Simple refresh to update the item's expiration time
                using (var command = new SqlCommand(SqlScripts.GetRefreshItemScript(_options), connection))
                {
                    command.CommandTimeout = _options.CommandTimeout;
                    command.Parameters.AddWithValue("@CacheKey", key);
                    command.Parameters.AddWithValue("@CurrentTime", DateTimeOffset.UtcNow);
                    command.Parameters.AddWithValue("@SlidingExpirationMinutes", slidingExpirationInterval.TotalMinutes);
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // Ignore errors when refreshing expiration
            }
        }
    }
}
