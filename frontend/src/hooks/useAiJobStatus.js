import { useState, useEffect, useRef, useCallback } from 'react';
import { useMutation, useQuery, useLazyQuery } from '@apollo/client';
import {
  QUEUE_AI_JOB,
  GET_AI_JOB,
  GET_AI_JOBS_FOR_INVESTIGATION,
  CANCEL_AI_JOB,
  RETRY_AI_JOB
} from '../graphql/aiJobQueries';

/**
 * useQueueAiJob - Queue a new AI job
 * @param {boolean} debug - Enable debug mode to capture detailed metrics
 * @returns {Object} { queueJob, loading, error }
 */
export const useQueueAiJob = () => {
  const [queueMutation, { loading, error }] = useMutation(QUEUE_AI_JOB);

  const queueJob = useCallback(
    async (investigationId, jobType, model, promptOverride = null, debug = false) => {
      try {
        const { data } = await queueMutation({
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

        if (data?.queueAiJob.success) {
          return {
            success: true,
            jobId: data.queueAiJob.data.id,
            job: data.queueAiJob.data
          };
        } else {
          return {
            success: false,
            error: data?.queueAiJob.message || 'Failed to queue job'
          };
        }
      } catch (err) {
        console.error('Error queuing job:', err);
        return {
          success: false,
          error: err.message
        };
      }
    },
    [queueMutation]
  );

  return { queueJob, loading, error };
};

/**
 * useAiJobStatus - Poll job status with automatic stop on completion
 * @param {number} jobId - Job ID to poll
 * @param {number} pollIntervalMs - Polling interval (default 2000ms)
 * @returns {Object} { job, isPolling, loading, isTerminal, refetch }
 */
export const useAiJobStatus = (jobId, pollIntervalMs = 2000) => {
  const [isPolling, setIsPolling] = useState(false);
  const pollIntervalRef = useRef(null);

  // Terminal states where polling should stop
  const TERMINAL_STATUSES = ['Succeeded', 'Failed', 'Cancelled'];

  const { data, loading, refetch } = useQuery(GET_AI_JOB, {
    variables: { jobId },
    skip: !jobId,
    fetchPolicy: 'network-only'
  });

  const jobStatus = data?.aiJob?.status;
  const isTerminal = jobStatus && TERMINAL_STATUSES.includes(jobStatus);

  // Set up polling interval
  useEffect(() => {
    if (!jobId) {
      setIsPolling(false);
      return;
    }

    if (isTerminal) {
      setIsPolling(false);
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
      }
      return;
    }

    setIsPolling(true);

    pollIntervalRef.current = setInterval(() => {
      refetch();
    }, pollIntervalMs);

    return () => {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
        pollIntervalRef.current = null;
      }
    };
  }, [jobId, pollIntervalMs, refetch, isTerminal]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
      }
    };
  }, []);

  return {
    job: data?.aiJob || null,
    isPolling,
    loading,
    isTerminal: !!isTerminal,
    refetch
  };
};

/**
 * useAiJobHistory - Fetch job history for an investigation
 * @param {number} investigationId - Investigation ID
 * @param {number} take - Number of recent jobs to fetch (default 10)
 * @returns {Object} { jobs, loading, error, refetch }
 */
export const useAiJobHistory = (investigationId, take = 10) => {
  const { data, loading, error, refetch } = useQuery(GET_AI_JOBS_FOR_INVESTIGATION, {
    variables: { investigationId, take },
    skip: !investigationId,
    fetchPolicy: 'cache-and-network'
  });

  return {
    jobs: data?.aiJobsForInvestigation || [],
    loading,
    error,
    refetch
  };
};

/**
 * useCancelAiJob - Cancel a queued or running job
 * @returns {Object} { cancelJob, loading, error }
 */
export const useCancelAiJob = () => {
  const [cancelMutation, { loading, error }] = useMutation(CANCEL_AI_JOB);

  const cancelJob = useCallback(
    async (jobId) => {
      try {
        const { data } = await cancelMutation({
          variables: { jobId }
        });

        return {
          success: data?.cancelAiJob.success || false,
          message: data?.cancelAiJob.message
        };
      } catch (err) {
        console.error('Error canceling job:', err);
        return {
          success: false,
          message: err.message
        };
      }
    },
    [cancelMutation]
  );

  return { cancelJob, loading, error };
};

/**
 * useRetryAiJob - Retry a failed job
 * @returns {Object} { retryJob, loading, error }
 */
export const useRetryAiJob = () => {
  const [retryMutation, { loading, error }] = useMutation(RETRY_AI_JOB);

  const retryJob = useCallback(
    async (jobId) => {
      try {
        const { data } = await retryMutation({
          variables: { jobId }
        });

        if (data?.retryAiJob.success) {
          return {
            success: true,
            jobId: data.retryAiJob.data.id,
            job: data.retryAiJob.data
          };
        } else {
          return {
            success: false,
            error: data?.retryAiJob.message || 'Failed to retry job'
          };
        }
      } catch (err) {
        console.error('Error retrying job:', err);
        return {
          success: false,
          error: err.message
        };
      }
    },
    [retryMutation]
  );

  return { retryJob, loading, error };
};
