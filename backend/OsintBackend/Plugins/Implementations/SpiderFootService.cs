using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsintBackend.Models;
using OsintBackend.Services;

namespace OsintBackend.Plugins.Implementations
{
    /// <summary>
    /// SpiderFoot integration plugin
    /// Handles scan orchestration, result normalization, and persistence
    /// </summary>
    public class SpiderFootService : IExternalToolService
    {
        private readonly SpiderFootClient _client;
        private readonly ILogger<SpiderFootService> _logger;
        private readonly SpiderFootConfig _config;
        private const int StatusCheckIntervalMs = 5000; // 5 seconds
        private const int MaxStatusCheckAttempts = 120; // 10 minutes total

        public string ToolName => "spiderfoot";
        public string ToolVersion => "4.0"; // Version should match deployed SpiderFoot instance

        public SpiderFootService(
            SpiderFootClient client,
            IOptionsMonitor<SpiderFootConfig> configMonitor,
            ILogger<SpiderFootService> logger)
        {
            _client = client;
            _logger = logger;
            _config = configMonitor.CurrentValue;
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _client.TestConnectionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SpiderFoot availability check failed");
                return false;
            }
        }

        public ValidationResult ValidateConfiguration(string? configuration)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configuration))
                {
                    // Default config is always valid
                    return ValidationResult.Success();
                }

                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configuration);
                if (config == null)
                {
                    return ValidationResult.Failure("Configuration must be valid JSON");
                }

                // Validate known configuration fields
                var errors = new List<string>();

                if (config.TryGetValue("modules", out var modulesObj))
                {
                    if (!(modulesObj is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Object))
                    {
                        errors.Add("'modules' field must be a JSON object");
                    }
                }

                if (config.TryGetValue("timeout", out var timeoutObj))
                {
                    if (!int.TryParse(timeoutObj?.ToString(), out int timeout) || timeout <= 0)
                    {
                        errors.Add("'timeout' must be a positive integer (seconds)");
                    }
                }

                return errors.Count > 0
                    ? ValidationResult.Failure(errors.ToArray())
                    : ValidationResult.Success();
            }
            catch (JsonException ex)
            {
                return ValidationResult.Failure($"Invalid JSON configuration: {ex.Message}");
            }
        }

        public async Task<ToolExecutionResult> ExecuteAsync(
            string target,
            string? configuration,
            CancellationToken cancellationToken = default)
        {
            var result = new ToolExecutionResult();
            var executionStartTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("Starting SpiderFoot scan for target: {Target}", target);

                // Parse configuration
                var moduleConfig = ExtractModuleConfig(configuration);

                // Start the scan
                var scanResponse = await _client.StartScanAsync(
                    $"scan_{target}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                    target,
                    moduleConfig,
                    cancellationToken);

                if (scanResponse == null || string.IsNullOrEmpty(scanResponse.scan_id))
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to start SpiderFoot scan";
                    return result;
                }

                result.ExecutionId = scanResponse.scan_id;

                // Poll for completion
                var findings = await PollScanUntilCompleteAsync(
                    scanResponse.scan_id,
                    cancellationToken);

                if (findings == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to retrieve scan results or scan timed out";
                    await _client.StopScanAsync(scanResponse.scan_id, cancellationToken);
                    return result;
                }

                // Serialize findings to JSON
                result.RawData = JsonSerializer.Serialize(findings);
                result.FindingCount = findings.Count;
                result.Success = true;
                result.Status = "completed";
                result.Metadata = JsonSerializer.Serialize(new
                {
                    ScanId = scanResponse.scan_id,
                    Target = target,
                    StartedAt = executionStartTime,
                    CompletedAt = DateTime.UtcNow,
                    DurationSeconds = (DateTime.UtcNow - executionStartTime).TotalSeconds,
                    ModuleCount = moduleConfig?.Count ?? 0
                });

                _logger.LogInformation(
                    "SpiderFoot scan completed: {ScanId}, {FindingCount} findings",
                    scanResponse.scan_id, findings.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("SpiderFoot scan cancelled");
                result.Success = false;
                result.ErrorMessage = "Scan was cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SpiderFoot scan execution failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<ToolStatusResponse> GetStatusAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var status = await _client.GetScanStatusAsync(executionId, cancellationToken);
                if (status == null)
                {
                    return new ToolStatusResponse
                    {
                        Status = "unknown",
                        Message = "Could not retrieve scan status"
                    };
                }

                return new ToolStatusResponse
                {
                    Status = status.status,
                    ProgressPercent = Math.Min(100, Math.Max(0, status.progress)),
                    Message = status.description
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get SpiderFoot scan status");
                return new ToolStatusResponse
                {
                    Status = "error",
                    Message = ex.Message
                };
            }
        }

        public async Task<bool> CancelAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _client.StopScanAsync(executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel SpiderFoot scan");
                return false;
            }
        }

        public async IAsyncEnumerable<ToolFinding> NormalizeResultsAsync(ToolExecutionResult result)
        {
            if (!result.Success || string.IsNullOrEmpty(result.RawData))
            {
                yield break;
            }

            List<SpiderFootFinding>? findings = null;
            try
            {
                findings = JsonSerializer.Deserialize<List<SpiderFootFinding>>(result.RawData);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize SpiderFoot findings");
                yield break;
            }

            if (findings == null || findings.Count == 0)
            {
                yield break;
            }

            foreach (var finding in findings)
            {
                yield return NormalizeFinding(finding);
            }
        }

        /// <summary>
        /// Polls SpiderFoot API until scan completes or times out
        /// </summary>
        private async Task<List<SpiderFootFinding>?> PollScanUntilCompleteAsync(
            string scanId,
            CancellationToken cancellationToken)
        {
            int attempts = 0;

            while (attempts < MaxStatusCheckAttempts)
            {
                try
                {
                    var status = await _client.GetScanStatusAsync(scanId, cancellationToken);
                    if (status == null)
                    {
                        _logger.LogWarning("Failed to get scan status for {ScanId}", scanId);
                        attempts++;
                        await Task.Delay(StatusCheckIntervalMs, cancellationToken);
                        continue;
                    }

                    _logger.LogDebug("Scan {ScanId} status: {Status} ({Progress}%)",
                        scanId, status.status, status.progress);

                    if (status.status == "FINISHED")
                    {
                        // Retrieve results
                        return await _client.GetScanResultsAsync(scanId, cancellationToken);
                    }

                    if (status.status == "FAILED" || status.status == "STOPPED")
                    {
                        _logger.LogWarning("Scan {ScanId} ended with status: {Status}", scanId, status.status);
                        return new List<SpiderFootFinding>();
                    }

                    attempts++;
                    await Task.Delay(StatusCheckIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling scan status for {ScanId}", scanId);
                    attempts++;
                    await Task.Delay(StatusCheckIntervalMs, cancellationToken);
                }
            }

            _logger.LogWarning("Scan {ScanId} polling timed out after {Attempts} attempts", scanId, MaxStatusCheckAttempts);
            return null;
        }

        /// <summary>
        /// Extracts module configuration from JSON string
        /// </summary>
        private Dictionary<string, string>? ExtractModuleConfig(string? configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
            {
                return null;
            }

            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configuration);
                if (config != null && config.TryGetValue("modules", out var modulesObj))
                {
                    if (modulesObj is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Object)
                    {
                        var result = new Dictionary<string, string>();
                        foreach (var prop in jsonEl.EnumerateObject())
                        {
                            result[prop.Name] = prop.Value.GetString() ?? "0";
                        }
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract module configuration");
            }

            return null;
        }

        /// <summary>
        /// Normalizes a SpiderFoot finding into a ToolFinding entity
        /// </summary>
        private ToolFinding NormalizeFinding(SpiderFootFinding spFinding)
        {
            return new ToolFinding
            {
                FindingType = NormalizeFindingType(spFinding.type),
                Value = spFinding.data,
                Description = $"Module: {spFinding.module}, Visibility: {spFinding.visibility}",
                Severity = NormalizeSeverity(spFinding.confidence),
                ConfidenceScore = ParseConfidenceScore(spFinding.confidence),
                Source = spFinding.module,
                RawData = JsonSerializer.Serialize(spFinding),
                RelatedEntities = "[]",
                ReferenceUrl = null,
                DiscoveredAt = DateTime.UtcNow,
                IsVerified = false,
                AnalystNotes = null
            };
        }

        /// <summary>
        /// Maps SpiderFoot finding types to normalized finding types
        /// </summary>
        private string NormalizeFindingType(string spType)
        {
            return spType.ToLowerInvariant() switch
            {
                "dns_name" => "domain",
                "ipv4_address" => "ip",
                "ipv6_address" => "ipv6",
                "email_addr" => "email",
                "username" => "username",
                "url" => "url",
                "web_framework" => "web_technology",
                "web_server_banner" => "service_info",
                "ssl_certificate" => "certificate",
                "asn" => "asn",
                "netblock" => "netblock",
                "phonenumber" => "phone",
                "human_name" => "person",
                "company_name" => "organization",
                "api_key" => "credential",
                "api_endpoint" => "api",
                "vulnerability" => "vulnerability",
                _ => spType
            };
        }

        /// <summary>
        /// Maps SpiderFoot confidence to severity level
        /// </summary>
        private string NormalizeSeverity(string confidence)
        {
            if (!int.TryParse(confidence, out int score))
            {
                return "Info";
            }

            return score switch
            {
                >= 90 => "Critical",
                >= 75 => "High",
                >= 50 => "Medium",
                >= 25 => "Low",
                _ => "Info"
            };
        }

        /// <summary>
        /// Parses confidence score from string
        /// </summary>
        private int? ParseConfidenceScore(string confidence)
        {
            if (int.TryParse(confidence, out int score))
            {
                return Math.Max(0, Math.Min(100, score));
            }
            return null;
        }
    }

    /// <summary>
    /// SpiderFoot configuration from appsettings
    /// </summary>
    public class SpiderFootConfig
    {
        public string Url { get; set; } = "http://localhost:5001";
        public int TimeoutSeconds { get; set; } = 600;
    }
}
