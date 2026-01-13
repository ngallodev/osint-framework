using System.Collections.Generic;
using System.Linq;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using OsintBackend.Data;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Queries
{
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Query)]
    public class ResultQueries
    {
        /// <summary>
        /// Get all results with pagination, filtering, and sorting support
        /// </summary>
        [UsePaging]
        [UseFiltering]
        [UseSorting]
        public IQueryable<OsintResult> GetResults([Service] OsintDbContext context)
            => context.Results.AsNoTracking();

        /// <summary>
        /// Get a single result by ID
        /// </summary>
        public Task<OsintResult?> GetResultByIdAsync(
            int id,
            [Service] OsintDbContext context)
        {
            return context.Results
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        /// <summary>
        /// Get all results for a specific investigation, sorted by collection date (newest first)
        /// </summary>
        public Task<List<OsintResult>> GetResultsByInvestigationIdAsync(
            int investigationId,
            [Service] OsintDbContext context)
        {
            return context.Results
                .Where(r => r.OsintInvestigationId == investigationId)
                .OrderByDescending(r => r.CollectedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get results by data type with pagination and filtering
        /// </summary>
        [UsePaging]
        [UseFiltering]
        [UseSorting]
        public IQueryable<OsintResult> GetResultsByType(
            string dataType,
            [Service] OsintDbContext context)
            => context.Results
                .Where(r => r.DataType == dataType)
                .AsNoTracking();

        /// <summary>
        /// Get results by tool name with pagination and filtering
        /// </summary>
        [UsePaging]
        [UseFiltering]
        [UseSorting]
        public IQueryable<OsintResult> GetResultsByTool(
            string toolName,
            [Service] OsintDbContext context)
            => context.Results
                .Where(r => r.ToolName == toolName)
                .AsNoTracking();

        /// <summary>
        /// Get result count for an investigation
        /// </summary>
        public async Task<int> GetResultCountAsync(
            int investigationId,
            [Service] OsintDbContext context)
            => await context.Results
                .CountAsync(r => r.OsintInvestigationId == investigationId);

        /// <summary>
        /// Get unique data types that have been collected
        /// </summary>
        public async Task<List<string>> GetAvailableDataTypesAsync(
            [Service] OsintDbContext context)
            => await context.Results
                .Select(r => r.DataType)
                .Distinct()
                .OrderBy(dt => dt)
                .ToListAsync();

        /// <summary>
        /// Get unique tools that have been used
        /// </summary>
        public async Task<List<string>> GetAvailableToolsAsync(
            [Service] OsintDbContext context)
            => await context.Results
                .Select(r => r.ToolName)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
    }
}
