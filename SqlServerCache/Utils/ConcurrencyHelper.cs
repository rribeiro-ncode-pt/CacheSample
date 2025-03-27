using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace SqlServerCache.Utils
{
    /// <summary>
    /// Provides methods for distributed locking using SQL Server.
    /// </summary>
    internal static class ConcurrencyHelper
    {
        /// <summary>
        /// Executes an action within a distributed lock using SQL Server's sp_getapplock.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="connection">The SQL connection.</param>
        /// <param name="lockKey">The key for the lock resource.</param>
        /// <param name="action">The action to execute within the lock.</param>
        /// <param name="timeout">The timeout period for acquiring the lock.</param>
        /// <returns>The result of the action.</returns>
        public static T ExecuteWithLock<T>(SqlConnection connection, string lockKey, Func<T> action, TimeSpan? timeout = null)
        {
            bool ownsConnection = false;

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
                ownsConnection = true;
            }

            try
            {
                var lockResource = $"Cache_{lockKey}";
                var timeoutMs = (int)(timeout?.TotalMilliseconds ?? 10000);

                // Acquire lock
                using (var command = new SqlCommand(SqlScripts.GetAcquireLockScript(), connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@LockResource", lockResource);
                    command.Parameters.AddWithValue("@LockTimeoutMs", timeoutMs);

                    var resultObj = command.ExecuteScalar();
                    if (resultObj == null)
                    {
                        throw new InvalidOperationException($"SQL lock procedure returned null for resource '{lockResource}'. Check if sp_getapplock is available in your SQL Server instance.");
                    }
                    
                    var result = Convert.ToInt32(resultObj);
                    if (result < 0)
                    {
                        throw new TimeoutException($"Could not acquire lock for '{lockKey}' within timeout period. Lock result: {result}");
                    }
                }

                try
                {
                    return action();
                }
                finally
                {
                    // Release lock
                    using (var command = new SqlCommand(SqlScripts.GetReleaseLockScript(), connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@LockResource", lockResource);
                        command.ExecuteNonQuery();
                    }
                }
            }
            finally
            {
                if (ownsConnection)
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Executes an action within a distributed lock using SQL Server's sp_getapplock asynchronously.
        /// </summary>
        /// <typeparam name="T">The return type of the action.</typeparam>
        /// <param name="connection">The SQL connection.</param>
        /// <param name="lockKey">The key for the lock resource.</param>
        /// <param name="action">The action to execute within the lock.</param>
        /// <param name="timeout">The timeout period for acquiring the lock.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task<T> ExecuteWithLockAsync<T>(SqlConnection connection, string lockKey, Func<Task<T>> action, TimeSpan? timeout = null)
        {
            bool ownsConnection = false;

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
                ownsConnection = true;
            }

            try
            {
                var lockResource = $"Cache_{lockKey}";
                var timeoutMs = (int)(timeout?.TotalMilliseconds ?? 10000);

                // Acquire lock
                using (var command = new SqlCommand(SqlScripts.GetAcquireLockScript(), connection))
                {
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@LockResource", lockResource);
                    command.Parameters.AddWithValue("@LockTimeoutMs", timeoutMs);

                    var resultObj = await command.ExecuteScalarAsync();
                    if (resultObj == null)
                    {
                        throw new InvalidOperationException($"SQL lock procedure returned null for resource '{lockResource}'. Check if sp_getapplock is available in your SQL Server instance.");
                    }
                    
                    var result = Convert.ToInt32(resultObj);
                    if (result < 0)
                    {
                        throw new TimeoutException($"Could not acquire lock for '{lockKey}' within timeout period. Lock result: {result}");
                    }
                }

                try
                {
                    return await action();
                }
                finally
                {
                    // Release lock
                    using (var command = new SqlCommand(SqlScripts.GetReleaseLockScript(), connection))
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@LockResource", lockResource);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            finally
            {
                if (ownsConnection)
                {
                    connection.Close();
                }
            }
        }
    }
}
