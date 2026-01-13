using System;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using OsintBackend.Services;

namespace OsintBackend.GraphQL.Mutations
{
    /// <summary>
    /// GraphQL mutations for external tool execution
    /// </summary>
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class ToolMutations
    {
        /// <summary>
        /// Executes an external OSINT tool (SpiderFoot, Sherlock, etc.) against an investigation target
        /// </summary>
        [GraphQLDescription("Execute an external OSINT tool for an investigation")]
        public async Task<ExecuteToolResponse> ExecuteToolAsync(
            [GraphQLDescription("Investigation ID")] int investigationId,
            [GraphQLDescription("Tool name (e.g., 'spiderfoot', 'sherlock')")] string toolName,
            [GraphQLDescription("Optional tool-specific configuration as JSON")] string? configuration,
            ToolOrchestrationService orchestrationService,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return ExecuteToolResponse.Failure("Tool name is required");
            }

            var result = await orchestrationService.ExecuteToolAsync(
                investigationId,
                toolName,
                configuration,
                cancellationToken);

            return new ExecuteToolResponse
            {
                Success = result.IsSuccess,
                Error = result.Error,
                ExecutionId = result.Data?.ExecutionId,
                ToolName = result.Data?.ToolName,
                Status = result.Data?.Status,
                FindingCount = result.Data?.FindingCount ?? 0,
                CompletedAt = result.Data?.CompletedAt
            };
        }

        /// <summary>
        /// Cancels a running tool execution
        /// </summary>
        [GraphQLDescription("Cancel a running tool execution")]
        public async Task<CancelExecutionResponse> CancelToolExecutionAsync(
            [GraphQLDescription("Tool execution ID")] int executionId,
            ToolOrchestrationService orchestrationService,
            CancellationToken cancellationToken)
        {
            if (executionId <= 0)
            {
                return CancelExecutionResponse.Failure("Invalid execution ID");
            }

            var success = await orchestrationService.CancelExecutionAsync(executionId, cancellationToken);

            return success
                ? CancelExecutionResponse.Success()
                : CancelExecutionResponse.Failure("Failed to cancel execution");
        }
    }

    /// <summary>
    /// Response from executing a tool
    /// </summary>
    public class ExecuteToolResponse
    {
        [GraphQLDescription("Whether the execution was initiated successfully")]
        public bool Success { get; set; }

        [GraphQLDescription("Error message if execution failed")]
        public string? Error { get; set; }

        [GraphQLDescription("Tool execution ID (can be used to check status)")]
        public int? ExecutionId { get; set; }

        [GraphQLDescription("Name of the tool that was executed")]
        public string? ToolName { get; set; }

        [GraphQLDescription("Current execution status")]
        public string? Status { get; set; }

        [GraphQLDescription("Number of findings discovered")]
        public int FindingCount { get; set; }

        [GraphQLDescription("When the execution completed (for synchronous execution)")]
        public DateTime? CompletedAt { get; set; }

        public static ExecuteToolResponse Failure(string error) =>
            new() { Success = false, Error = error };
    }

    /// <summary>
    /// Response from cancelling a tool execution
    /// </summary>
    public class CancelExecutionResponse
    {
        [GraphQLDescription("Whether the cancellation was successful")]
        public bool IsSuccess { get; set; }

        [GraphQLDescription("Error message if cancellation failed")]
        public string? Error { get; set; }

        public static CancelExecutionResponse Success() =>
            new() { IsSuccess = true };

        public static CancelExecutionResponse Failure(string error) =>
            new() { IsSuccess = false, Error = error };
    }
}
