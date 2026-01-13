import { gql } from '@apollo/client';

/**
 * Queue a new AI job (analysis or inference)
 * Returns immediately with job ID - no blocking
 */
export const QUEUE_AI_JOB = gql`
  mutation QueueAiJob($input: QueueAiJobInput!) {
    queueAiJob(input: $input) {
      success
      message
      data {
        id
        investigationId
        jobType
        status
        model
        createdAt
      }
    }
  }
`;

/**
 * Get status and result of a single job
 * Used for polling
 */
export const GET_AI_JOB = gql`
  query GetAiJob($jobId: Int!) {
    aiJob(jobId: $jobId) {
      id
      investigationId
      jobType
      status
      model
      result
      error
      createdAt
      startedAt
      completedAt
      attemptCount
      debug
      debugInfo {
        promptText
        promptLength
        requestSentAt
        responseReceivedAt
        ollamaMetrics {
          model
          totalDurationNs
          loadDurationNs
          promptEvalCount
          promptEvalDurationNs
          evalCount
          evalDurationNs
          promptTokensPerSecond
          responseTokensPerSecond
          doneReason
        }
        httpMetrics {
          requestDurationMs
          statusCode
          requestBodySize
          responseBodySize
          endpointUrl
          retryAttempts
        }
      }
    }
  }
`;

/**
 * Get recent jobs for an investigation
 * Used to display job history
 */
export const GET_AI_JOBS_FOR_INVESTIGATION = gql`
  query GetAiJobsForInvestigation($investigationId: Int!, $take: Int) {
    aiJobsForInvestigation(investigationId: $investigationId, take: $take) {
      id
      jobType
      status
      model
      result
      error
      createdAt
      startedAt
      completedAt
      attemptCount
      debug
    }
  }
`;

/**
 * Cancel a queued or running job
 */
export const CANCEL_AI_JOB = gql`
  mutation CancelAiJob($jobId: Int!) {
    cancelAiJob(jobId: $jobId) {
      success
      message
    }
  }
`;

/**
 * Retry a failed job
 */
export const RETRY_AI_JOB = gql`
  mutation RetryAiJob($jobId: Int!) {
    retryAiJob(jobId: $jobId) {
      success
      message
      data {
        id
        status
        createdAt
      }
    }
  }
`;
