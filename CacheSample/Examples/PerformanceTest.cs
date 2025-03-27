using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using SqlServerCache.Interfaces;

namespace CacheSample.Examples
{
    /// <summary>
    /// Performance benchmarking for the SQL Server distributed cache.
    /// </summary>
    public static class PerformanceTest
    {
        /// <summary>
        /// Runs the performance benchmark.
        /// </summary>
        /// <param name="cache">The distributed cache instance.</param>
        public static void Run(IDistributedCache cache)
        {
            Console.WriteLine("\n========== PERFORMANCE BENCHMARK ==========\n");
            
            try
            {
                // Test parameters
                int iterations = 50; // Lower for SQL Server to avoid long wait times
                int smallDataSize = 1 * 1024; // 1 KB
                int mediumDataSize = 10 * 1024; // 10 KB
                int largeDataSize = 100 * 1024; // 100 KB
                
                Console.WriteLine("Performance benchmark parameters:");
                Console.WriteLine($"- Iterations: {iterations}");
                Console.WriteLine($"- Data sizes: Small ({smallDataSize / 1024} KB), Medium ({mediumDataSize / 1024} KB), Large ({largeDataSize / 1024} KB)");
                Console.WriteLine("Note: SQL Server cache is typically slower than in-memory caches but offers distributed capabilities.");
                
                // Clean up any previous test data
                Console.WriteLine("\nCleaning up previous test data...");
                for (int i = 0; i < iterations; i++)
                {
                    cache.Remove($"perf:small:{i}");
                    cache.Remove($"perf:medium:{i}");
                    cache.Remove($"perf:large:{i}");
                }
                
                // Generate test data
                Console.WriteLine("\nGenerating test data...");
                byte[] smallData = GenerateRandomData(smallDataSize);
                byte[] mediumData = GenerateRandomData(mediumDataSize);
                byte[] largeData = GenerateRandomData(largeDataSize);
                
                // Run benchmarks
                Console.WriteLine("\nRunning benchmarks...");
                
                // Small data benchmark
                Console.WriteLine("\n1. Small Data ({0} KB)", smallDataSize / 1024);
                RunSingleBenchmark(cache, "perf:small", iterations, smallData);
                
                // Medium data benchmark
                Console.WriteLine("\n2. Medium Data ({0} KB)", mediumDataSize / 1024);
                RunSingleBenchmark(cache, "perf:medium", iterations, mediumData);
                
                // Large data benchmark
                Console.WriteLine("\n3. Large Data ({0} KB)", largeDataSize / 1024);
                RunSingleBenchmark(cache, "perf:large", iterations, largeData);
                
                // Clean up test data
                Console.WriteLine("\nCleaning up test data...");
                for (int i = 0; i < iterations; i++)
                {
                    cache.Remove($"perf:small:{i}");
                    cache.Remove($"perf:medium:{i}");
                    cache.Remove($"perf:large:{i}");
                }
                
                // Display cache statistics
                Console.WriteLine("\nCache statistics after benchmark:");
                try
                {
                    var stats = cache.GetStatistics();
                    if (stats != null)
                    {
                        Console.WriteLine($"Item count: {stats.ItemCount}");
                        Console.WriteLine($"Total size: {(stats.TotalSizeBytes > 0 ? stats.TotalSizeBytes / 1024.0 / 1024.0 : 0):F2} MB");
                        Console.WriteLine($"Cache hit ratio: {stats.CacheHitRatio:P2}");
                    }
                    else
                    {
                        Console.WriteLine("Cache statistics are not available.");
                    }
                }
                catch (Exception statsEx)
                {
                    Console.WriteLine($"Error retrieving cache statistics: {statsEx.Message}");
                }
                
                Console.WriteLine("\nPerformance notes:");
                Console.WriteLine("- SQL Server cache is optimized for distributed scenarios, not raw performance");
                Console.WriteLine("- Performance varies based on network latency, SQL Server load, and hardware");
                Console.WriteLine("- For very high-performance scenarios, consider a tiered approach with local caching");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PerformanceTest: {ex.Message}");
            }
            
            Console.WriteLine("\nPerformance benchmark completed.");
        }
        
        /// <summary>
        /// Runs a benchmark for a specific data size.
        /// </summary>
        private static void RunSingleBenchmark(IDistributedCache cache, string keyPrefix, int iterations, byte[] data)
        {
            // Measure write performance
            Console.WriteLine("  Write Performance Test:");
            Stopwatch writeStopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                string key = $"{keyPrefix}:{i}";
                cache.Set(key, data, TimeSpan.FromMinutes(5));
            }
            
            writeStopwatch.Stop();
            double writeTimeMs = writeStopwatch.ElapsedMilliseconds;
            double writeTimePerOp = writeTimeMs / iterations;
            
            Console.WriteLine($"  - Total time: {writeTimeMs:N0} ms");
            Console.WriteLine($"  - Avg. per operation: {writeTimePerOp:N2} ms");
            Console.WriteLine($"  - Operations per second: {1000 / writeTimePerOp:N0}");
            
            // Pause briefly to let the database settle
            Task.Delay(1000).Wait();
            
            // Measure read performance
            Console.WriteLine("  Read Performance Test:");
            Stopwatch readStopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                string key = $"{keyPrefix}:{i}";
                byte[] result = cache.Get<byte[]>(key);
                if (result == null || result.Length == 0)
                {
                    Console.WriteLine($"  Warning: Failed to retrieve item {key}");
                }
            }
            
            readStopwatch.Stop();
            double readTimeMs = readStopwatch.ElapsedMilliseconds;
            double readTimePerOp = readTimeMs / iterations;
            
            Console.WriteLine($"  - Total time: {readTimeMs:N0} ms");
            Console.WriteLine($"  - Avg. per operation: {readTimePerOp:N2} ms");
            Console.WriteLine($"  - Operations per second: {1000 / readTimePerOp:N0}");
        }
        
        /// <summary>
        /// Generates random binary data of the specified size.
        /// </summary>
        private static byte[] GenerateRandomData(int sizeInBytes)
        {
            byte[] data = new byte[sizeInBytes];
            new Random().NextBytes(data);
            return data;
        }
    }
}
