using System.Collections.Generic;
using OsintBackend.Models;
using System.Text.Json.Serialization;

namespace OsintBackend.Services
{
    public interface IOllamaService
    {
        Task<string> GenerateAnalysisAsync(string prompt, string? model = null);
        Task<OllamaCompletionResult> GenerateAnalysisWithMetricsAsync(string prompt, string? model = null);
        Task<string> AnalyzeOsintDataAsync(List<OsintResult> results, string analysisType, string? model = null);
        Task<List<string>> GetAvailableModelsAsync();
        Task<bool> IsServiceAvailableAsync();
        Task<string> GenerateInferencesAsync(List<OsintResult> results, string? model = null);
    }

    /// <summary>
    /// Configuration settings for Ollama service (local or remote)
    /// </summary>
    public class OllamaSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string DefaultModel { get; set; } = "llama2";
        public int TimeoutSeconds { get; set; } = 300;
    }

    /// <summary>
    /// Exception thrown when the Ollama service encounters recoverable or fatal errors.
    /// </summary>
    public class OllamaServiceException : Exception
    {
        public string? ErrorCode { get; }
        public bool IsRetryable { get; }
        public Dictionary<string, string> Metadata { get; }

        public OllamaServiceException(
            string message,
            string? errorCode = null,
            bool isRetryable = true,
            Exception? innerException = null,
            Dictionary<string, string>? metadata = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            IsRetryable = isRetryable;
            Metadata = metadata is { Count: > 0 }
                ? new Dictionary<string, string>(metadata)
                : new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Response from Ollama /api/generate endpoint
    /// </summary>
    public class OllamaResponse
    {
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        public string Response { get; set; } = string.Empty;
        public bool Done { get; set; }

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; set; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; set; }

        [JsonPropertyName("load_duration")]
        public long? LoadDuration { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }

        [JsonPropertyName("eval_duration")]
        public long? EvalDuration { get; set; }
    }

    /// <summary>
    /// Response from Ollama /api/tags endpoint (list of available models)
    /// </summary>
    public class OllamaModelsResponse
    {
        public List<OllamaModel> Models { get; set; } = new List<OllamaModel>();
    }

    /// <summary>
    /// Represents a single Ollama model
    /// </summary>
    public class OllamaModel
    {
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("modified_at")]
        public DateTime ModifiedAt { get; set; }

        public long Size { get; set; }
    }
}
