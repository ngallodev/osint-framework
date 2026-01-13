using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsintBackend.Data;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    public class AiJobBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AiJobBackgroundService> _logger;
        private readonly IOptionsMonitor<AiJobQueueOptions> _optionsMonitor;
        private readonly TimeSpan _idleDelay = TimeSpan.FromSeconds(3);

        public AiJobBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<AiJobBackgroundService> logger,
            IOptionsMonitor<AiJobQueueOptions> optionsMonitor)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AI job background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var queue = scope.ServiceProvider.GetRequiredService<IAiJobQueueService>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<OsintDbContext>();
                    var ollama = scope.ServiceProvider.GetRequiredService<IOllamaService>();

                    var job = await queue.TryClaimNextJobAsync(stoppingToken);
                    if (job is null)
                    {
                        await Task.Delay(_idleDelay, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Processing AI job {JobId} ({JobType}) [Debug: {Debug}]", job.Id, job.JobType, job.Debug);

                    string? promptUsed = job.Prompt;

                    try
                    {
                        var modelToUse = job.Model;
                        List<OsintResult>? investigationResults = null;

                        if (string.IsNullOrWhiteSpace(promptUsed))
                        {
                            investigationResults = await dbContext.Results
                                .Where(r => r.OsintInvestigationId == job.OsintInvestigationId)
                                .OrderBy(r => r.CollectedAt)
                                .ToListAsync(stoppingToken);

                            if (job.JobType == AiJobTypes.Inference)
                            {
                                promptUsed = AiPromptBuilder.BuildInferencePrompt(investigationResults);
                                modelToUse ??= "llama2:13b";
                            }
                            else
                            {
                                promptUsed = AiPromptBuilder.BuildAnalysisPrompt(investigationResults, job.JobType);
                            }
                        }

                        if (string.IsNullOrWhiteSpace(promptUsed))
                        {
                            throw new InvalidOperationException($"AI job {job.Id} did not produce a prompt.");
                        }

                        var requestStartedAt = DateTime.UtcNow;
                        var completion = await ollama.GenerateAnalysisWithMetricsAsync(promptUsed, modelToUse);
                        var requestCompletedAt = DateTime.UtcNow;

                        var rawResponse = completion.Response.Response;
                        var structuredResult = AiResponseParser.Parse(job.JobType, rawResponse);

                        AiJobDebugInfo? debugInfo = null;
                        if (job.Debug)
                        {
                            debugInfo = BuildDebugInfo(completion, promptUsed, requestStartedAt, requestCompletedAt);
                        }

                        await queue.MarkJobSucceededAsync(
                            job.Id,
                            new AiJobCompletionPayload
                            {
                                RawResult = rawResponse,
                                StructuredResult = structuredResult,
                                ResultFormat = structuredResult?.FormatVersion ?? AiJobResultFormats.MarkdownSectionsV1,
                                PromptUsed = promptUsed,
                                DebugInfo = debugInfo
                            },
                            stoppingToken);

                        await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "AI job {JobId} failed", job.Id);

                        var errorInfo = AiJobErrorClassifier.FromException(ex);
                        var terminal = await queue.MarkJobFailedAsync(
                            job.Id,
                            new AiJobFailurePayload
                            {
                                Error = errorInfo,
                                PromptUsed = promptUsed
                            },
                            stoppingToken);

                        if (!terminal)
                        {
                            var backoffSeconds = Math.Max(0, _optionsMonitor.CurrentValue.RetryBackoffSeconds);
                            if (backoffSeconds > 0)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), stoppingToken);
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Service is shutting down
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in AI job background service");
                    await Task.Delay(_idleDelay, stoppingToken);
                }
            }

            _logger.LogInformation("AI job background service stopping");
        }

        private static AiJobDebugInfo BuildDebugInfo(OllamaCompletionResult completion, string prompt, DateTime startedAt, DateTime completedAt)
        {
            static double? TokensPerSecond(int? tokens, long? durationNs)
            {
                if (!tokens.HasValue || !durationNs.HasValue || durationNs.Value <= 0)
                {
                    return null;
                }

                var seconds = durationNs.Value / 1_000_000_000.0;
                return seconds > 0 ? tokens.Value / seconds : null;
            }

            var response = completion.Response;

            return new AiJobDebugInfo
            {
                PromptText = prompt,
                PromptLength = prompt.Length,
                RequestSentAt = startedAt,
                ResponseReceivedAt = completedAt,
                OllamaMetrics = new OllamaDebugMetrics
                {
                    Model = response.Model,
                    TotalDurationNs = response.TotalDuration,
                    LoadDurationNs = response.LoadDuration,
                    PromptEvalCount = response.PromptEvalCount,
                    PromptEvalDurationNs = response.PromptEvalDuration,
                    EvalCount = response.EvalCount,
                    EvalDurationNs = response.EvalDuration,
                    DoneReason = response.DoneReason,
                    PromptTokensPerSecond = TokensPerSecond(response.PromptEvalCount, response.PromptEvalDuration),
                    ResponseTokensPerSecond = TokensPerSecond(response.EvalCount, response.EvalDuration)
                },
                HttpMetrics = new HttpDebugMetrics
                {
                    RequestDurationMs = (completedAt - startedAt).TotalMilliseconds,
                    StatusCode = (int)completion.StatusCode,
                    RequestBodySize = completion.RequestBodyByteCount,
                    ResponseBodySize = completion.ResponseBodyByteCount,
                    EndpointUrl = completion.RequestedUri?.ToString(),
                    RetryAttempts = completion.AttemptCount
                }
            };
        }
    }
}
