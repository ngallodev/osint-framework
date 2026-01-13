using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using OsintBackend.Data;
using OsintBackend.GraphQL.InputTypes;
using OsintBackend.GraphQL.Types;
using OsintBackend.Models;
using OsintBackend.Services;

namespace OsintBackend.GraphQL.Mutations
{
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class AiJobMutations
    {
        public async Task<MutationResponse<AiJob>> QueueAiJob(
            QueueAiJobInput input,
            [Service] IAiJobQueueService queueService,
            CancellationToken cancellationToken)
        {
            try
            {
                if (input.InvestigationId <= 0)
                {
                    return new MutationResponse<AiJob>
                    {
                        Success = false,
                        Error = "InvestigationId is required"
                    };
                }

                var jobType = string.IsNullOrWhiteSpace(input.JobType) ? AiJobTypes.Analysis : input.JobType.ToLowerInvariant();
                if (jobType != AiJobTypes.Analysis && jobType != AiJobTypes.Inference)
                {
                    return new MutationResponse<AiJob>
                    {
                        Success = false,
                        Error = "Unsupported AI job type"
                    };
                }

                var job = await queueService.EnqueueAsync(
                    input.InvestigationId,
                    jobType,
                    input.Model,
                    input.PromptOverride,
                    input.Debug,
                    cancellationToken);

                return new MutationResponse<AiJob>
                {
                    Success = true,
                    Data = job,
                    Message = $"AI job {job.Id} queued"
                };
            }
            catch (Exception ex)
            {
                return new MutationResponse<AiJob>
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<MutationResponse<bool>> CancelAiJob(
            int jobId,
            [Service] OsintDbContext context,
            CancellationToken cancellationToken)
        {
            var job = await context.AiJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                return new MutationResponse<bool>
                {
                    Success = false,
                    Error = "Job not found",
                    Data = false
                };
            }

            if (job.Status is AiJobStatus.Succeeded or AiJobStatus.Failed)
            {
                return new MutationResponse<bool>
                {
                    Success = false,
                    Error = "Job already completed",
                    Data = false
                };
            }

            job.Status = AiJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return new MutationResponse<bool>
            {
                Success = true,
                Data = true,
                Message = "Job cancelled"
            };
        }

        public async Task<MutationResponse<AiJob>> RetryAiJob(
            int jobId,
            [Service] OsintDbContext context,
            [Service] IAiJobQueueService queueService,
            CancellationToken cancellationToken)
        {
            var job = await context.AiJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
            {
                return new MutationResponse<AiJob>
                {
                    Success = false,
                    Error = "Job not found"
                };
            }

            if (job.Status != AiJobStatus.Failed && job.Status != AiJobStatus.Cancelled)
            {
                return new MutationResponse<AiJob>
                {
                    Success = false,
                    Error = $"Cannot retry job with status {job.Status}"
                };
            }

            var retried = await queueService.RetryJobAsync(jobId, cancellationToken);
            if (retried is null)
            {
                return new MutationResponse<AiJob>
                {
                    Success = false,
                    Error = "Job could not be re-queued"
                };
            }

            return new MutationResponse<AiJob>
            {
                Success = true,
                Data = retried,
                Message = $"Job {retried.Id} re-queued for retry"
            };
        }
    }
}
