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
    /// Standard Ollama service implementation with basic retry logic.
    /// Works for both local and remote Ollama instances.
    /// </summary>
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaService> _logger;
        private readonly OllamaSettings _settings;

        public OllamaService(HttpClient httpClient, IOptions<OllamaSettings> settings, ILogger<OllamaService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        public async Task<string> GenerateAnalysisAsync(string prompt, string? model = null)
        {
            var completion = await GenerateAnalysisWithMetricsAsync(prompt, model);
            return completion.Response.Response;
        }

        public async Task<OllamaCompletionResult> GenerateAnalysisWithMetricsAsync(string prompt, string? model = null)
        {
            var selectedModel = model ?? _settings.DefaultModel;
            var requestPayload = new
            {
                model = selectedModel,
                prompt,
                stream = false
            };

            var payloadJson = JsonSerializer.Serialize(requestPayload);
            using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("/api/generate", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var metadata = new Dictionary<string, string>
                    {
                        ["statusCode"] = ((int)response.StatusCode).ToString(),
                        ["statusName"] = response.StatusCode.ToString(),
                        ["requestBodyBytes"] = Encoding.UTF8.GetByteCount(payloadJson).ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(responseJson))
                    {
                        metadata["responseBody"] = Truncate(responseJson, 1000);
                    }

                    var retryable = response.StatusCode >= HttpStatusCode.InternalServerError ||
                                    response.StatusCode == HttpStatusCode.RequestTimeout;

                    throw new OllamaServiceException(
                        $"Ollama returned HTTP {(int)response.StatusCode} while generating analysis.",
                        "ollama_http_error",
                        retryable,
                        metadata: metadata);
                }

                var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                if (result is null || string.IsNullOrWhiteSpace(result.Response))
                {
                    throw new OllamaServiceException("Ollama returned an empty response.", "ollama_empty_response", true);
                }

                return new OllamaCompletionResult
                {
                    Response = result,
                    StatusCode = response.StatusCode,
                    AttemptCount = 1,
                    RequestBodyByteCount = Encoding.UTF8.GetByteCount(payloadJson),
                    ResponseBodyByteCount = Encoding.UTF8.GetByteCount(responseJson),
                    RequestedUri = response.RequestMessage?.RequestUri ?? new Uri(_settings.BaseUrl.TrimEnd('/') + "/api/generate")
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Timeout generating analysis with Ollama at {BaseUrl}", _settings.BaseUrl);
                throw new OllamaServiceException("Timed out waiting for Ollama to respond.", "ollama_timeout", true, ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Network error contacting Ollama at {BaseUrl}", _settings.BaseUrl);
                throw new OllamaServiceException("Network error while contacting Ollama.", "ollama_network", true, ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Ollama response at {BaseUrl}", _settings.BaseUrl);
                throw new OllamaServiceException("Failed to parse Ollama response.", "ollama_parse_error", false, ex);
            }
        }

        public async Task<string> AnalyzeOsintDataAsync(List<OsintResult> results, string analysisType, string? model = null)
        {
            var prompt = AiPromptBuilder.BuildAnalysisPrompt(results, analysisType);
            var completion = await GenerateAnalysisWithMetricsAsync(prompt, model);
            return completion.Response.Response;
        }

        public async Task<string> GenerateInferencesAsync(List<OsintResult> results, string? model = null)
        {
            var prompt = AiPromptBuilder.BuildInferencePrompt(results);
            var targetModel = model ?? "llama2:13b";
            var completion = await GenerateAnalysisWithMetricsAsync(prompt, targetModel);
            return completion.Response.Response;
        }

        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OllamaModelsResponse>(content);
                    return result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models from Ollama at {BaseUrl}", _settings.BaseUrl);
            }

            return new List<string> { _settings.DefaultModel };
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama service unavailable at {BaseUrl}", _settings.BaseUrl);
                return false;
            }
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
