using System.Linq;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using OsintBackend.Data;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Queries
{
    /// <summary>
    /// GraphQL queries for external tool executions and findings
    /// </summary>
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Query)]
    public class ToolQueries
    {
        /// <summary>
        /// Gets a specific tool execution by ID
        /// </summary>
        [GraphQLDescription("Get a tool execution by ID")]
        public ToolExecution? GetToolExecution(
            [GraphQLDescription("Execution ID")] int executionId,
            OsintDbContext dbContext)
        {
            return dbContext.ToolExecutions
                .FirstOrDefault(e => e.Id == executionId);
        }

        /// <summary>
        /// Gets all tool executions for an investigation
        /// </summary>
        [GraphQLDescription("Get all tool executions for an investigation")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolExecution> GetInvestigationToolExecutions(
            [GraphQLDescription("Investigation ID")] int investigationId,
            OsintDbContext dbContext)
        {
            return dbContext.ToolExecutions
                .Where(e => e.OsintInvestigationId == investigationId);
        }

        /// <summary>
        /// Gets all tool executions
        /// </summary>
        [GraphQLDescription("Get all tool executions")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolExecution> GetToolExecutions(
            OsintDbContext dbContext)
        {
            return dbContext.ToolExecutions;
        }

        /// <summary>
        /// Gets findings from a specific tool execution
        /// </summary>
        [GraphQLDescription("Get findings from a tool execution")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolFinding> GetExecutionFindings(
            [GraphQLDescription("Execution ID")] int executionId,
            OsintDbContext dbContext)
        {
            return dbContext.ToolFindings
                .Where(f => f.ToolExecutionId == executionId);
        }

        /// <summary>
        /// Gets all findings for an investigation (across all tool executions)
        /// </summary>
        [GraphQLDescription("Get all findings for an investigation")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolFinding> GetInvestigationFindings(
            [GraphQLDescription("Investigation ID")] int investigationId,
            OsintDbContext dbContext)
        {
            return dbContext.ToolFindings
                .Where(f => f.ToolExecution.OsintInvestigationId == investigationId);
        }

        /// <summary>
        /// Gets findings of a specific type
        /// </summary>
        [GraphQLDescription("Get findings by type")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolFinding> GetFindingsByType(
            [GraphQLDescription("Finding type (e.g., 'domain', 'email', 'ip')")] string findingType,
            OsintDbContext dbContext)
        {
            return dbContext.ToolFindings
                .Where(f => f.FindingType == findingType);
        }

        /// <summary>
        /// Gets findings from a specific tool
        /// </summary>
        [GraphQLDescription("Get findings from a specific tool")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolFinding> GetFindingsByTool(
            [GraphQLDescription("Tool name (e.g., 'spiderfoot', 'sherlock')")] string toolName,
            OsintDbContext dbContext)
        {
            return dbContext.ToolFindings
                .Where(f => f.ToolExecution.ToolName == toolName);
        }

        /// <summary>
        /// Gets findings with a minimum severity level
        /// </summary>
        [GraphQLDescription("Get findings by severity level")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolFinding> GetFindingsBySeverity(
            [GraphQLDescription("Minimum severity level (Info, Low, Medium, High, Critical)")] string minSeverity,
            OsintDbContext dbContext)
        {
            var severityOrder = new[] { "Info", "Low", "Medium", "High", "Critical" };
            var minIndex = System.Array.IndexOf(severityOrder, minSeverity);

            if (minIndex < 0)
            {
                minIndex = 0; // Default to Info if invalid
            }

            var minSeverities = severityOrder.Skip(minIndex).ToList();

            return dbContext.ToolFindings
                .Where(f => minSeverities.Contains(f.Severity));
        }

        /// <summary>
        /// Gets findings that have been verified
        /// </summary>
        [GraphQLDescription("Get verified findings")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolFinding> GetVerifiedFindings(
            OsintDbContext dbContext)
        {
            return dbContext.ToolFindings
                .Where(f => f.IsVerified);
        }

        /// <summary>
        /// Gets the count of tool executions for an investigation
        /// </summary>
        [GraphQLDescription("Get count of tool executions for an investigation")]
        public int GetToolExecutionCount(
            [GraphQLDescription("Investigation ID")] int investigationId,
            OsintDbContext dbContext)
        {
            return dbContext.ToolExecutions
                .Count(e => e.OsintInvestigationId == investigationId);
        }

        /// <summary>
        /// Gets the count of findings for an investigation
        /// </summary>
        [GraphQLDescription("Get count of findings for an investigation")]
        public int GetFindingCount(
            [GraphQLDescription("Investigation ID")] int investigationId,
            OsintDbContext dbContext)
        {
            return dbContext.ToolFindings
                .Count(f => f.ToolExecution.OsintInvestigationId == investigationId);
        }

        /// <summary>
        /// Gets findings by confidence score threshold
        /// </summary>
        [GraphQLDescription("Get findings by minimum confidence score")]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<ToolFinding> GetFindingsByConfidence(
            [GraphQLDescription("Minimum confidence score (0-100)")] int minConfidence,
            OsintDbContext dbContext)
        {
            return dbContext.ToolFindings
                .Where(f => f.ConfidenceScore.HasValue && f.ConfidenceScore >= minConfidence);
        }
    }
}
