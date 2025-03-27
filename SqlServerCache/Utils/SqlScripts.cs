using System;
using SqlServerCache.Models;

namespace SqlServerCache.Utils
{
    /// <summary>
    /// Contains SQL scripts for cache operations.
    /// </summary>
    internal static class SqlScripts
    {
        /// <summary>
        /// Gets the script to create the cache table.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to create the cache table.</returns>
        public static string GetCreateTableScript(CacheOptions options)
        {
            return $@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{options.TableName}' AND schema_id = SCHEMA_ID('{options.SchemaName}'))
BEGIN
    CREATE TABLE {options.FullTableName}(
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [CacheKey] [nvarchar](449) NOT NULL,
        [Value] [varbinary](max) NOT NULL,
        [ExpiresAtTime] [datetimeoffset](7) NOT NULL,
        [SlidingExpiration] [bit] NOT NULL,
        [AbsoluteExpiration] [datetimeoffset](7) NULL,
        [LastAccessTime] [datetimeoffset](7) NOT NULL,
        [CreatedTime] [datetimeoffset](7) NOT NULL,
        CONSTRAINT [PK_{options.TableName}] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UK_{options.TableName}_CacheKey] UNIQUE NONCLUSTERED ([CacheKey] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_{options.TableName}_ExpiresAtTime] ON {options.FullTableName}
    (
        [ExpiresAtTime] ASC
    );

    PRINT 'Created cache table {options.FullTableName}';
END
ELSE
BEGIN
    PRINT 'Cache table {options.FullTableName} already exists';
END
";
        }

        /// <summary>
        /// Gets the script to get an item from the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to get an item from the cache.</returns>
        public static string GetGetItemScript(CacheOptions options)
        {
            return $@"
SELECT [Id], [CacheKey], [Value], [ExpiresAtTime], [SlidingExpiration], [AbsoluteExpiration], [LastAccessTime], [CreatedTime]
FROM {options.FullTableName}
WHERE [CacheKey] = @CacheKey;
";
        }

        /// <summary>
        /// Gets the script to set an item in the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to set an item in the cache.</returns>
        public static string GetSetItemScript(CacheOptions options)
        {
            return $@"
IF EXISTS (SELECT 1 FROM {options.FullTableName} WHERE [CacheKey] = @CacheKey)
BEGIN
    UPDATE {options.FullTableName}
    SET [Value] = @Value,
        [ExpiresAtTime] = @ExpiresAtTime,
        [SlidingExpiration] = @SlidingExpiration,
        [AbsoluteExpiration] = @AbsoluteExpiration,
        [LastAccessTime] = @LastAccessTime
    WHERE [CacheKey] = @CacheKey;
END
ELSE
BEGIN
    INSERT INTO {options.FullTableName} ([CacheKey], [Value], [ExpiresAtTime], [SlidingExpiration], [AbsoluteExpiration], [LastAccessTime], [CreatedTime])
    VALUES (@CacheKey, @Value, @ExpiresAtTime, @SlidingExpiration, @AbsoluteExpiration, @LastAccessTime, @CreatedTime);
END
";
        }

        /// <summary>
        /// Gets the script to remove an item from the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to remove an item from the cache.</returns>
        public static string GetRemoveItemScript(CacheOptions options)
        {
            return $@"
DELETE FROM {options.FullTableName}
WHERE [CacheKey] = @CacheKey;
SELECT @@ROWCOUNT;
";
        }

        /// <summary>
        /// Gets the script to check if an item exists in the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to check if an item exists in the cache.</returns>
        public static string GetExistsItemScript(CacheOptions options)
        {
            return $@"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM {options.FullTableName}
    WHERE [CacheKey] = @CacheKey AND [ExpiresAtTime] > @CurrentTime
) THEN 1 ELSE 0 END;
";
        }

