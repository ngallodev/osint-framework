using OsintBackend.Models;

namespace OsintBackend.Services
{
    public interface IAiJobQueueService
    {
        Task<AiJob> EnqueueAsync(int investigationId, string jobType, string? model, string? prompt, bool debug = false, CancellationToken cancellationToken = default);
        Task<AiJob?> GetJobAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AiJob>> GetJobsForInvestigationAsync(int investigationId, int take = 10, CancellationToken cancellationToken = default);
        Task<AiJob?> TryClaimNextJobAsync(CancellationToken cancellationToken = default);
        Task MarkJobSucceededAsync(int jobId, AiJobCompletionPayload payload, CancellationToken cancellationToken = default);
        Task<bool> MarkJobFailedAsync(int jobId, AiJobFailurePayload payload, CancellationToken cancellationToken = default);
        Task<AiJob?> RetryJobAsync(int jobId, CancellationToken cancellationToken = default);
    }
}
