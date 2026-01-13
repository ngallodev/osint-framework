import { gql } from '@apollo/client';

export const GET_INVESTIGATIONS = gql`
  query GetInvestigations($first: Int, $skip: Int) {
    investigations(first: $first, skip: $skip) {
      edges {
        node {
          id
          target
          investigationType
          status
          requestedAt
          requestedBy
          results {
            id
          }
        }
      }
      pageInfo {
        hasNextPage
        hasPreviousPage
      }
    }
  }
`;

export const GET_INVESTIGATION = gql`
  query GetInvestigationById($id: Int!) {
    investigationById(id: $id) {
      id
      target
      investigationType
      status
      requestedAt
      requestedBy
      results {
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

export const CREATE_INVESTIGATION = gql`
  mutation CreateInvestigation($input: CreateInvestigationInput!) {
    createInvestigation(input: $input) {
      success
      message
      error
      data {
        id
        target
        investigationType
        status
        requestedAt
        requestedBy
      }
    }
  }
`;

export const UPDATE_INVESTIGATION = gql`
  mutation UpdateInvestigation($input: UpdateInvestigationInput!) {
    updateInvestigation(input: $input) {
      success
      message
      error
      data {
        id
        target
        investigationType
        status
        requestedAt
        requestedBy
      }
    }
  }
`;

export const UPDATE_INVESTIGATION_STATUS = gql`
  mutation UpdateInvestigationStatus($investigationId: Int!, $status: InvestigationStatus!) {
    updateInvestigationStatus(investigationId: $investigationId, status: $status) {
      success
      message
      error
      data {
        id
        status
      }
    }
  }
`;

export const DELETE_INVESTIGATION = gql`
  mutation DeleteInvestigation($investigationId: Int!) {
    deleteInvestigation(investigationId: $investigationId) {
      success
      message
      error
      data
    }
  }
`;