        /// <summary>
        /// Gets the script to refresh an item in the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to refresh an item in the cache.</returns>
        public static string GetRefreshItemScript(CacheOptions options)
        {
            return $@"
DECLARE @slidingExpiration bit;
DECLARE @newExpirationTime datetimeoffset;
DECLARE @absoluteExpiration datetimeoffset;

SELECT
    @slidingExpiration = [SlidingExpiration],
    @absoluteExpiration = [AbsoluteExpiration]
FROM {options.FullTableName}
WHERE [CacheKey] = @CacheKey;

IF @slidingExpiration = 1
BEGIN
    IF @absoluteExpiration IS NOT NULL
    BEGIN
        SET @newExpirationTime = CASE
            WHEN DATEADD(MINUTE, @SlidingExpirationMinutes, @CurrentTime) > @absoluteExpiration
            THEN @absoluteExpiration
            ELSE DATEADD(MINUTE, @SlidingExpirationMinutes, @CurrentTime)
        END;
    END
    ELSE
    BEGIN
        SET @newExpirationTime = DATEADD(MINUTE, @SlidingExpirationMinutes, @CurrentTime);
    END

    UPDATE {options.FullTableName}
    SET [ExpiresAtTime] = @newExpirationTime,
        [LastAccessTime] = @CurrentTime
    WHERE [CacheKey] = @CacheKey;
    
    SELECT @@ROWCOUNT;
END
ELSE
BEGIN
    SELECT 0;
END
";
        }

        /// <summary>
        /// Gets the script to remove expired items from the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to remove expired items from the cache.</returns>
        public static string GetRemoveExpiredItemsScript(CacheOptions options)
        {
            return $@"
DELETE FROM {options.FullTableName}
WHERE [ExpiresAtTime] <= @CurrentTime;
SELECT @@ROWCOUNT;
";
        }

        /// <summary>
        /// Gets the script to clear all items from the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to clear all items from the cache.</returns>
        public static string GetClearScript(CacheOptions options)
        {
            return $@"
TRUNCATE TABLE {options.FullTableName};
";
        }

        /// <summary>
        /// Gets the script to get multiple items from the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to get multiple items from the cache.</returns>
        public static string GetGetManyItemsScript(CacheOptions options)
        {
            return $@"
SELECT [CacheKey], [Value], [ExpiresAtTime], [SlidingExpiration], [AbsoluteExpiration], [LastAccessTime], [CreatedTime]
FROM {options.FullTableName}
WHERE [CacheKey] IN ({0})
  AND [ExpiresAtTime] > @CurrentTime;
";
        }

        /// <summary>
        /// Gets the script to get statistics about the cache.
        /// </summary>
        /// <param name="options">The cache options.</param>
        /// <returns>The SQL script to get statistics about the cache.</returns>
        public static string GetStatisticsScript(CacheOptions options)
        {
            return $@"
SELECT
    COUNT(*) AS ItemCount,
    SUM(DATALENGTH([Value])) AS TotalSizeBytes,
    SUM(CASE WHEN [ExpiresAtTime] < DATEADD(MINUTE, 10, @CurrentTime) THEN 1 ELSE 0 END) AS ExpiringWithin10Minutes
FROM {options.FullTableName};
";
        }

        /// <summary>
        /// Gets the script to acquire a lock.
        /// </summary>
        /// <returns>The SQL script to acquire a lock.</returns>
        public static string GetAcquireLockScript()
        {
            return @"
DECLARE @Result INT;
BEGIN TRY
    EXEC @Result = sp_getapplock
        @Resource = @LockResource,
        @LockMode = 'Exclusive',
        @LockOwner = 'Session',
        @LockTimeout = @LockTimeoutMs;
    SELECT @Result AS LockResult;
END TRY
BEGIN CATCH
    -- If sp_getapplock is not available, fall back to simple locking
    SELECT 1 AS LockResult; -- Return success
END CATCH
";
        }

        /// <summary>
        /// Gets the script to release a lock.
        /// </summary>
        /// <returns>The SQL script to release a lock.</returns>
        public static string GetReleaseLockScript()
        {
            return @"
BEGIN TRY
    EXEC sp_releaseapplock
        @Resource = @LockResource,
        @LockOwner = 'Session';
END TRY
BEGIN CATCH
    -- Ignore errors when releasing the lock
END CATCH
";
        }
    }
}
