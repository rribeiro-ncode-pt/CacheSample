using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlServerCache.Interfaces;

// Creating a reference type wrapper for int
[Serializable]
public class IntegerBox
{
    public int Value { get; set; }
    
    public static implicit operator int(IntegerBox box) => box?.Value ?? 0;
    
    public override string ToString() => Value.ToString();
}

namespace CacheSample.Examples
{
    /// <summary>
    /// Demonstrates cache concurrency handling using multiple threads.
    /// </summary>
    public static class ConcurrencyExample
    {
        /// <summary>
        /// Runs the concurrency testing example.
        /// </summary>
        /// <param name="cache">The distributed cache instance.</param>
        public static void Run(IDistributedCache cache)
        {
            Console.WriteLine("\n========== CONCURRENCY TESTING ==========\n");
            
            try
            {
                // Clear any existing items that might interfere with our test
                cache.Remove("counter");
                
                // Number of concurrent tasks to run
                int taskCount = 5;
                int incrementsPerTask = 10;
                int expectedValue = taskCount * incrementsPerTask;
                
                Console.WriteLine($"Starting concurrency test with {taskCount} tasks, each incrementing {incrementsPerTask} times");
                Console.WriteLine($"Expected final counter value: {expectedValue}");
                
                // Use a shared counter key for the test
                string counterKey = "counter";
                
                // Start multiple tasks that increment a counter
                var tasks = new List<Task>();
                
                for (int i = 0; i < taskCount; i++)
                {
                    int taskId = i + 1;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int j = 0; j < incrementsPerTask; j++)
                        {
                            // Each task tries to increment the counter multiple times
                            IncrementCounter(cache, counterKey, taskId, j + 1);
                            
                            // Small delay to simulate real-world conditions
                            Thread.Sleep(new Random().Next(10, 50));
                        }
                    }));
                }
                
                Console.WriteLine($"Started {taskCount} concurrent tasks...");
                
                // Wait for all tasks to complete
                Task.WaitAll(tasks.ToArray());
                
                // Retrieve the final counter value
                var boxedValue = cache.Get<IntegerBox>(counterKey);
                int finalValue = boxedValue?.Value ?? 0;
                Console.WriteLine($"\nFinal counter value: {finalValue}");
                Console.WriteLine($"Expected value: {expectedValue}");
                
                // Check if the counter value is correct
                if (finalValue == expectedValue)
                {
                    Console.WriteLine("SUCCESS: Cache concurrency handling is working correctly!");
                }
                else
                {
                    Console.WriteLine("WARNING: Cache concurrency handling is not working correctly.");
                    Console.WriteLine("This can happen if multiple instances increment the counter at the same time without proper locking.");
                }
                
                // Clean up
                cache.Remove(counterKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ConcurrencyExample: {ex.Message}");
            }
            
            Console.WriteLine("\nConcurrency example completed.");
        }
        
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Simpler approach to incrementing the counter value with proper locking
        /// </summary>
        private static void IncrementCounter(IDistributedCache cache, string key, int taskId, int iteration)
        {
            try
            {
                // Using a static lock object to ensure only one thread modifies the counter at a time
                // This is a simplification for the demo - in a real distributed scenario we'd use
                // something like Redis or SQL Server's application locks
                lock (_lockObject)
                {
                    // Get the current value
                    var boxedValue = cache.Get<IntegerBox>(key);
                    int currentValue = 0;
                    
                    if (boxedValue != null)
                    {
                        currentValue = boxedValue.Value;
                    }
                    
                    // Increment the value
                    int newValue = currentValue + 1;
                    
                    // Log the increment
                    Console.WriteLine($"Task {taskId}, Iteration {iteration}: Incrementing counter from {currentValue} to {newValue}");
                    
                    // Update the cache with the new value
                    cache.Set(key, new IntegerBox { Value = newValue });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing counter: {ex.Message}");
            }
        }
    }
}
