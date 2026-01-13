using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using OsintBackend.Data;
using OsintBackend.Models;
using OsintBackend.Services;

namespace OsintBackend.GraphQL.Queries
{
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Query)]
    public class AiJobQueries
    {
        public Task<AiJob?> GetAiJobAsync(int jobId, [Service] IAiJobQueueService queueService, CancellationToken cancellationToken)
            => queueService.GetJobAsync(jobId, cancellationToken);

        public Task<IReadOnlyList<AiJob>> GetAiJobsForInvestigationAsync(
            int investigationId,
            int? take,
            [Service] IAiJobQueueService queueService,
            CancellationToken cancellationToken)
            => queueService.GetJobsForInvestigationAsync(investigationId, take.GetValueOrDefault(10), cancellationToken);
    }
}
