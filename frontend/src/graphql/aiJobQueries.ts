import { gql } from '@apollo/client';

export const QUEUE_AI_JOB = gql`
  mutation QueueAiJob($input: QueueAiJobInput!) {
    queueAiJob(input: $input) {
      success
      message
      error
      data {
        id
        osintInvestigationId
        jobType
        status
        model
        createdAt
        debug
      }
    }
  }
`;

export const GET_AI_JOB = gql`
  query GetAiJob($jobId: Int!) {
    aiJob(jobId: $jobId) {
      id
      osintInvestigationId
      jobType
      status
      model
      result
      resultFormat
      structuredResult {
        formatVersion
        sections {
          key
          heading
          content
        }
        metadata
      }
      error
      errorInfo {
        message
        code
        details
        isRetryable
        occurredAt
        metadata
      }
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
      createdAt
      startedAt
      completedAt
    }
  }
`;

export const GET_AI_JOBS_FOR_INVESTIGATION = gql`
  query GetAiJobsForInvestigation($investigationId: Int!, $take: Int) {
    aiJobsForInvestigation(investigationId: $investigationId, take: $take) {
      id
      osintInvestigationId
      jobType
      status
      model
      result
      error
      attemptCount
      debug
      createdAt
      startedAt
      completedAt
      resultFormat
    }
  }
`;

export const CANCEL_AI_JOB = gql`
  mutation CancelAiJob($jobId: Int!) {
    cancelAiJob(jobId: $jobId) {
      success
      message
      error
      data
    }
  }
`;

export const RETRY_AI_JOB = gql`
  mutation RetryAiJob($jobId: Int!) {
    retryAiJob(jobId: $jobId) {
      success
      message
      error
      data {
        id
        osintInvestigationId
        jobType
        status
        model
        createdAt
      }
    }
  }
`;
