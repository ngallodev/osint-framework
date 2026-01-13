using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OsintBackend.Services
{
    /// <summary>
    /// REST client for SpiderFoot OSINT tool
    /// Communicates with SpiderFoot API to start scans, get status, and retrieve results
    /// </summary>
    public class SpiderFootClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<SpiderFootClient> _logger;
        private readonly RetryPolicy _retryPolicy;

        public SpiderFootClient(
            HttpClient httpClient,
            string baseUrl,
            ILogger<SpiderFootClient> logger)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl.TrimEnd('/');
            _logger = logger;
            _retryPolicy = new RetryPolicy(
                maxAttempts: 3,
                initialDelayMs: 1000,
                backoffMultiplier: 2.0,
                logger: logger);
        }

        /// <summary>
        /// Tests connectivity to SpiderFoot API
        /// </summary>
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(
                    "SpiderFoot.TestConnection",
                    async () => await _httpClient.GetAsync($"{_baseUrl}/api/info", cancellationToken));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SpiderFoot connection test failed");
                return false;
            }
        }

        /// <summary>
        /// Starts a new scan in SpiderFoot
        /// </summary>
        public async Task<SpiderFootScanResponse?> StartScanAsync(
            string targetName,
            string targetValue,
            Dictionary<string, string>? moduleConfig = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new
                {
                    scanname = targetName,
                    scantarget = targetValue,
                    modules = moduleConfig ?? GetDefaultModules(),
                    usecase = "all"
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _retryPolicy.ExecuteAsync(
                    "SpiderFoot.StartScan",
                    async () => await _httpClient.PostAsync($"{_baseUrl}/api/scanstart", content, cancellationToken));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SpiderFoot scan start failed: {StatusCode}", response.StatusCode);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<SpiderFootScanResponse>(jsonContent);

                _logger.LogInformation("Started SpiderFoot scan: {ScanId}", result?.scan_id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting SpiderFoot scan for target {Target}", targetValue);
                return null;
            }
        }

        /// <summary>
        /// Gets the status of a running scan
        /// </summary>
        public async Task<SpiderFootStatusResponse?> GetScanStatusAsync(
            string scanId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(
                    "SpiderFoot.GetStatus",
                    async () => await _httpClient.GetAsync($"{_baseUrl}/api/scanstatus/{scanId}", cancellationToken));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SpiderFoot status request failed: {StatusCode}", response.StatusCode);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<SpiderFootStatusResponse>(jsonContent);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SpiderFoot scan status for scan {ScanId}", scanId);
                return null;
            }
        }

        /// <summary>
        /// Gets results from a completed scan
        /// </summary>
        public async Task<List<SpiderFootFinding>?> GetScanResultsAsync(
            string scanId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(
                    "SpiderFoot.GetResults",
                    async () => await _httpClient.GetAsync($"{_baseUrl}/api/scanresults/{scanId}", cancellationToken));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SpiderFoot results request failed: {StatusCode}", response.StatusCode);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var results = JsonSerializer.Deserialize<List<SpiderFootFinding>>(jsonContent);

                _logger.LogInformation("Retrieved {Count} findings from SpiderFoot scan {ScanId}",
                    results?.Count ?? 0, scanId);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SpiderFoot scan results for scan {ScanId}", scanId);
                return null;
            }
        }

        /// <summary>
        /// Deletes/stops a scan
        /// </summary>
        public async Task<bool> StopScanAsync(string scanId, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(
                    "SpiderFoot.StopScan",
                    async () => await _httpClient.GetAsync($"{_baseUrl}/api/scanstop/{scanId}", cancellationToken));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping SpiderFoot scan {ScanId}", scanId);
                return false;
            }
        }

        /// <summary>
        /// Gets default SpiderFoot modules to enable
        /// </summary>
        private static Dictionary<string, string> GetDefaultModules()
        {
            return new Dictionary<string, string>
            {
                ["sfp_dns"] = "1",
                ["sfp_dnsbrute"] = "1",
                ["sfp_whois"] = "1",
                ["sfp_dnsresolve"] = "1",
                ["sfp_subdomainsgather"] = "1",
                ["sfp_emailformat"] = "1",
                ["sfp_filtration"] = "1",
                ["sfp_names"] = "1",
                ["sfp_pagelinks"] = "1",
                ["sfp_pagetext"] = "1",
                ["sfp_pageextractor"] = "1",
                ["sfp_spider"] = "1",
                ["sfp_sslcert"] = "1",
                ["sfp_dnslookup"] = "1",
                ["sfp_havebeenpwned"] = "1",
                ["sfp_hibp"] = "1"
            };
        }
    }

    /// <summary>
    /// SpiderFoot API response models
    /// </summary>
    public class SpiderFootScanResponse
    {
        public string scan_id { get; set; } = string.Empty;
        public string scan_name { get; set; } = string.Empty;
        public string success { get; set; } = "false";
    }

    public class SpiderFootStatusResponse
    {
        public string status { get; set; } = "unknown";
        public int progress { get; set; }
        public string description { get; set; } = string.Empty;
    }

    public class SpiderFootFinding
    {
        public string type { get; set; } = string.Empty;
        public string data { get; set; } = string.Empty;
        public string module { get; set; } = string.Empty;
        public string confidence { get; set; } = "0";
        public string visibility { get; set; } = "unknown";
        public Dictionary<string, object>? extra { get; set; }
    }
}
