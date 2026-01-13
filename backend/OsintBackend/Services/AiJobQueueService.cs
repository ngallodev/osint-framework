using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsintBackend.Data;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    public class AiJobQueueService : IAiJobQueueService
    {
        private readonly OsintDbContext _context;
        private readonly ILogger<AiJobQueueService> _logger;
        private readonly AiJobQueueOptions _options;

        public AiJobQueueService(
            OsintDbContext context,
            ILogger<AiJobQueueService> logger,
            IOptions<AiJobQueueOptions> options)
        {
            _context = context;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<AiJob> EnqueueAsync(int investigationId, string jobType, string? model, string? prompt, bool debug = false, CancellationToken cancellationToken = default)
        {
            var exists = await _context.Investigations.AnyAsync(i => i.Id == investigationId, cancellationToken);
            if (!exists)
            {
                throw new InvalidOperationException($"Investigation {investigationId} not found");
            }

            var job = new AiJob
            {
                OsintInvestigationId = investigationId,
                JobType = jobType,
                Model = model,
                Prompt = prompt,
                Debug = debug,
                Status = AiJobStatus.Queued,
                CreatedAt = DateTime.UtcNow
            };

            await _context.AiJobs.AddAsync(job, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("AI job {JobId} queued for investigation {InvestigationId} ({JobType}) [Debug: {Debug}]", job.Id, investigationId, jobType, debug);

            return job;
        }

        public Task<AiJob?> GetJobAsync(int id, CancellationToken cancellationToken = default)
        {
            return _context.AiJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<AiJob>> GetJobsForInvestigationAsync(int investigationId, int take = 10, CancellationToken cancellationToken = default)
        {
            return await _context.AiJobs
                .AsNoTracking()
                .Where(j => j.OsintInvestigationId == investigationId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<AiJob?> TryClaimNextJobAsync(CancellationToken cancellationToken = default)
        {
            var job = await _context.AiJobs
                .Where(j => j.Status == AiJobStatus.Queued && j.AttemptCount < _options.MaxAttempts)
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (job is null)
            {
                return null;
            }

            job.Status = AiJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.AttemptCount += 1;
            job.Error = null;
            job.WorkerHost = Environment.MachineName;
            job.LastAttemptStartedAt = DateTime.UtcNow;
            job.LastAttemptCompletedAt = null;
            job.LastDurationMilliseconds = null;
            job.Result = null;
            job.ResultFormat = AiJobResultFormats.MarkdownSectionsV1;
            job.LastError = null;
            job.ErrorInfo = null;
            job.StructuredResult = null;
            job.DebugInfo = null;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Claimed AI job {JobId} ({JobType}) for processing", job.Id, job.JobType);

            return job;
        }

        public async Task MarkJobSucceededAsync(int jobId, AiJobCompletionPayload payload, CancellationToken cancellationToken = default)
        {
            var job = await _context.AiJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                _logger.LogWarning("Attempted to mark job {JobId} as succeeded but it was not found", jobId);
                return;
            }

            job.Status = AiJobStatus.Succeeded;
            job.Result = payload.RawResult;
            job.ResultFormat = payload.ResultFormat;
            job.StructuredResult = payload.StructuredResult;
            job.Error = null;
            job.ErrorInfo = null;
            job.DebugInfo = payload.DebugInfo;
            job.CompletedAt = DateTime.UtcNow;
            job.LastAttemptCompletedAt = DateTime.UtcNow;
            job.LastDurationMilliseconds = job.StartedAt.HasValue
                ? (DateTime.UtcNow - job.StartedAt.Value).TotalMilliseconds
                : null;
            job.StartedAt = null;
            job.LastError = null;
            if (!string.IsNullOrWhiteSpace(payload.PromptUsed))
            {
                job.Prompt = payload.PromptUsed;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("AI job {JobId} completed successfully", jobId);
        }

        public async Task<bool> MarkJobFailedAsync(int jobId, AiJobFailurePayload payload, CancellationToken cancellationToken = default)
        {
            var job = await _context.AiJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                _logger.LogWarning("Attempted to mark job {JobId} as failed but it was not found", jobId);
                return true;
            }

            job.Error = payload.Error.Message;
            job.LastAttemptCompletedAt = DateTime.UtcNow;
            job.LastDurationMilliseconds = job.StartedAt.HasValue
                ? (DateTime.UtcNow - job.StartedAt.Value).TotalMilliseconds
                : null;
            job.LastError = payload.Error.Message;
            job.StartedAt = null;
            job.StructuredResult = null;
            job.Result = null;
            job.ResultFormat = AiJobResultFormats.MarkdownSectionsV1;
            job.ErrorInfo = payload.Error;
            job.DebugInfo = null;
            if (!string.IsNullOrWhiteSpace(payload.PromptUsed))
            {
                job.Prompt = payload.PromptUsed;
            }

            var hasRemainingAttempts = payload.Error.IsRetryable && job.AttemptCount < _options.MaxAttempts;

            if (hasRemainingAttempts)
            {
                job.Status = AiJobStatus.Queued;
                job.CompletedAt = null;
            }
            else
            {
                job.Status = AiJobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.AttemptCount = Math.Max(job.AttemptCount, _options.MaxAttempts);
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (hasRemainingAttempts)
            {
                _logger.LogWarning("AI job {JobId} failed (attempt {Attempt}/{Max}) and will be retried. Reason: {Error}", jobId, job.AttemptCount, _options.MaxAttempts, payload.Error.Message);
            }
            else
            {
                _logger.LogError("AI job {JobId} failed after {Attempt} attempts: {Error}", jobId, job.AttemptCount, payload.Error.Message);
            }

            return !hasRemainingAttempts;
        }

        public async Task<AiJob?> RetryJobAsync(int jobId, CancellationToken cancellationToken = default)
        {
            var job = await _context.AiJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                _logger.LogWarning("Attempted to retry job {JobId} but it was not found", jobId);
                return null;
            }

            if (job.Status is AiJobStatus.Running or AiJobStatus.Queued)
            {
                _logger.LogWarning("Attempted to retry job {JobId} but it is currently {Status}", jobId, job.Status);
                return job;
            }

            job.Status = AiJobStatus.Queued;
            job.CompletedAt = null;
            job.StartedAt = null;
            job.Error = null;
            job.LastError = null;
            job.LastAttemptStartedAt = null;
            job.LastAttemptCompletedAt = null;
            job.LastDurationMilliseconds = null;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("AI job {JobId} has been re-queued for retry", jobId);

            return job;
        }
    }
}
