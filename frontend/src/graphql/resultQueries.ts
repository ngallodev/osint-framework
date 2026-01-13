import { gql } from '@apollo/client';

export const GET_RESULTS = gql`
  query GetResults($first: Int, $skip: Int) {
    results(first: $first, skip: $skip) {
      edges {
        node {
          id
          toolName
          dataType
          summary
          rawData
          collectedAt
          confidenceScore
          osintInvestigationId
        }
      }
      pageInfo {
        hasNextPage
        hasPreviousPage
      }
    }
  }
`;

export const GET_RESULTS_BY_INVESTIGATION = gql`
  query GetResultsByInvestigation($investigationId: Int!) {
    resultsByInvestigation: resultsByInvestigationId(investigationId: $investigationId) {
      id
      toolName
      dataType
      summary
      rawData
      collectedAt
      confidenceScore
      osintInvestigationId
    }
  }
`;

export const GET_RESULT_COUNT = gql`
  query GetResultCount($investigationId: Int!) {
    resultCount: getResultCount(investigationId: $investigationId)
  }
`;

export const GET_AVAILABLE_DATA_TYPES = gql`
  query GetAvailableDataTypes {
    availableDataTypes: getAvailableDataTypes
  }
`;

export const GET_AVAILABLE_TOOLS = gql`
  query GetAvailableTools {
    availableTools: getAvailableTools
  }
`;

export const INGEST_RESULT = gql`
  mutation IngestResult($input: IngestResultInput!) {
    ingestResult(input: $input) {
      success
      message
      error
      data {
        id
        toolName
        dataType
        summary
        rawData
        collectedAt
        confidenceScore
      }
    }
  }
`;

export const BULK_INGEST_RESULTS = gql`
  mutation BulkIngestResults($input: BulkIngestResultInput!) {
    bulkIngestResults(input: $input) {
      success
      message
      error
      totalIngested
      validationErrors
    }
  }
`;

export const DELETE_RESULT = gql`
  mutation DeleteResult($resultId: Int!) {
    deleteResult(resultId: $resultId) {
      success
      message
      error
      data
    }
  }
`;

export const UPDATE_RESULT_SUMMARY = gql`
  mutation UpdateResultSummary($resultId: Int!, $summary: String, $confidenceScore: String) {
    updateResultSummary(resultId: $resultId, summary: $summary, confidenceScore: $confidenceScore) {
      success
      message
      error
      data {
        id
        summary
        confidenceScore
      }
    }
  }
`;
