using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using OsintBackend.Data;
using OsintBackend.Models;
using Microsoft.EntityFrameworkCore;
using OsintBackend.Plugins.Implementations;
using OsintBackend.Services;

namespace OsintBackend.GraphQL.Mutations
{
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class AiMutations
    {
        /// <summary>
        /// Analyze investigation results using AI
        /// </summary>
        public async Task<AiAnalysisResult> AnalyzeInvestigation(
            int investigationId,
            string? model = null,
            [Service] OsintDbContext context = null!,
            [Service] AiAnalysisPlugin aiPlugin = null!)
        {
            var results = await context.Results
                .Where(r => r.OsintInvestigationId == investigationId)
                .ToListAsync();

            if (!results.Any())
                return new AiAnalysisResult { Success = false, Error = "No results found for analysis" };

            try
            {
                // Pass model to plugin if provided
                var analysis = await aiPlugin.AnalyzeInvestigationAsync(investigationId, results, model);

                return new AiAnalysisResult
                {
                    Success = true,
                    Analysis = analysis,
                    InvestigationId = investigationId,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new AiAnalysisResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Generate intelligent inferences from investigation results
        /// </summary>
        public async Task<AiAnalysisResult> GenerateInferences(
            int investigationId,
            string? model = null,
            [Service] OsintDbContext context = null!,
            [Service] AiAnalysisPlugin aiPlugin = null!)
        {
            var results = await context.Results
                .Where(r => r.OsintInvestigationId == investigationId)
                .ToListAsync();

            if (!results.Any())
                return new AiAnalysisResult { Success = false, Error = "No results found for inference generation" };

            try
            {
                // Pass model to plugin if provided
                var inferences = await aiPlugin.GenerateInferencesAsync(results, model);

                return new AiAnalysisResult
                {
                    Success = true,
                    Analysis = inferences,
                    InvestigationId = investigationId,
                    GeneratedAt = DateTime.UtcNow,
                    AnalysisType = "Inferences"
                };
            }
            catch (Exception ex)
            {
                return new AiAnalysisResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<OllamaStatus> GetOllamaStatus([Service] IOllamaService ollamaService)
        {
            var isAvailable = await ollamaService.IsServiceAvailableAsync();
            var models = await ollamaService.GetAvailableModelsAsync();

            return new OllamaStatus
            {
                IsAvailable = isAvailable,
                AvailableModels = models,
                Status = isAvailable ? "Connected" : "Disconnected"
            };
        }
    }

    public class AiAnalysisResult
    {
        public bool Success { get; set; }
        public string? Analysis { get; set; }
        public string? Error { get; set; }
        public int InvestigationId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string AnalysisType { get; set; } = "Analysis";
    }

    public class OllamaStatus
    {
        public bool IsAvailable { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> AvailableModels { get; set; } = new List<string>();
    }
}
