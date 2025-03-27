# SQL Server Distributed Cache for .NET Framework

A robust SQL Server-based distributed caching implementation for .NET Framework 4.8 applications. This library provides a distributed caching solution that can be shared across multiple application instances, making it ideal for web farms and load-balanced environments.

## Latest Updates

- **Fixed Value Type Constraints**: Added `IntegerBox` reference type wrapper to allow caching of primitive value types.
- **Improved Concurrency Handling**: Enhanced thread synchronization to prevent race conditions.
- **Enhanced Error Handling**: Better handling of null values in cache statistics.
- **Command-Line Test Support**: Added direct test execution via command line parameters.

## Features

- **Distributed Caching**: Cache data is stored in SQL Server, making it accessible to multiple application instances.
- **Concurrency Support**: Built-in distributed locking mechanisms to handle concurrent access.
- **Flexible Expiration Policies**: Support for both sliding and absolute expiration.
- **Atomic Operations**: Ensure data consistency with SQL Server transactions.
- **Performance Monitoring**: Built-in statistics tracking for cache hits, misses, and item counts.
- **Automatic Cleanup**: Background task to remove expired items.
- **Configurable**: Customizable table names, schema names, command timeouts, and more.
- **Thread-Safe**: All operations are safe for concurrent access from multiple threads.

## Project Structure

The solution consists of two main projects:

1. **SqlServerCache** - The core library implementing the distributed cache functionality.
2. **CacheSample** - A console application demonstrating the cache usage.

## Prerequisites

- .NET Framework 4.8
- SQL Server (2012 or later)
- System.Data.SqlClient package

## SQL Server Setup

You can use any existing SQL Server instance, or run SQL Server in Docker:

```bash
docker pull mcr.microsoft.com/mssql/server:2022-latest

docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 --name sqlserver2022 -d mcr.microsoft.com/mssql/server:2022-latest
```

The application will automatically create the required database and tables on first run.

## Getting Started

1. Clone the repository
2. Update the connection string in `CacheSample/App.config` to point to your SQL Server instance
3. Build the solution
4. Run the CacheSample console application

## Basic Usage

```csharp
// Create cache options
var options = new CacheOptions
{
    TableName = "DistributedCache",
    DefaultSlidingExpiration = TimeSpan.FromMinutes(10)
};

// Create cache instance
using (var cache = new SqlServerDistributedCache(connectionString, options))
{
    // Store an item in cache
    cache.Set("myKey", myObject, TimeSpan.FromMinutes(30));
    
    // Retrieve an item from cache
    var cachedObject = cache.Get<MyType>("myKey");
    
    // Check if an item exists
    bool exists = cache.Exists("myKey");
    
    // Get or add an item 
    var result = cache.GetOrAdd("myKey", () => {
        // This lambda is only executed if the item isn't in the cache
        return ExpensiveOperation();
    });
    
    // Remove an item
    cache.Remove("myKey");
}
```

## Demo Application

The included CacheSample console application demonstrates:

1. **Basic Operations**: Storing, retrieving, and removing cache items.
2. **Concurrency Testing**: Multiple threads accessing and modifying cached data.
3. **Performance Benchmarking**: Testing cache performance with different data sizes.

### Running Tests via Command Line

You can run specific tests directly using command-line parameters:

```bash
# Run the concurrency test
CacheSample.exe concurrency

# Run the performance benchmark
CacheSample.exe performance
```

### Performance Results

From recent benchmarks on typical hardware:

```
Small Data (1 KB)
- Write: ~4.18 ms per operation (239 ops/sec)
- Read: ~0.60 ms per operation (1,667 ops/sec)

Medium Data (10 KB)
- Write: ~3.52 ms per operation (284 ops/sec)
- Read: ~0.66 ms per operation (1,515 ops/sec)

Large Data (100 KB)
- Write: ~4.00 ms per operation (250 ops/sec)
- Read: ~1.58 ms per operation (633 ops/sec)
```

These results show that read operations are significantly faster than write operations, which is ideal for frequently read but infrequently updated cache items.

## Implementation Details

### Cache Table Structure

```sql
CREATE TABLE [dbo].[DistributedCache](
    [Id] [bigint] IDENTITY(1,1) NOT NULL,
    [CacheKey] [nvarchar](449) NOT NULL,
    [Value] [varbinary](max) NOT NULL,
    [ExpiresAtTime] [datetimeoffset](7) NOT NULL,
    [SlidingExpiration] [bit] NOT NULL,
    [AbsoluteExpiration] [datetimeoffset](7) NULL,
    [LastAccessTime] [datetimeoffset](7) NOT NULL,
    [CreatedTime] [datetimeoffset](7) NOT NULL,
    CONSTRAINT [PK_DistributedCache] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UK_DistributedCache_CacheKey] UNIQUE NONCLUSTERED ([CacheKey] ASC)
)
```

### Serialization

By default, BinaryFormatter is used for serialization. Objects to be cached must be marked with the `[Serializable]` attribute. You can implement custom serialization by creating a class that implements `ICacheSerializer`.

### Distributed Locking

SQL Server application locks (via `sp_getapplock`) are used to ensure cache operations are atomic, even across multiple application instances. For local concurrency (multiple threads within the same process), a static lock object provides thread safety.

#### Using Reference Types with Cache

Since the cache interface requires reference types (`where T : class`), primitive value types must be wrapped:

```csharp
// Create a reference type wrapper for int
[Serializable]
public class IntegerBox
{
    public int Value { get; set; }
    
    // Implicit conversion for easier usage
    public static implicit operator int(IntegerBox box) => box?.Value ?? 0;
}

// Store in cache
cache.Set("counter", new IntegerBox { Value = 42 });

// Retrieve from cache
var box = cache.Get<IntegerBox>("counter");
int value = box?.Value ?? 0;
```

## Extending the Cache

### Custom Serialization

```csharp
public class JsonSerializer : ICacheSerializer
{
    public byte[] Serialize<T>(T value) where T : class
    {
        if (value == null) return null;
        var json = JsonConvert.SerializeObject(value);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] data) where T : class
    {
        if (data == null || data.Length == 0) return default;
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json);
    }
}

// Using the custom serializer
var cache = new SqlServerDistributedCache(connectionString, options, new JsonSerializer());
```

### Tiered Caching

For better performance, you can implement a tiered approach with a local in-memory cache:

```csharp
public class TieredCache : IDistributedCache
{
    private readonly SqlServerDistributedCache _distributedCache;
    private readonly MemoryCache _localCache;
    
    // Implementation details...
}
```

## Performance Considerations

- SQL Server cache is optimized for distributed scenarios, not raw performance
- Read operations (typically 0.6-1.6ms) are significantly faster than write operations (3-5ms)
- Network latency and database load significantly impact performance
- Consider using connection pooling to optimize connections
- Index the ExpiresAtTime column for faster cleanup operations
- For high-throughput scenarios, consider a tiered caching approach combining local memory cache with the distributed SQL Server cache

## Error Handling

The library includes robust error handling for common scenarios:

- **Table Not Found**: Automatically creates required tables
- **Database Connection Issues**: Detailed error messages for connection problems
- **Concurrency Conflicts**: Managed through distributed locking
- **Null References**: Handled gracefully with fallback to default values

## License

This project is licensed under the MIT License - see the LICENSE file for details.
