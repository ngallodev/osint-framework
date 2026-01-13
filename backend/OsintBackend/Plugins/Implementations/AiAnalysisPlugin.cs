using OsintBackend.Models;
using OsintBackend.Plugins.Interfaces;
using OsintBackend.Services;

namespace OsintBackend.Plugins.Implementations
{
    public class AiAnalysisPlugin : IOsintPlugin
    {
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<AiAnalysisPlugin> _logger;

        public string ToolName => "AI Analysis";
        public bool IsEnabled => true;

        public AiAnalysisPlugin(IOllamaService ollamaService, ILogger<AiAnalysisPlugin> logger)
        {
            _ollamaService = ollamaService;
            _logger = logger;
        }

        public Task<OsintResult> ExecuteAsync(string target, string investigationType, CancellationToken cancellationToken)
        {
            // This plugin analyzes existing results, so it needs context from the investigation
            // We'll return a placeholder and the actual analysis will be triggered separately
            var placeholderResult = new OsintResult
            {
                ToolName = ToolName,
                DataType = "AIAnalysis",
                RawData = "{}",
                Summary = "AI Analysis plugin registered - use dedicated AI analysis endpoints",
                CollectedAt = DateTime.UtcNow,
                ConfidenceScore = "N/A"
            };

            return Task.FromResult(placeholderResult);
        }

        public bool SupportsOperation(string operationType) 
            => operationType == "AIAnalysis";

        /// <summary>
        /// Analyze investigation results using the specified AI model
        /// </summary>
        public async Task<string> AnalyzeInvestigationAsync(int investigationId, List<OsintResult> results, string? model = null)
        {
            if (!results.Any())
                return "No data available for analysis";

            return await _ollamaService.AnalyzeOsintDataAsync(results, "comprehensive analysis", model);
        }

        /// <summary>
        /// Generate intelligent inferences from investigation results using the specified AI model
        /// </summary>
        public async Task<string> GenerateInferencesAsync(List<OsintResult> results, string? model = null)
        {
            // If a specific model is provided, we could use it here
            // For now, GenerateInferencesAsync will use the default/configured model
            return await _ollamaService.GenerateInferencesAsync(results, model);
        }
    }
}
