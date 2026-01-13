using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using OsintBackend.Data;
using OsintBackend.Models;
using OsintBackend.GraphQL.InputTypes;
using OsintBackend.GraphQL.Types;

namespace OsintBackend.GraphQL.Mutations
{
    /// <summary>
    /// Result ingestion mutations - for adding OSINT data points to investigations
    /// </summary>
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class ResultMutations
    {
        /// <summary>
        /// Ingest a single OSINT result/data point
        /// </summary>
        public async Task<MutationResponse<OsintResult>> IngestResult(
            IngestResultInput input,
            [Service] OsintDbContext context)
        {
            try
            {
                // Validate that investigation exists
                var investigation = await context.Investigations
                    .FirstOrDefaultAsync(i => i.Id == input.InvestigationId);

                if (investigation == null)
                    return new MutationResponse<OsintResult>
                    {
                        Success = false,
                        Error = $"Investigation with ID {input.InvestigationId} not found"
                    };

                if (string.IsNullOrWhiteSpace(input.ToolName))
                    return new MutationResponse<OsintResult>
                    {
                        Success = false,
                        Error = "ToolName is required"
                    };

                if (string.IsNullOrWhiteSpace(input.DataType))
                    return new MutationResponse<OsintResult>
                    {
                        Success = false,
                        Error = "DataType is required"
                    };

                if (string.IsNullOrWhiteSpace(input.RawData))
                    return new MutationResponse<OsintResult>
                    {
                        Success = false,
                        Error = "RawData is required"
                    };

                var result = new OsintResult
                {
                    OsintInvestigationId = input.InvestigationId,
                    ToolName = input.ToolName,
                    DataType = input.DataType,
                    RawData = input.RawData,
                    Summary = input.Summary,
                    ConfidenceScore = input.ConfidenceScore,
                    CollectedAt = DateTime.UtcNow
                };

                context.Results.Add(result);
                await context.SaveChangesAsync();

                return new MutationResponse<OsintResult>
                {
                    Success = true,
                    Data = result,
                    Message = $"Result ingested successfully with ID {result.Id}"
                };
            }
            catch (Exception ex)
            {
                return new MutationResponse<OsintResult>
                {
                    Success = false,
                    Error = $"Failed to ingest result: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Bulk ingest multiple OSINT results for an investigation
        /// </summary>
        public async Task<BulkIngestResponse> BulkIngestResults(
            BulkIngestResultInput input,
            [Service] OsintDbContext context)
        {
            try
            {
                // Validate that investigation exists
                var investigation = await context.Investigations
                    .FirstOrDefaultAsync(i => i.Id == input.InvestigationId);

                if (investigation == null)
                    return new BulkIngestResponse
                    {
                        Success = false,
                        Error = $"Investigation with ID {input.InvestigationId} not found",
                        TotalIngested = 0
                    };

                if (input.Results == null || input.Results.Count == 0)
                    return new BulkIngestResponse
                    {
                        Success = false,
                        Error = "No results provided for ingestion",
                        TotalIngested = 0
                    };

                var ingestedResults = new List<OsintResult>();
                var validationErrors = new List<string>();

                // Validate and prepare results
                foreach (var (result, index) in input.Results.Select((r, i) => (r, i)))
                {
                    var errors = new List<string>();

                    if (string.IsNullOrWhiteSpace(result.ToolName))
                        errors.Add("ToolName is required");
                    if (string.IsNullOrWhiteSpace(result.DataType))
                        errors.Add("DataType is required");
                    if (string.IsNullOrWhiteSpace(result.RawData))
                        errors.Add("RawData is required");

                    if (errors.Any())
                    {
                        validationErrors.Add($"Result {index}: {string.Join(", ", errors)}");
                        continue;
                    }

                    var osintResult = new OsintResult
                    {
                        OsintInvestigationId = input.InvestigationId,
                        ToolName = result.ToolName,
                        DataType = result.DataType,
                        RawData = result.RawData,
                        Summary = result.Summary,
                        ConfidenceScore = result.ConfidenceScore,
                        CollectedAt = DateTime.UtcNow
                    };

                    ingestedResults.Add(osintResult);
                }

                // If there were validation errors, report them
                if (ingestedResults.Count == 0)
                    return new BulkIngestResponse
                    {
                        Success = false,
                        Error = "All results failed validation",
                        ValidationErrors = validationErrors,
                        TotalIngested = 0
                    };

                // Add all valid results to the database
                context.Results.AddRange(ingestedResults);
                await context.SaveChangesAsync();

                return new BulkIngestResponse
                {
                    Success = true,
                    TotalIngested = ingestedResults.Count,
                    Message = $"Successfully ingested {ingestedResults.Count} results",
                    ValidationErrors = validationErrors.Any() ? validationErrors : null
                };
            }
            catch (Exception ex)
            {
                return new BulkIngestResponse
                {
                    Success = false,
                    Error = $"Failed to bulk ingest results: {ex.Message}",
                    TotalIngested = 0
                };
            }
        }

        /// <summary>
        /// Delete a single result
        /// </summary>
        public async Task<MutationResponse<bool>> DeleteResult(
            int resultId,
            [Service] OsintDbContext context)
        {
            try
            {
                var result = await context.Results
                    .FirstOrDefaultAsync(r => r.Id == resultId);

                if (result == null)
                    return new MutationResponse<bool>
                    {
                        Success = false,
                        Error = $"Result with ID {resultId} not found"
                    };

                context.Results.Remove(result);
                await context.SaveChangesAsync();

                return new MutationResponse<bool>
                {
                    Success = true,
                    Data = true,
                    Message = $"Result {resultId} deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new MutationResponse<bool>
                {
                    Success = false,
                    Error = $"Failed to delete result: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Update a result's summary or confidence score
        /// </summary>
        public async Task<MutationResponse<OsintResult>> UpdateResultSummary(
            int resultId,
            string? summary,
            string? confidenceScore,
            [Service] OsintDbContext context)
        {
            try
            {
                var result = await context.Results
                    .FirstOrDefaultAsync(r => r.Id == resultId);

                if (result == null)
                    return new MutationResponse<OsintResult>
                    {
                        Success = false,
                        Error = $"Result with ID {resultId} not found"
                    };

                if (!string.IsNullOrWhiteSpace(summary))
                    result.Summary = summary;

                if (!string.IsNullOrWhiteSpace(confidenceScore))
                    result.ConfidenceScore = confidenceScore;

                context.Results.Update(result);
                await context.SaveChangesAsync();

                return new MutationResponse<OsintResult>
                {
                    Success = true,
                    Data = result,
                    Message = "Result updated successfully"
                };
            }
            catch (Exception ex)
            {
                return new MutationResponse<OsintResult>
                {
                    Success = false,
                    Error = $"Failed to update result: {ex.Message}"
                };
            }
        }
    }

    /// <summary>
    /// Response for bulk ingestion operations
    /// </summary>
    public class BulkIngestResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public int TotalIngested { get; set; }
        public List<string>? ValidationErrors { get; set; }
    }
}
