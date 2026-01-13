using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    /// Sherlock integration plugin for username enumeration across social media platforms
    /// Runs Sherlock as a subprocess and normalizes findings
    /// </summary>
    public class SherlockService : IExternalToolService
    {
        private readonly ILogger<SherlockService> _logger;
        private readonly SherlockConfig _config;
        private readonly RetryPolicy _retryPolicy;

        public string ToolName => "sherlock";
        public string ToolVersion => "0.14.2"; // Version should match deployed Sherlock

        public SherlockService(
            IOptionsMonitor<SherlockConfig> configMonitor,
            ILogger<SherlockService> logger)
        {
            _logger = logger;
            _config = configMonitor.CurrentValue;
            _retryPolicy = new RetryPolicy(
                maxAttempts: 2,
                initialDelayMs: 500,
                backoffMultiplier: 2.0,
                logger: logger);
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var output = await ExecuteSherlockCommandAsync("--version", cancellationToken);
                return !string.IsNullOrEmpty(output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sherlock availability check failed");
                return false;
            }
        }

        public ValidationResult ValidateConfiguration(string? configuration)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configuration))
                {
                    return ValidationResult.Success();
                }

                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configuration);
                if (config == null)
                {
                    return ValidationResult.Failure("Configuration must be valid JSON");
                }

                var errors = new List<string>();

                // Validate sites array if provided
                if (config.TryGetValue("sites", out var sitesObj))
                {
                    if (!(sitesObj is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Array))
                    {
                        errors.Add("'sites' field must be a JSON array");
                    }
                }

                // Validate timeout if provided
                if (config.TryGetValue("timeout", out var timeoutObj))
                {
                    if (!int.TryParse(timeoutObj?.ToString(), out int timeout) || timeout <= 0)
                    {
                        errors.Add("'timeout' must be a positive integer (seconds)");
                    }
                }

                // Validate exclude_sites if provided
                if (config.TryGetValue("exclude_sites", out var excludeObj))
                {
                    if (!(excludeObj is JsonElement jsonEl2 && jsonEl2.ValueKind == JsonValueKind.Array))
                    {
                        errors.Add("'exclude_sites' field must be a JSON array");
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
                _logger.LogInformation("Starting Sherlock scan for username: {Target}", target);

                // Build Sherlock command
                var args = BuildSherlockArgs(target, configuration);

                // Execute Sherlock with output to JSON
                var tempJsonFile = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"sherlock_{target}_{DateTime.UtcNow:yyyyMMddHHmmss}.json");

                args += $" --output {tempJsonFile} --json";

                var output = await ExecuteSherlockCommandAsync(args, cancellationToken);

                // Parse the JSON output
                var findings = await ParseSherlockOutputAsync(tempJsonFile, cancellationToken);

                if (findings == null || findings.Count == 0)
                {
                    _logger.LogWarning("Sherlock scan returned no results for username: {Target}", target);
                    findings = new List<SherlockFinding>();
                }

                result.RawData = JsonSerializer.Serialize(findings);
                result.FindingCount = findings.Count;
                result.Success = true;
                result.Status = "completed";
                result.Metadata = JsonSerializer.Serialize(new
                {
                    Target = target,
                    StartedAt = executionStartTime,
                    CompletedAt = DateTime.UtcNow,
                    DurationSeconds = (DateTime.UtcNow - executionStartTime).TotalSeconds,
                    FindingsCount = findings.Count
                });

                // Clean up temp file
                try
                {
                    if (System.IO.File.Exists(tempJsonFile))
                    {
                        System.IO.File.Delete(tempJsonFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to clean up temp file {FilePath}: {Error}", tempJsonFile, ex.Message);
                }

                _logger.LogInformation(
                    "Sherlock scan completed for username {Target}: {FindingCount} results",
                    target, findings.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Sherlock scan cancelled for username: {Target}", target);
                result.Success = false;
                result.ErrorMessage = "Scan was cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sherlock scan execution failed for username: {Target}", target);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<ToolStatusResponse> GetStatusAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            // Sherlock runs synchronously, so it's either completed or not running
            // In a real scenario, this could check a queue or background job status
            return await Task.FromResult(new ToolStatusResponse
            {
                Status = "completed",
                ProgressPercent = 100,
                Message = "Sherlock execution completed"
            });
        }

        public async Task<bool> CancelAsync(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            // Sherlock is synchronous, cancellation is handled via CancellationToken in ExecuteAsync
            return await Task.FromResult(true);
        }

        public async IAsyncEnumerable<ToolFinding> NormalizeResultsAsync(ToolExecutionResult result)
        {
            if (!result.Success || string.IsNullOrEmpty(result.RawData))
            {
                yield break;
            }

            List<SherlockFinding>? findings = null;
            try
            {
                findings = JsonSerializer.Deserialize<List<SherlockFinding>>(result.RawData);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Sherlock findings");
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
        /// Executes a Sherlock command
        /// </summary>
        private async Task<string> ExecuteSherlockCommandAsync(
            string args,
            CancellationToken cancellationToken)
        {
            return await _retryPolicy.ExecuteAsync(
                "Sherlock.Execute",
                async () =>
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = _config.PythonPath ?? "python3",
                        Arguments = $"{_config.SherlockPath} {args}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = processInfo };
                    process.Start();

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    var completedTask = await Task.WhenAny(
                        Task.Run(() => process.WaitForExit((int)(_config.TimeoutSeconds * 1000)), cancellationToken),
                        Task.Delay(_config.TimeoutSeconds * 1000 + 5000, cancellationToken));

                    if (!process.HasExited)
                    {
                        _logger.LogWarning("Sherlock process timeout, killing process");
                        process.Kill();
                        await Task.Delay(1000);
                    }

                    var output = await outputTask;
                    var error = await errorTask;

                    if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                    {
                        _logger.LogWarning("Sherlock process returned exit code {ExitCode}: {Error}",
                            process.ExitCode, error);
                    }

                    return output;
                });
        }

        /// <summary>
        /// Parses Sherlock JSON output file
        /// </summary>
        private async Task<List<SherlockFinding>?> ParseSherlockOutputAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Sherlock output file not found: {FilePath}", filePath);
                    return new List<SherlockFinding>();
                }

                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var results = JsonSerializer.Deserialize<Dictionary<string, SherlockSiteResult>>(json);

                if (results == null)
                {
                    return new List<SherlockFinding>();
                }

                var findings = new List<SherlockFinding>();
                foreach (var kvp in results)
                {
                    findings.Add(new SherlockFinding
                    {
                        Site = kvp.Key,
                        Found = kvp.Value.found,
                        Url = kvp.Value.url,
                        Username = kvp.Value.username,
                        ErrorType = kvp.Value.error_type,
                        ResponseTime = kvp.Value.response_time
                    });
                }

                return findings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Sherlock output from {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Builds Sherlock command arguments
        /// </summary>
        private string BuildSherlockArgs(string target, string? configuration)
        {
            var args = target; // Username is the first argument

            if (string.IsNullOrWhiteSpace(configuration))
            {
                return args;
            }

            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configuration);
                if (config == null)
                {
                    return args;
                }

                // Add site filtering if specified
                if (config.TryGetValue("sites", out var sitesObj) &&
                    sitesObj is JsonElement sitesEl && sitesEl.ValueKind == JsonValueKind.Array)
                {
                    var sites = string.Join(",", sitesEl.EnumerateArray()
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrEmpty(s)));

                    if (!string.IsNullOrEmpty(sites))
                    {
                        args += $" --site {sites}";
                    }
                }

                // Add excluded sites if specified
                if (config.TryGetValue("exclude_sites", out var excludeObj) &&
                    excludeObj is JsonElement excludeEl && excludeEl.ValueKind == JsonValueKind.Array)
                {
                    var excludeSites = string.Join(",", excludeEl.EnumerateArray()
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrEmpty(s)));

                    if (!string.IsNullOrEmpty(excludeSites))
                    {
                        args += $" --exclude {excludeSites}";
                    }
                }

                // Add timeout if specified
                if (config.TryGetValue("timeout", out var timeoutObj) &&
                    int.TryParse(timeoutObj?.ToString(), out int timeout) && timeout > 0)
                {
                    args += $" --timeout {timeout}";
                }

                // Add verbosity flag if requested
                if (config.TryGetValue("verbose", out var verboseObj) &&
                    verboseObj is JsonElement verboseEl && verboseEl.GetBoolean())
                {
                    args += " --verbose";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing Sherlock configuration");
            }

            return args;
        }

        /// <summary>
        /// Normalizes a Sherlock finding into a ToolFinding entity
        /// </summary>
        private ToolFinding NormalizeFinding(SherlockFinding finding)
        {
            var description = finding.Found
                ? $"Username found on {finding.Site}"
                : $"Username not found on {finding.Site}";

            if (!string.IsNullOrEmpty(finding.ErrorType))
            {
                description += $" (Error: {finding.ErrorType})";
            }

            return new ToolFinding
            {
                FindingType = "social_media",
                Value = $"{finding.Username}@{finding.Site}",
                Description = description,
                Severity = finding.Found ? "Medium" : "Info",
                ConfidenceScore = finding.Found ? 90 : null,
                Source = "sherlock",
                RawData = JsonSerializer.Serialize(finding),
                RelatedEntities = JsonSerializer.Serialize(new[] { finding.Site, finding.Username }),
                ReferenceUrl = finding.Found ? finding.Url : null,
                DiscoveredAt = DateTime.UtcNow,
                IsVerified = finding.Found,
                AnalystNotes = finding.Found
                    ? $"Active account on {finding.Site}"
                    : null
            };
        }
    }

    /// <summary>
    /// Sherlock configuration from appsettings
    /// </summary>
    public class SherlockConfig
    {
        public string SherlockPath { get; set; } = "/opt/sherlock/sherlock/sherlock.py";
        public string? PythonPath { get; set; } = "python3";
        public int TimeoutSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Sherlock finding from username search
    /// </summary>
    public class SherlockFinding
    {
        public string Username { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public bool Found { get; set; }
        public string? Url { get; set; }
        public string? ErrorType { get; set; }
        public double ResponseTime { get; set; }
    }

    /// <summary>
    /// Sherlock JSON output site result
    /// </summary>
    public class SherlockSiteResult
    {
        public bool found { get; set; }
        public string? url { get; set; }
        public string? username { get; set; }
        public string? error_type { get; set; }
        public double response_time { get; set; }
    }
}
