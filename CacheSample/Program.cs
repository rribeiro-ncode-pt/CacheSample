using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading;
using CacheSample.Examples;
using SqlServerCache;
using SqlServerCache.Interfaces;
using SqlServerCache.Models;

namespace CacheSample
{
    /// <summary>
    /// Demonstrates SQL Server distributed cache operations.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SQL Server Distributed Cache Demo");
            Console.WriteLine("=================================");
            
            // Get connection string from configuration
            string connectionString = ConfigurationManager.ConnectionStrings["CacheDb"].ConnectionString;
            
            // Check for command line arguments to auto-run specific tests
            bool runConcurrencyTest = args.Length > 0 && args[0].Equals("concurrency", StringComparison.OrdinalIgnoreCase);
            bool runPerformanceTest = args.Length > 0 && args[0].Equals("performance", StringComparison.OrdinalIgnoreCase);
            
            try
            {
                // Ensure database exists
                SetupDatabase(connectionString);
                
                // Configure cache options
                var options = new CacheOptions
                {
                    TableName = "DistributedCache",
                    DefaultSlidingExpiration = TimeSpan.FromMinutes(10),
                    EnableCompression = true,
                    AutoCleanup = true
                };
                
                // Create cache instance
                Console.WriteLine("\nInitializing SQL Server distributed cache...");
                using (var cache = new SqlServerDistributedCache(connectionString, options))
                {
                    // Run specific tests if command line arguments are provided, otherwise show menu
                    if (runConcurrencyTest)
                    {
                        Console.WriteLine("Auto-running concurrency test...");
                        try
                        {
                            ConcurrencyExample.Run(cache);
                        }
                        catch (Exception testEx)
                        {
                            Console.WriteLine($"Concurrency test error: {testEx.Message}");
                            Console.WriteLine($"Stack trace: {testEx.StackTrace}");
                        }
                    }
                    else if (runPerformanceTest)
                    {
                        Console.WriteLine("Auto-running performance test...");
                        try
                        {
                            PerformanceTest.Run(cache);
                        }
                        catch (Exception testEx)
                        {
                            Console.WriteLine($"Performance test error: {testEx.Message}");
                            Console.WriteLine($"Stack trace: {testEx.StackTrace}");
                        }
                    }
                    else
                    {
                        // Display menu and run examples
                        RunExampleMenu(cache);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine("\nStack trace:");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        
        /// <summary>
        /// Displays the menu and runs the selected example.
        /// </summary>
        private static void RunExampleMenu(IDistributedCache cache)
        {
            while (true)
            {
                Console.WriteLine("\nSelect an example to run:");
                Console.WriteLine("1. Basic cache operations");
                Console.WriteLine("2. Multi-threaded concurrency test");
                Console.WriteLine("3. Performance benchmark");
                Console.WriteLine("4. Clear all cache data");
                Console.WriteLine("5. Exit");
                Console.Write("\nEnter your choice (1-5): ");
                
                string choice = Console.ReadLine()?.Trim();
                
                switch (choice)
                {
                    case "1":
                        BasicExample.Run(cache);
                        break;
                    
                    case "2":
                        ConcurrencyExample.Run(cache);
                        break;
                    
                    case "3":
                        PerformanceTest.Run(cache);
                        break;
                    
                    case "4":
                        Console.WriteLine("\nClearing all cache data...");
                        cache.Clear();
                        Console.WriteLine("Cache cleared successfully.");
                        break;
                    
                    case "5":
                        return;
                    
                    default:
                        Console.WriteLine("Invalid choice, please try again.");
                        break;
                }
                
                // Pause before showing the menu again
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }
        
        /// <summary>
        /// Sets up the database for the cache.
        /// </summary>
        private static void SetupDatabase(string connectionString)
        {
            string databaseName = "CacheDB";
            
            // Extract server connection string (for master database)
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            string originalCatalog = builder.InitialCatalog;
            builder.InitialCatalog = "master";
            string masterConnectionString = builder.ConnectionString;
            
            Console.WriteLine($"Checking if database '{databaseName}' exists...");
            
            using (var connection = new SqlConnection(masterConnectionString))
            {
                try
                {
                    connection.Open();
                    
                    // Check if database exists
                    using (var command = new SqlCommand($"SELECT DB_ID('{databaseName}')", connection))
                    {
                        var result = command.ExecuteScalar();
                        if (result != DBNull.Value && result != null)
                        {
                            Console.WriteLine($"Database '{databaseName}' already exists.");
                            return;
                        }
                    }
                    
                    // Create the database
                    Console.WriteLine($"Creating database '{databaseName}'...");
                    using (var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection))
                    {
                        command.ExecuteNonQuery();
                        Console.WriteLine($"Database '{databaseName}' created successfully.");
                    }
                    
                    // Wait a moment for the database to be fully created
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during database setup: {ex.Message}");
                    Console.WriteLine("Ensure SQL Server is running and the connection string is correct.");
                    Console.WriteLine();
                    Console.WriteLine("If you're using Docker, make sure the SQL Server container is running:");
                    Console.WriteLine("docker run -e \"ACCEPT_EULA=Y\" -e \"MSSQL_SA_PASSWORD=YourStrong!Passw0rd\" -p 1433:1433 --name sqlserver2022 -d mcr.microsoft.com/mssql/server:2022-latest");
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    Environment.Exit(1);
                }
            }
        }
    }
}
