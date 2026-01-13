using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    /// <summary>
    /// Enhanced Ollama service implementation with aggressive retry logic and timeout handling.
    /// Recommended for remote Ollama instances across networks or unreliable connections.
    /// Features: 3-attempt retry with exponential backoff, detailed logging, timeout configuration.
    /// </summary>
    public class RemoteOllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RemoteOllamaService> _logger;
        private readonly OllamaSettings _settings;
        private const int MaxRetries = 3;

        public RemoteOllamaService(HttpClient httpClient, IOptions<OllamaSettings> settings, ILogger<RemoteOllamaService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;

            // Configure HTTP client for remote connection
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

            // User agent for request tracking
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OSINT-Framework/1.0");
        }

        /// <summary>
        /// Test connectivity to remote Ollama instance.
        /// Useful for health checks before processing requests.
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags");
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully connected to remote Ollama instance at {BaseUrl}", _settings.BaseUrl);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Ollama service returned status code {StatusCode} at {BaseUrl}", response.StatusCode, _settings.BaseUrl);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to remote Ollama instance at {BaseUrl}", _settings.BaseUrl);
                return false;
            }
        }

        /// <summary>
        /// Generate analysis with automatic retry on network failures.
        /// Implements exponential backoff: 1s, 2s, 3s between retries.
        /// </summary>
        public async Task<string> GenerateAnalysisAsync(string prompt, string? model = null)
        {
            var completion = await GenerateAnalysisWithMetricsAsync(prompt, model);
            return completion.Response.Response;
        }

        public async Task<OllamaCompletionResult> GenerateAnalysisWithMetricsAsync(string prompt, string? model = null)
        {
            var selectedModel = model ?? _settings.DefaultModel;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                var requestPayload = new
                {
                    model = selectedModel,
                    prompt,
                    stream = false,
                    options = new { temperature = 0.7, top_p = 0.9 }
                };

                var payloadJson = JsonSerializer.Serialize(requestPayload);
                var requestBodyBytes = Encoding.UTF8.GetByteCount(payloadJson);

                using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                try
                {
                    _logger.LogDebug("Sending AI analysis request to {Model} (attempt {Attempt}/{MaxRetries})", selectedModel, attempt, MaxRetries);

                    var response = await _httpClient.PostAsync("/api/generate", content);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        var metadata = new Dictionary<string, string>
                        {
                            ["statusCode"] = ((int)response.StatusCode).ToString(),
                            ["statusName"] = response.StatusCode.ToString(),
                            ["requestBodyBytes"] = requestBodyBytes.ToString()
                        };

                        if (!string.IsNullOrWhiteSpace(responseJson))
                        {
                            metadata["responseBody"] = Truncate(responseJson, 1000);
                        }

                        var retryable = response.StatusCode >= HttpStatusCode.InternalServerError ||
                                        response.StatusCode == HttpStatusCode.RequestTimeout;

                        _logger.LogWarning("Ollama API returned {StatusCode} on attempt {Attempt}/{MaxRetries}", response.StatusCode, attempt, MaxRetries);
                        if (attempt == MaxRetries)
                        {
                            throw new OllamaServiceException(
                                $"Ollama returned HTTP {(int)response.StatusCode} while generating analysis.",
                                "ollama_http_error",
                                retryable,
                                metadata: metadata);
                        }

                        await Task.Delay(1000 * attempt);
                        continue;
                    }

                    var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                    if (result is null || string.IsNullOrWhiteSpace(result.Response))
                    {
                        _logger.LogWarning("Ollama returned an empty response on attempt {Attempt}/{MaxRetries}", attempt, MaxRetries);
                        if (attempt == MaxRetries)
                        {
                            throw new OllamaServiceException("Ollama returned an empty response.", "ollama_empty_response", true);
                        }

                        await Task.Delay(1000 * attempt);
                        continue;
                    }

                    _logger.LogInformation("AI analysis completed successfully using {Model} on attempt {Attempt}", selectedModel, attempt);
                    return new OllamaCompletionResult
                    {
                        Response = result,
                        StatusCode = response.StatusCode,
                        AttemptCount = attempt,
                        RequestBodyByteCount = requestBodyBytes,
                        ResponseBodyByteCount = Encoding.UTF8.GetByteCount(responseJson),
                        RequestedUri = response.RequestMessage?.RequestUri ?? new Uri(_settings.BaseUrl.TrimEnd('/') + "/api/generate")
                    };
                }
                catch (HttpRequestException ex) when (attempt < MaxRetries)
                {
                    _logger.LogWarning(ex, "Network error on attempt {Attempt}/{MaxRetries}, retrying in {DelaySeconds}s...", attempt, MaxRetries, 1000 * attempt);
                    await Task.Delay(1000 * attempt);
                }
                catch (OperationCanceledException ex) when (attempt < MaxRetries)
                {
                    _logger.LogWarning(ex, "Timeout on attempt {Attempt}/{MaxRetries}, retrying in {DelaySeconds}s...", attempt, MaxRetries, 1000 * attempt);
                    await Task.Delay(1000 * attempt);
                }
                catch (TaskCanceledException ex) when (attempt == MaxRetries)
                {
                    throw new OllamaServiceException("Timed out waiting for Ollama to respond.", "ollama_timeout", true, ex);
                }
                catch (HttpRequestException ex) when (attempt == MaxRetries)
                {
                    throw new OllamaServiceException("Network error while contacting Ollama.", "ollama_network", true, ex);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse Ollama response on attempt {Attempt}/{MaxRetries}", attempt, MaxRetries);
                    throw new OllamaServiceException("Failed to parse Ollama response.", "ollama_parse_error", false, ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during AI analysis on attempt {Attempt}/{MaxRetries}", attempt, MaxRetries);
                    if (attempt == MaxRetries)
                    {
                        throw new OllamaServiceException("Unexpected error invoking Ollama.", "ollama_unexpected_error", false, ex);
                    }
                }
            }

            throw new OllamaServiceException("Failed to generate analysis after multiple attempts.", "ollama_retry_exhausted", true);
        }

        /// <summary>
        /// Analyze OSINT data with intelligent prompt construction.
        /// Uses GenerateAnalysisAsync with retry logic.
        /// </summary>
        public async Task<string> AnalyzeOsintDataAsync(List<OsintResult> results, string analysisType, string? model = null)
        {
            var prompt = AiPromptBuilder.BuildAnalysisPrompt(results, analysisType);
            return await GenerateAnalysisAsync(prompt, model);
        }

        /// <summary>
        /// Generate intelligent inferences from OSINT data.
        /// Uses larger model variant (llama2:13b) for complex analysis.
        /// </summary>
        public async Task<string> GenerateInferencesAsync(List<OsintResult> results, string? model = null)
        {
            var prompt = AiPromptBuilder.BuildInferencePrompt(results);

            var targetModel = model ?? "llama2:13b";
            return await GenerateAnalysisAsync(prompt, targetModel);
        }

        /// <summary>
        /// Get list of available models from remote Ollama instance.
        /// Returns default model if request fails after retries.
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync("/api/tags");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<OllamaModelsResponse>(content);
                        var models = result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();

                        _logger.LogInformation("Retrieved {ModelCount} models from Ollama on attempt {Attempt}", models.Count, attempt);
                        return models;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to get models: HTTP {StatusCode} on attempt {Attempt}/{MaxRetries}", response.StatusCode, attempt, MaxRetries);
                        if (attempt < MaxRetries)
                            await Task.Delay(1000 * attempt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting models on attempt {Attempt}/{MaxRetries}", attempt, MaxRetries);
                    if (attempt < MaxRetries)
                        await Task.Delay(1000 * attempt);
                }
            }

            _logger.LogWarning("Failed to get models after {MaxRetries} attempts, returning default model", MaxRetries);
            return new List<string> { _settings.DefaultModel };
        }

        /// <summary>
        /// Check if Ollama service is available.
        /// Uses /api/tags endpoint for health check.
        /// </summary>
        public async Task<bool> IsServiceAvailableAsync()
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync("/api/tags");
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Ollama service is available at {BaseUrl}", _settings.BaseUrl);
                        return true;
                    }

                    if (attempt < MaxRetries)
                    {
                        _logger.LogDebug("Service health check failed (HTTP {StatusCode}), retrying...", response.StatusCode);
                        await Task.Delay(1000 * attempt);
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < MaxRetries)
                    {
                        _logger.LogDebug(ex, "Service health check failed on attempt {Attempt}, retrying...", attempt);
                        await Task.Delay(1000 * attempt);
                    }
                }
            }

            _logger.LogWarning("Ollama service unavailable at {BaseUrl} after {MaxRetries} health checks", _settings.BaseUrl, MaxRetries);
            return false;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength] + "…";
        }
    }
}
