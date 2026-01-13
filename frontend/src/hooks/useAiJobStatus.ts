import { useEffect, useState, useRef } from 'react';
import { useQuery, useMutation } from '@apollo/client';
import {
  GET_AI_JOB,
  GET_AI_JOBS_FOR_INVESTIGATION,
  QUEUE_AI_JOB,
  CANCEL_AI_JOB,
  RETRY_AI_JOB
} from '../graphql/aiJobQueries';

export type AiJobStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';

export interface AiJobStructuredResultSection {
  key: string;
  heading: string;
  content: string;
}

export interface AiJobStructuredResult {
  formatVersion: string;
  sections: AiJobStructuredResultSection[];
  metadata?: Record<string, string> | null;
}

export interface AiJobErrorInfo {
  message: string;
  code?: string | null;
  details?: string | null;
  isRetryable: boolean;
  occurredAt: string;
  metadata?: Record<string, string> | null;
}

export interface AiJobOllamaMetrics {
  model?: string | null;
  totalDurationNs?: number | null;
  loadDurationNs?: number | null;
  promptEvalCount?: number | null;
  promptEvalDurationNs?: number | null;
  evalCount?: number | null;
  evalDurationNs?: number | null;
  promptTokensPerSecond?: number | null;
  responseTokensPerSecond?: number | null;
  doneReason?: string | null;
}

export interface AiJobHttpMetrics {
  requestDurationMs?: number | null;
  statusCode?: number | null;
  requestBodySize?: number | null;
  responseBodySize?: number | null;
  endpointUrl?: string | null;
  retryAttempts?: number | null;
}

export interface AiJobDebugInfo {
  promptText?: string | null;
  promptLength?: number | null;
  requestSentAt?: string | null;
  responseReceivedAt?: string | null;
  ollamaMetrics?: AiJobOllamaMetrics | null;
  httpMetrics?: AiJobHttpMetrics | null;
}

export interface AiJob {
  id: number;
  osintInvestigationId: number;
  jobType: string;
  status: AiJobStatus;
  model?: string | null;
  prompt?: string | null;
  result?: string | null;
  resultFormat?: string | null;
  structuredResult?: AiJobStructuredResult | null;
  error?: string | null;
  errorInfo?: AiJobErrorInfo | null;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  attemptCount?: number;
  debug?: boolean;
  debugInfo?: AiJobDebugInfo | null;
}

interface QueueAiJobData {
  queueAiJob: {
    success: boolean;
    message?: string | null;
    error?: string | null;
    data: AiJob;
  };
}

interface QueueAiJobVariables {
  input: {
    investigationId: number;
    jobType: string;
    model?: string | null;
    promptOverride?: string | null;
    debug?: boolean;
  };
}

interface GetAiJobData {
  aiJob: AiJob;
}

interface GetAiJobVars {
  jobId: number;
}

interface GetAiJobsForInvestigationData {
  aiJobsForInvestigation: AiJob[];
}

interface GetAiJobsForInvestigationVars {
  investigationId: number;
  take?: number;
}

interface CancelAiJobData {
  cancelAiJob: {
    success: boolean;
    message?: string | null;
    error?: string | null;
    data: boolean;
  };
}

interface CancelAiJobVars {
  jobId: number;
}

interface RetryAiJobData {
  retryAiJob: {
    success: boolean;
    message?: string | null;
    error?: string | null;
    data: AiJob;
  };
}

interface RetryAiJobVars {
  jobId: number;
}

export const useQueueAiJob = () => {
  const [queueAiJobMutation, { loading, error }] = useMutation<
    QueueAiJobData,
    QueueAiJobVariables
  >(QUEUE_AI_JOB);

  const queueJob = async (
    investigationId: number,
    jobType: string,
    model?: string | null,
    promptOverride: string | null = null,
    debug = false
  ) => {
    try {
      const { data } = await queueAiJobMutation({
        variables: {
          input: {
            investigationId,
            jobType,
            model,
            promptOverride,
            debug
          }
        }
      });

      if (data?.queueAiJob.success && data.queueAiJob.data) {
        return {
          success: true,
          jobId: data.queueAiJob.data.id,
          job: data.queueAiJob.data,
          message: data.queueAiJob.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.queueAiJob.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { queueJob, loading, error };
};

const TERMINAL_STATUSES: AiJobStatus[] = ['Succeeded', 'Failed', 'Cancelled'];

export const useAiJobStatus = (jobId?: number, pollIntervalMs = 2000) => {
  const [jobStatus, setJobStatus] = useState<AiJob | null>(null);
  const [isPolling, setIsPolling] = useState<boolean>(false);
  const pollIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const { data, loading: queryLoading, refetch } = useQuery<GetAiJobData, GetAiJobVars>(
    GET_AI_JOB,
    {
      variables: { jobId: jobId as number },
      skip: !jobId,
      pollInterval: 0
    }
  );

  useEffect(() => {
    if (!jobId) {
      setIsPolling(false);
      return;
    }

    setIsPolling(true);

    const startPolling = async () => {
      try {
        const { data: result } = await refetch();
        if (result?.aiJob) {
          setJobStatus(result.aiJob);

          if (TERMINAL_STATUSES.includes(result.aiJob.status)) {
            setIsPolling(false);
            if (pollIntervalRef.current) {
              clearInterval(pollIntervalRef.current);
              pollIntervalRef.current = null;
            }
          }
        }
      } catch (err) {
        console.error('Error polling job status:', err);
      }
    };

    startPolling();
    pollIntervalRef.current = setInterval(startPolling, pollIntervalMs);

    return () => {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
        pollIntervalRef.current = null;
      }
    };
  }, [jobId, pollIntervalMs, refetch]);

  const job = jobStatus ?? data?.aiJob ?? null;
  const isTerminal = job ? TERMINAL_STATUSES.includes(job.status) : false;

  return {
    job,
    isPolling,
    loading: queryLoading,
    isTerminal
  };
};

export const useAiJobHistory = (investigationId?: number, take = 10) => {
  const { loading, error, data, refetch } = useQuery<
    GetAiJobsForInvestigationData,
    GetAiJobsForInvestigationVars
  >(GET_AI_JOBS_FOR_INVESTIGATION, {
    variables: { investigationId: investigationId as number, take },
    skip: !investigationId
  });

  return {
    jobs: data?.aiJobsForInvestigation ?? [],
    loading,
    error,
    refetch
  };
};

export const useCancelAiJob = () => {
  const [cancelAiJobMutation, { loading, error }] = useMutation<
    CancelAiJobData,
    CancelAiJobVars
  >(CANCEL_AI_JOB);

  const cancelJob = async (jobId: number) => {
    try {
      const { data } = await cancelAiJobMutation({
        variables: { jobId }
      });

      if (data?.cancelAiJob.success) {
        return {
          success: true,
          data: data.cancelAiJob.data,
          message: data.cancelAiJob.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.cancelAiJob.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { cancelJob, loading, error };
};

export const useRetryAiJob = () => {
  const [retryAiJobMutation, { loading, error }] = useMutation<
    RetryAiJobData,
    RetryAiJobVars
  >(RETRY_AI_JOB);

  const retryJob = async (jobId: number) => {
    try {
      const { data } = await retryAiJobMutation({
        variables: { jobId }
      });

      if (data?.retryAiJob.success && data.retryAiJob.data) {
        return {
          success: true,
          jobId: data.retryAiJob.data.id,
          job: data.retryAiJob.data,
          message: data.retryAiJob.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.retryAiJob.error ?? data?.retryAiJob.message ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { retryJob, loading, error };
};
