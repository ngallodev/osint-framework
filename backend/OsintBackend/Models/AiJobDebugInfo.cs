namespace OsintBackend.Models
{
    /// <summary>
    /// Debug information captured when Debug flag is enabled
    /// </summary>
    public class AiJobDebugInfo
    {
        /// <summary>
        /// Full prompt sent to Ollama
        /// </summary>
        public string? PromptText { get; set; }

        /// <summary>
        /// Length of prompt in characters
        /// </summary>
        public int? PromptLength { get; set; }

        /// <summary>
        /// Ollama response metadata
        /// </summary>
        public OllamaDebugMetrics? OllamaMetrics { get; set; }

        /// <summary>
        /// HTTP request/response timing
        /// </summary>
        public HttpDebugMetrics? HttpMetrics { get; set; }

        /// <summary>
        /// Timestamp when request was sent
        /// </summary>
        public DateTime? RequestSentAt { get; set; }

        /// <summary>
        /// Timestamp when response was received
        /// </summary>
        public DateTime? ResponseReceivedAt { get; set; }
    }

    public class OllamaDebugMetrics
    {
        /// <summary>
        /// Model name used
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Total duration in nanoseconds
        /// </summary>
        public long? TotalDurationNs { get; set; }

        /// <summary>
        /// Model load duration in nanoseconds
        /// </summary>
        public long? LoadDurationNs { get; set; }

        /// <summary>
        /// Prompt evaluation token count
        /// </summary>
        public int? PromptEvalCount { get; set; }

        /// <summary>
        /// Prompt evaluation duration in nanoseconds
        /// </summary>
        public long? PromptEvalDurationNs { get; set; }

        /// <summary>
        /// Response token count
        /// </summary>
        public int? EvalCount { get; set; }

        /// <summary>
        /// Response generation duration in nanoseconds
        /// </summary>
        public long? EvalDurationNs { get; set; }

        /// <summary>
        /// Calculated tokens per second for prompt
        /// </summary>
        public double? PromptTokensPerSecond { get; set; }

        /// <summary>
        /// Calculated tokens per second for response
        /// </summary>
        public double? ResponseTokensPerSecond { get; set; }

        /// <summary>
        /// Done reason (stop, length, etc.)
        /// </summary>
        public string? DoneReason { get; set; }
    }

    public class HttpDebugMetrics
    {
        /// <summary>
        /// HTTP request duration in milliseconds
        /// </summary>
        public double? RequestDurationMs { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Request body size in bytes
        /// </summary>
        public long? RequestBodySize { get; set; }

        /// <summary>
        /// Response body size in bytes
        /// </summary>
        public long? ResponseBodySize { get; set; }

        /// <summary>
        /// Ollama endpoint URL
        /// </summary>
        public string? EndpointUrl { get; set; }

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int? RetryAttempts { get; set; }
    }
}
