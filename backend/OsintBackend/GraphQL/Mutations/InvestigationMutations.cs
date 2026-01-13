using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using OsintBackend.Data;
using OsintBackend.GraphQL.InputTypes;
using OsintBackend.GraphQL.Types;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Mutations
{
    /// <summary>
    /// Mutations for creating, updating, and deleting investigations.
    /// </summary>
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class InvestigationMutations
    {
        public async Task<MutationResponse<OsintInvestigation>> CreateInvestigation(
            CreateInvestigationInput input,
            [Service] OsintDbContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input.Target))
                {
                    return new MutationResponse<OsintInvestigation>
                    {
                        Success = false,
                        Error = "Target is required."
                    };
                }

                if (string.IsNullOrWhiteSpace(input.InvestigationType))
                {
                    return new MutationResponse<OsintInvestigation>
                    {
                        Success = false,
                        Error = "InvestigationType is required."
                    };
                }

                var investigation = new OsintInvestigation
                {
                    Target = input.Target,
                    InvestigationType = input.InvestigationType,
                    RequestedBy = input.RequestedBy,
                    RequestedAt = DateTime.UtcNow,
                    Status = InvestigationStatus.Pending
                };

                context.Investigations.Add(investigation);
                await context.SaveChangesAsync();

                return new MutationResponse<OsintInvestigation>
                {
                    Success = true,
                    Data = investigation,
                    Message = $"Investigation created with ID {investigation.Id}."
                };
            }
            catch (Exception ex)
            {
                return new MutationResponse<OsintInvestigation>
                {
                    Success = false,
                    Error = $"Failed to create investigation: {ex.Message}"
                };
            }
        }

        public async Task<MutationResponse<OsintInvestigation>> UpdateInvestigation(
            UpdateInvestigationInput input,
            [Service] OsintDbContext context)
        {
            try
            {
                var investigation = await context.Investigations
                    .FirstOrDefaultAsync(i => i.Id == input.Id);

                if (investigation == null)
                {
                    return new MutationResponse<OsintInvestigation>
                    {
                        Success = false,
                        Error = $"Investigation with ID {input.Id} not found."
                    };
                }

                if (!string.IsNullOrWhiteSpace(input.Target))
                {
                    investigation.Target = input.Target;
                }

                if (!string.IsNullOrWhiteSpace(input.InvestigationType))
                {
                    investigation.InvestigationType = input.InvestigationType;
                }

                if (input.Status.HasValue)
                {
                    investigation.Status = input.Status.Value;
                }

                if (!string.IsNullOrWhiteSpace(input.RequestedBy))
                {
                    investigation.RequestedBy = input.RequestedBy;
                }

                await context.SaveChangesAsync();

                return new MutationResponse<OsintInvestigation>
                {
                    Success = true,
                    Data = investigation,
                    Message = $"Investigation {investigation.Id} updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new MutationResponse<OsintInvestigation>
                {
                    Success = false,
                    Error = $"Failed to update investigation: {ex.Message}"
                };
            }
        }

        public async Task<MutationResponse<bool>> DeleteInvestigation(
            int id,
            [Service] OsintDbContext context)
        {
            try
            {
                var investigation = await context.Investigations
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (investigation == null)
                {
                    return new MutationResponse<bool>
                    {
                        Success = false,
                        Error = $"Investigation with ID {id} not found.",
                        Data = false
                    };
                }

                context.Investigations.Remove(investigation);
                await context.SaveChangesAsync();

                return new MutationResponse<bool>
                {
                    Success = true,
                    Data = true,
                    Message = $"Investigation {id} deleted successfully."
                };
            }
            catch (Exception ex)
            {
                return new MutationResponse<bool>
                {
                    Success = false,
                    Error = $"Failed to delete investigation: {ex.Message}",
                    Data = false
                };
            }
        }
    }
}
