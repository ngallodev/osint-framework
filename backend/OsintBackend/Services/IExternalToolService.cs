using System.Threading;
using System.Threading.Tasks;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    /// <summary>
    /// Base contract for external OSINT tool integrations (SpiderFoot, Sherlock, etc.)
    /// Defines lifecycle operations, configuration, and result normalization.
    /// </summary>
    public interface IExternalToolService
    {
        /// <summary>
        /// Gets the tool name/identifier (e.g., "spiderfoot", "sherlock")
        /// </summary>
        string ToolName { get; }

        /// <summary>
        /// Gets the tool version
        /// </summary>
        string ToolVersion { get; }

        /// <summary>
        /// Validates that the tool is available and accessible (e.g., API reachable, dependencies installed)
        /// </summary>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates tool-specific configuration before execution
        /// </summary>
        /// <param name="configuration">Tool configuration as JSON string</param>
        /// <returns>Validation result with errors if invalid</returns>
        ValidationResult ValidateConfiguration(string? configuration);

        /// <summary>
        /// Executes the tool against a target with the provided configuration
        /// </summary>
        /// <param name="target">The investigation target (domain, email, username, etc.)</param>
        /// <param name="configuration">Tool-specific configuration as JSON string</param>
        /// <param name="cancellationToken">Cancellation token for stopping long-running operations</param>
        /// <returns>Structured findings from the tool execution</returns>
        Task<ToolExecutionResult> ExecuteAsync(
            string target,
            string? configuration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the status of a running tool execution (for async polling scenarios)
        /// </summary>
        /// <param name="executionId">Tool-specific execution identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current execution status and progress</returns>
        Task<ToolStatusResponse> GetStatusAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a running tool execution
        /// </summary>
        /// <param name="executionId">Tool-specific execution identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<bool> CancelAsync(
            string executionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Maps/transforms raw tool findings into normalized ToolFinding entities
        /// This is called after ExecuteAsync to persist findings consistently
        /// </summary>
        /// <param name="result">Raw execution result from ExecuteAsync</param>
        /// <returns>Enumerable of normalized ToolFinding entities ready to persist</returns>
        IAsyncEnumerable<ToolFinding> NormalizeResultsAsync(ToolExecutionResult result);
    }

    /// <summary>
    /// Result of tool execution containing raw findings
    /// </summary>
    public class ToolExecutionResult
    {
        /// <summary>
        /// Tool-specific execution identifier (may be empty for synchronous executions)
        /// </summary>
        public string ExecutionId { get; set; } = string.Empty;

        /// <summary>
        /// Success/failure indicator
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Raw findings as JSON (tool-specific format preserved)
        /// </summary>
        public string RawData { get; set; } = "[]";

        /// <summary>
        /// Number of findings discovered
        /// </summary>
        public int FindingCount { get; set; }

        /// <summary>
        /// Execution metadata (start time, end time, duration, tool version, etc.)
        /// </summary>
        public string Metadata { get; set; } = "{}";

        /// <summary>
        /// Tool-specific status if async execution
        /// </summary>
        public string Status { get; set; } = "completed";
    }

    /// <summary>
    /// Status of an async tool execution
    /// </summary>
    public class ToolStatusResponse
    {
        public string Status { get; set; } = "unknown";
        public int? ProgressPercent { get; set; }
        public string? Message { get; set; }
        public int FindingCount { get; set; }
    }

    /// <summary>
    /// Configuration validation result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Failure(params string[] errors) => new()
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }
}
