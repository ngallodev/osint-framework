import { useMutation, useQuery } from '@apollo/client';
import {
  GET_INVESTIGATIONS,
  GET_INVESTIGATION,
  CREATE_INVESTIGATION,
  UPDATE_INVESTIGATION,
  UPDATE_INVESTIGATION_STATUS,
  DELETE_INVESTIGATION
} from '../graphql/investigationQueries';

export interface InvestigationResult {
  id: number;
  toolName: string;
  dataType: string;
  summary?: string | null;
  rawData: string;
  collectedAt: string;
  confidenceScore?: string | null;
}

export interface Investigation {
  id: number;
  target: string;
  investigationType: string;
  status: string;
  requestedAt: string;
  requestedBy?: string | null;
  results?: InvestigationResult[];
}

interface InvestigationEdge {
  node: Investigation;
}

interface PageInfo {
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

interface GetInvestigationsData {
  investigations: {
    edges: InvestigationEdge[];
    pageInfo: PageInfo;
  };
}

interface GetInvestigationsVars {
  first?: number;
  skip?: number;
}

interface GetInvestigationData {
  investigationById: InvestigationDetail;
}

interface InvestigationDetail extends Investigation {
  results: InvestigationResult[];
}

interface MutationResponse<T> {
  success: boolean;
  message?: string | null;
  error?: string | null;
  data: T;
}

interface CreateInvestigationData {
  createInvestigation: MutationResponse<Investigation>;
}

interface UpdateInvestigationData {
  updateInvestigation: MutationResponse<Investigation>;
}

interface UpdateInvestigationStatusData {
  updateInvestigationStatus: MutationResponse<{ id: number; status: string }>;
}

interface DeleteInvestigationData {
  deleteInvestigation: MutationResponse<boolean>;
}

interface CreateInvestigationInput {
  target: string;
  investigationType: string;
  requestedBy?: string | null;
}

interface UpdateInvestigationInput {
  id: number;
  target?: string;
  investigationType?: string;
  requestedBy?: string | null;
}

export const useInvestigations = (first = 10, skip = 0) => {
  const { loading, error, data, refetch } = useQuery<
    GetInvestigationsData,
    GetInvestigationsVars
  >(GET_INVESTIGATIONS, {
    variables: { first, skip }
  });

  const investigations =
    data?.investigations?.edges?.map((edge) => edge.node) ?? [];

  return {
    investigations,
    loading,
    error,
    hasNextPage: data?.investigations?.pageInfo?.hasNextPage ?? false,
    hasPreviousPage: data?.investigations?.pageInfo?.hasPreviousPage ?? false,
    refetch
  };
};

export const useInvestigation = (id?: number) => {
  const { loading, error, data, refetch } = useQuery<
    GetInvestigationData,
    { id: number }
  >(GET_INVESTIGATION, {
    variables: { id: id as number },
    skip: !id
  });

  return {
    investigation: data?.investigationById,
    loading,
    error,
    refetch
  };
};

export const useCreateInvestigation = () => {
  const [createInvestigationMutation, { loading, error }] = useMutation<
    CreateInvestigationData,
    { input: CreateInvestigationInput }
  >(CREATE_INVESTIGATION);

  const createInvestigation = async (
    target: string,
    investigationType: string,
    requestedBy?: string | null
  ) => {
    try {
      const { data: response } = await createInvestigationMutation({
        variables: {
          input: {
            target,
            investigationType,
            requestedBy
          }
        }
      });

      if (response?.createInvestigation.success) {
        return {
          success: true,
          data: response.createInvestigation.data,
          message: response.createInvestigation.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: response?.createInvestigation.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { createInvestigation, loading, error };
};

export const useUpdateInvestigation = () => {
  const [updateInvestigationMutation, { loading, error }] = useMutation<
    UpdateInvestigationData,
    { input: UpdateInvestigationInput }
  >(UPDATE_INVESTIGATION);

  const updateInvestigation = async (
    id: number,
    updates: Omit<UpdateInvestigationInput, 'id'>
  ) => {
    try {
      const { data } = await updateInvestigationMutation({
        variables: {
          input: {
            id,
            ...updates
          }
        }
      });

      if (data?.updateInvestigation.success) {
        return {
          success: true,
          data: data.updateInvestigation.data,
          message: data.updateInvestigation.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.updateInvestigation.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { updateInvestigation, loading, error };
};

export const useUpdateInvestigationStatus = () => {
  const [updateStatusMutation, { loading, error }] = useMutation<
    UpdateInvestigationStatusData,
    { investigationId: number; status: string }
  >(UPDATE_INVESTIGATION_STATUS);

  const updateStatus = async (investigationId: number, status: string) => {
    try {
      const { data } = await updateStatusMutation({
        variables: {
          investigationId,
          status
        }
      });

      if (data?.updateInvestigationStatus.success) {
        return {
          success: true,
          data: data.updateInvestigationStatus.data,
          message: data.updateInvestigationStatus.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.updateInvestigationStatus.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { updateStatus, loading, error };
};

export const useDeleteInvestigation = () => {
  const [deleteInvestigationMutation, { loading, error }] = useMutation<
    DeleteInvestigationData,
    { investigationId: number }
  >(DELETE_INVESTIGATION);

  const deleteInvestigation = async (investigationId: number) => {
    try {
      const { data } = await deleteInvestigationMutation({
        variables: { investigationId }
      });

      if (data?.deleteInvestigation.success) {
        return {
          success: true,
          message: data.deleteInvestigation.message ?? undefined
        } as const;
      }

      return {
        success: false,
        error: data?.deleteInvestigation.error ?? 'Unknown error'
      } as const;
    } catch (err) {
      return {
        success: false,
        error: err instanceof Error ? err.message : String(err)
      } as const;
    }
  };

  return { deleteInvestigation, loading, error };
};
