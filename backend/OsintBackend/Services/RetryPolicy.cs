using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OsintBackend.Services
{
    /// <summary>
    /// Retry policy with exponential backoff for external tool operations
    /// Handles transient failures (network timeouts, temporary unavailability)
    /// </summary>
    public class RetryPolicy
    {
        private readonly int _maxAttempts;
        private readonly int _initialDelayMs;
        private readonly double _backoffMultiplier;
        private readonly int _maxDelayMs;
        private readonly ILogger? _logger;

        public RetryPolicy(
            int maxAttempts = 3,
            int initialDelayMs = 1000,
            double backoffMultiplier = 2.0,
            int maxDelayMs = 30000,
            ILogger? logger = null)
        {
            _maxAttempts = Math.Max(1, maxAttempts);
            _initialDelayMs = Math.Max(0, initialDelayMs);
            _backoffMultiplier = Math.Max(1.0, backoffMultiplier);
            _maxDelayMs = Math.Max(initialDelayMs, maxDelayMs);
            _logger = logger;
        }

        /// <summary>
        /// Executes an async operation with retry logic
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operationName">Name of operation for logging</param>
        /// <param name="operation">The operation to execute</param>
        /// <returns>Result of the operation</returns>
        public async Task<T> ExecuteAsync<T>(string operationName, Func<Task<T>> operation)
        {
            int delayMs = _initialDelayMs;

            for (int attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                try
                {
                    _logger?.LogDebug("Executing '{OperationName}' (attempt {Attempt}/{MaxAttempts})",
                        operationName, attempt, _maxAttempts);

                    return await operation();
                }
                catch (Exception ex) when (attempt < _maxAttempts && IsTransient(ex))
                {
                    _logger?.LogWarning(ex,
                        "Transient error in '{OperationName}' on attempt {Attempt}/{MaxAttempts}. " +
                        "Retrying in {DelayMs}ms...",
                        operationName, attempt, _maxAttempts, delayMs);

                    await Task.Delay(delayMs);
                    delayMs = (int)Math.Min(_maxDelayMs, delayMs * _backoffMultiplier);
                }
            }

            // Final attempt without exception handling
            _logger?.LogDebug("Executing '{OperationName}' (final attempt {Attempt}/{MaxAttempts})",
                operationName, _maxAttempts, _maxAttempts);

            return await operation();
        }

        /// <summary>
        /// Executes an async operation with retry logic (void)
        /// </summary>
        public async Task ExecuteAsync(string operationName, Func<Task> operation)
        {
            int delayMs = _initialDelayMs;

            for (int attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                try
                {
                    _logger?.LogDebug("Executing '{OperationName}' (attempt {Attempt}/{MaxAttempts})",
                        operationName, attempt, _maxAttempts);

                    await operation();
                    return;
                }
                catch (Exception ex) when (attempt < _maxAttempts && IsTransient(ex))
                {
                    _logger?.LogWarning(ex,
                        "Transient error in '{OperationName}' on attempt {Attempt}/{MaxAttempts}. " +
                        "Retrying in {DelayMs}ms...",
                        operationName, attempt, _maxAttempts, delayMs);

                    await Task.Delay(delayMs);
                    delayMs = (int)Math.Min(_maxDelayMs, delayMs * _backoffMultiplier);
                }
            }

            // Final attempt without exception handling
            _logger?.LogDebug("Executing '{OperationName}' (final attempt {Attempt}/{MaxAttempts})",
                operationName, _maxAttempts, _maxAttempts);

            await operation();
        }

        /// <summary>
        /// Determines if an exception is transient (worth retrying)
        /// </summary>
        private static bool IsTransient(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TimeoutException ||
                   ex is OperationCanceledException ||
                   (ex.InnerException != null && IsTransient(ex.InnerException));
        }
    }
}
