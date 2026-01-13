using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OsintBackend.Data;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    /// <summary>
    /// Orchestrates execution of external tools and persistence of their findings
    /// Handles end-to-end workflow: validation, execution, normalization, and storage
    /// </summary>
    public class ToolOrchestrationService
    {
        private readonly OsintDbContext _dbContext;
        private readonly IToolServiceFactory _toolServiceFactory;
        private readonly ILogger<ToolOrchestrationService> _logger;

        public ToolOrchestrationService(
            OsintDbContext dbContext,
            IToolServiceFactory toolServiceFactory,
            ILogger<ToolOrchestrationService> logger)
        {
            _dbContext = dbContext;
            _toolServiceFactory = toolServiceFactory;
            _logger = logger;
        }

        /// <summary>
        /// Executes a tool against an investigation target and persists findings
        /// </summary>
        public async Task<ToolExecuteResponse> ExecuteToolAsync(
            int investigationId,
            string toolName,
            string? configuration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate investigation exists
                var investigation = await _dbContext.Investigations
                    .FirstOrDefaultAsync(i => i.Id == investigationId, cancellationToken);

                if (investigation == null)
                {
                    return ToolExecuteResponse.Failure($"Investigation {investigationId} not found");
                }

                // Get the tool service
                var toolService = _toolServiceFactory.ResolveService(toolName);
                if (toolService == null)
                {
                    var availableTools = string.Join(", ", _toolServiceFactory.GetAvailableTools());
                    return ToolExecuteResponse.Failure(
                        $"Tool '{toolName}' is not registered. Available tools: {availableTools}");
                }

                // Validate tool is available
                if (!await toolService.IsAvailableAsync(cancellationToken))
                {
                    return ToolExecuteResponse.Failure($"Tool '{toolName}' is not available");
                }

                // Validate configuration
                var validationResult = toolService.ValidateConfiguration(configuration);
                if (!validationResult.IsValid)
                {
                    return ToolExecuteResponse.Failure(
                        $"Invalid configuration: {string.Join(", ", validationResult.Errors)}");
                }

                // Create execution record
                var execution = new ToolExecution
                {
                    ToolName = toolName,
                    OsintInvestigationId = investigationId,
                    Status = Models.ToolExecutionStatus.Running,
                    Configuration = configuration ?? "{}",
                    StartedAt = DateTime.UtcNow
                };

                _dbContext.ToolExecutions.Add(execution);
                await _dbContext.SaveChangesAsync(cancellationToken);

                int executionId = execution.Id;

                _logger.LogInformation(
                    "Started execution of tool '{ToolName}' for investigation {InvestigationId} " +
                    "(execution ID: {ExecutionId})",
                    toolName, investigationId, executionId);

                // Execute the tool
                ToolExecutionResult executionResult;
                try
                {
                    executionResult = await toolService.ExecuteAsync(
                        investigation.Target,
                        configuration,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Tool execution failed for '{ToolName}' (investigation {InvestigationId})",
                        toolName, investigationId);

                    execution.Status = Models.ToolExecutionStatus.Failed;
                    execution.ErrorMessage = ex.Message;
                    execution.CompletedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    return ToolExecuteResponse.Failure($"Tool execution failed: {ex.Message}");
                }

                // Update execution record with results
                execution.FindingCount = executionResult.FindingCount;
                execution.ExecutionMetadata = executionResult.Metadata;
                execution.CompletedAt = DateTime.UtcNow;

                if (executionResult.Success)
                {
                    execution.Status = Models.ToolExecutionStatus.Completed;
                }
                else
                {
                    execution.Status = Models.ToolExecutionStatus.Failed;
                    execution.ErrorMessage = executionResult.ErrorMessage;
                }

                // Normalize and persist findings
                int findingsCount = 0;
                try
                {
                    await foreach (var finding in toolService.NormalizeResultsAsync(executionResult))
                    {
                        finding.ToolExecutionId = executionId;
                        _dbContext.ToolFindings.Add(finding);
                        findingsCount++;

                        // Batch save every 100 findings to avoid memory issues with large result sets
                        if (findingsCount % 100 == 0)
                        {
                            await _dbContext.SaveChangesAsync(cancellationToken);
                            _logger.LogDebug("Persisted {Count} findings for execution {ExecutionId}",
                                findingsCount, executionId);
                        }
                    }

                    // Final save for remaining findings
                    if (findingsCount % 100 != 0)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }

                    _logger.LogInformation(
                        "Successfully completed execution of '{ToolName}' for investigation {InvestigationId} " +
                        "with {FindingCount} findings",
                        toolName, investigationId, findingsCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to normalize/persist findings for execution {ExecutionId}",
                        executionId);

                    execution.Status = Models.ToolExecutionStatus.Failed;
                    execution.ErrorMessage = $"Failed to persist findings: {ex.Message}";
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    return ToolExecuteResponse.Failure($"Failed to persist findings: {ex.Message}");
                }

                // Final update to execution record
                await _dbContext.SaveChangesAsync(cancellationToken);

                return ToolExecuteResponse.Success(new ToolExecutionData
                {
                    ExecutionId = executionId,
                    ToolName = toolName,
                    Status = execution.Status.ToString(),
                    FindingCount = findingsCount,
                    CompletedAt = execution.CompletedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error during tool orchestration for '{ToolName}' " +
                    "(investigation {InvestigationId})",
                    toolName, investigationId);

                return ToolExecuteResponse.Failure($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancels a running tool execution
        /// </summary>
        public async Task<bool> CancelExecutionAsync(int executionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var execution = await _dbContext.ToolExecutions
                    .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

                if (execution == null)
                {
                    _logger.LogWarning("Execution {ExecutionId} not found for cancellation", executionId);
                    return false;
                }

                if (execution.Status != Models.ToolExecutionStatus.Running)
                {
                    _logger.LogWarning(
                        "Cannot cancel execution {ExecutionId} with status {Status}",
                        executionId, execution.Status);
                    return false;
                }

                var toolService = _toolServiceFactory.ResolveService(execution.ToolName);
                if (toolService == null)
                {
                    _logger.LogWarning("Tool service '{ToolName}' not found", execution.ToolName);
                    return false;
                }

                var cancelled = await toolService.CancelAsync(execution.ExecutionMetadata, cancellationToken);

                if (cancelled)
                {
                    execution.Status = Models.ToolExecutionStatus.Cancelled;
                    execution.CompletedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Successfully cancelled execution {ExecutionId}", executionId);
                }

                return cancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling execution {ExecutionId}", executionId);
                return false;
            }
        }

    }

    /// <summary>
    /// Response from tool execution
    /// </summary>
    public class ToolExecuteResponse
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public ToolExecutionData? Data { get; set; }

        public static ToolExecuteResponse Success(ToolExecutionData data) =>
            new() { IsSuccess = true, Data = data };

        public static ToolExecuteResponse Failure(string error) =>
            new() { IsSuccess = false, Error = error };
    }

    /// <summary>
    /// Data returned from successful tool execution
    /// </summary>
    public class ToolExecutionData
    {
        public int ExecutionId { get; set; }
        public string ToolName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int FindingCount { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
