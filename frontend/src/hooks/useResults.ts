import { useMutation, useQuery } from '@apollo/client';
import {
  GET_RESULTS,
  GET_RESULTS_BY_INVESTIGATION,
  GET_RESULT_COUNT,
  GET_AVAILABLE_DATA_TYPES,
  GET_AVAILABLE_TOOLS,
  INGEST_RESULT,
  BULK_INGEST_RESULTS,
  DELETE_RESULT,
  UPDATE_RESULT_SUMMARY
} from '../graphql/resultQueries';
import { InvestigationResult } from './useInvestigations';

interface ResultEdge {
  node: InvestigationResult;
}

interface ResultsConnection {
  edges: ResultEdge[];
  pageInfo: {
    hasNextPage: boolean;
    hasPreviousPage: boolean;
  };
}

interface GetResultsData {
  results: ResultsConnection;
}

interface GetResultsVars {
  first?: number;
  skip?: number;
}

interface GetResultsByInvestigationData {
  resultsByInvestigation: InvestigationResult[];
}

interface GetResultCountData {
  resultCount: number;
}

interface AvailableDataTypesData {
  availableDataTypes: string[];
}

interface AvailableToolsData {
  availableTools: string[];
}

interface MutationResponse<T> {
  success: boolean;
  message?: string | null;
  error?: string | null;
  data: T;
}

interface IngestResultData {
  ingestResult: MutationResponse<InvestigationResult>;
}

interface IngestResultInput {
  investigationId: number;
  toolName: string;
  dataType: string;
  rawData: string;
  summary?: string | null;
  confidenceScore?: string | null;
}

interface BulkIngestResultInput {
  toolName: string;
  dataType: string;
  rawData: string;
  summary?: string | null;
  confidenceScore?: string | null;
}

interface BulkIngestResultsData {
  bulkIngestResults: {
    success: boolean;
    message?: string | null;
    error?: string | null;
    totalIngested: number;
    validationErrors?: string[] | null;
  };
}

interface DeleteResultData {
  deleteResult: {
    success: boolean;
    message?: string | null;
    error?: string | null;
    data: boolean;
  };
}

interface UpdateResultSummaryData {
  updateResultSummary: MutationResponse<{
    id: string;
    summary?: string | null;
    confidenceScore?: string | null;
  }>;
}

export const useResults = (first = 20, skip = 0) => {
  const { loading, error, data, refetch } = useQuery<GetResultsData, GetResultsVars>(GET_RESULTS, {
    variables: { first, skip }
  });

  const results = data?.results?.edges?.map((edge) => edge.node) ?? [];

  return {
    results,
    loading,
    error,
    hasNextPage: data?.results?.pageInfo?.hasNextPage ?? false,
    hasPreviousPage: data?.results?.pageInfo?.hasPreviousPage ?? false,
    refetch
  };
};

export const useResultsByInvestigation = (investigationId?: number) => {
  const { loading, error, data, refetch } = useQuery<
    GetResultsByInvestigationData,
    { investigationId: number }
  >(GET_RESULTS_BY_INVESTIGATION, {
    variables: { investigationId: investigationId as number },
    skip: !investigationId
  });

  return {
    results: data?.resultsByInvestigation ?? [],
    loading,
    error,
    refetch
  };
};

export const useResultCount = (investigationId?: number) => {
  const { loading, error, data } = useQuery<GetResultCountData, { investigationId: number }>(
    GET_RESULT_COUNT,
    {
      variables: { investigationId: investigationId as number },
      skip: !investigationId
    }
  );

  return {
    count: data?.resultCount ?? 0,
    loading,
    error
  };
};

export const useAvailableDataTypes = () => {
  const { loading, error, data } = useQuery<AvailableDataTypesData>(GET_AVAILABLE_DATA_TYPES);

  return {
    dataTypes: data?.availableDataTypes ?? [],
    loading,
    error
  };
};

export const useAvailableTools = () => {
  const { loading, error, data } = useQuery<AvailableToolsData>(GET_AVAILABLE_TOOLS);

  return {
    tools: data?.availableTools ?? [],
    loading,
    error
  };
};

export const useIngestResult = () => {
  const [ingestResultMutation, { loading, error }] = useMutation<
    IngestResultData,
    { input: IngestResultInput }
  >(INGEST_RESULT);

  const ingestResult = async (
    investigationId: number,
    toolName: string,
    dataType: string,
    rawData: string,
    summary: string | null = null,
    confidenceScore: string | null = null
  ) => {
    try {
      const { data } = await ingestResultMutation({
        variables: {
          input: {
            investigationId,
            toolName,
            dataType,
            rawData,
            summary,
            confidenceScore
          }
        }
      });

      if (data?.ingestResult.success) {
        return {
          success: true,
          data: data.ingestResult.data,
          message: data.ingestResult.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.ingestResult.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { ingestResult, loading, error };
};

export const useBulkIngestResults = () => {
  const [bulkIngestMutation, { loading, error }] = useMutation<
    BulkIngestResultsData,
    { input: { investigationId: number; results: BulkIngestResultInput[] } }
  >(BULK_INGEST_RESULTS);

  const bulkIngestResults = async (investigationId: number, results: BulkIngestResultInput[]) => {
    try {
      const { data } = await bulkIngestMutation({
        variables: {
          input: {
            investigationId,
            results
          }
        }
      });

      if (data?.bulkIngestResults.success) {
        return {
          success: true,
          totalIngested: data.bulkIngestResults.totalIngested,
          validationErrors: data.bulkIngestResults.validationErrors ?? undefined,
          message: data.bulkIngestResults.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.bulkIngestResults.error ?? 'Unknown error',
        totalIngested: 0
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err),
        totalIngested: 0
      } as const;
    }
  };

  return { bulkIngestResults, loading, error };
};

export const useDeleteResult = () => {
  const [deleteResultMutation, { loading, error }] = useMutation<
    DeleteResultData,
    { resultId: number }
  >(DELETE_RESULT);

  const deleteResult = async (resultId: number) => {
    try {
      const { data } = await deleteResultMutation({
        variables: { resultId }
      });

      if (data?.deleteResult.success) {
        return {
          success: true,
          message: data.deleteResult.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.deleteResult.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { deleteResult, loading, error };
};

export const useUpdateResultSummary = () => {
  const [updateMutation, { loading, error }] = useMutation<
    UpdateResultSummaryData,
    { resultId: number; summary?: string | null; confidenceScore?: string | null }
  >(UPDATE_RESULT_SUMMARY);

  const updateResultSummary = async (
    resultId: number,
    summary: string | null = null,
    confidenceScore: string | null = null
  ) => {
    try {
      const { data } = await updateMutation({
        variables: {
          resultId,
          summary,
          confidenceScore
        }
      });

      if (data?.updateResultSummary.success) {
        return {
          success: true,
          data: data.updateResultSummary.data,
          message: data.updateResultSummary.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.updateResultSummary.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { updateResultSummary, loading, error };
};
