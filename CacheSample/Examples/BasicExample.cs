using System;
using System.Threading;
using CacheSample.Models;
using SqlServerCache.Interfaces;

namespace CacheSample.Examples
{
    /// <summary>
    /// Demonstrates basic cache operations.
    /// </summary>
    public static class BasicExample
    {
        /// <summary>
        /// Runs the basic cache operations example.
        /// </summary>
        /// <param name="cache">The distributed cache instance.</param>
        public static void Run(IDistributedCache cache)
        {
            Console.WriteLine("\n========== BASIC CACHE OPERATIONS ==========\n");
            
            try
            {
                // Create a sample product
                Console.WriteLine("Creating a sample product...");
                var product = new Product
                {
                    Id = 1,
                    Name = "Sample Product",
                    Price = 19.99m,
                    Description = "This is a sample product for caching demonstration",
                    CategoryId = 1
                };
                
                // Display the product
                Console.WriteLine($"Original product: {product}");
                
                // Add to cache with sliding expiration
                Console.WriteLine("\nAdding product to cache with 2 minute sliding expiration...");
                cache.Set("product:1", product, TimeSpan.FromMinutes(2));
                Console.WriteLine("Product added to cache.");
                
                // Get from cache
                Console.WriteLine("\nRetrieving product from cache...");
                var cachedProduct = cache.Get<Product>("product:1");
                Console.WriteLine($"Retrieved product: {cachedProduct}");
                
                // Check if exists
                Console.WriteLine("\nChecking if product exists in cache...");
                bool exists = cache.Exists("product:1");
                Console.WriteLine($"Product exists in cache: {exists}");
                
                // Demonstrate cache hit
                Console.WriteLine("\nRetrieving product from cache again (should be a cache hit)...");
                cachedProduct = cache.Get<Product>("product:1");
                Console.WriteLine($"Retrieved product: {cachedProduct}");
                
                // Remove from cache
                Console.WriteLine("\nRemoving product from cache...");
                bool removed = cache.Remove("product:1");
                Console.WriteLine($"Product {(removed ? "was" : "was not")} removed from cache.");
                
                // Check if exists after removal
                Console.WriteLine("\nChecking if product exists in cache after removal...");
                exists = cache.Exists("product:1");
                Console.WriteLine($"Product exists in cache: {exists}");
                
                // Demonstrate GetOrAdd
                Console.WriteLine("\nDemonstrating GetOrAdd (item doesn't exist yet)...");
                var product2 = cache.GetOrAdd("product:2", () =>
                {
                    Console.WriteLine("Factory method called - item not in cache");
                    return new Product
                    {
                        Id = 2,
                        Name = "Second Product",
                        Price = 29.99m,
                        Description = "Another sample product created with GetOrAdd",
                        CategoryId = 2
                    };
                }, TimeSpan.FromMinutes(5));
                
                Console.WriteLine($"GetOrAdd result: {product2}");
                
                // Call GetOrAdd again to demonstrate cache hit
                Console.WriteLine("\nCalling GetOrAdd again (item should be in cache)...");
                var product2Again = cache.GetOrAdd("product:2", () =>
                {
                    Console.WriteLine("Factory method called again - THIS SHOULD NOT APPEAR");
                    return new Product
                    {
                        Id = 2,
                        Name = "Modified Product",
                        Price = 99.99m,
                        Description = "This should not be returned if the cache is working",
                        CategoryId = 3
                    };
                });
                
                Console.WriteLine($"GetOrAdd result: {product2Again}");
                
                // Show cache statistics
                Console.WriteLine("\nCache statistics:");
                var stats = cache.GetStatistics();
                Console.WriteLine($"Item count: {stats.ItemCount}");
                Console.WriteLine($"Total size: {stats.TotalSizeBytes / 1024.0:F2} KB");
                Console.WriteLine($"Items expiring soon: {stats.ExpiringWithin10Minutes}");
                Console.WriteLine($"Cache hit ratio: {stats.CacheHitRatio:P2}");
                
                // Clean up for next examples
                Console.WriteLine("\nCleaning up cache...");
                cache.Remove("product:2");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in BasicExample: {ex.Message}");
            }
            
            Console.WriteLine("\nBasic example completed.");
        }
    }
}
