using System.Linq;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using OsintBackend.Data;
using OsintBackend.Models;

namespace OsintBackend.GraphQL.Queries
{
    [Authorize]
    [ExtendObjectType(OperationTypeNames.Query)]
    public class InvestigationQueries
    {
        [UsePaging]
        [UseFiltering]
        [UseSorting]
        public IQueryable<OsintInvestigation> GetInvestigations([Service] OsintDbContext context)
            => context.Investigations.AsNoTracking();

        public Task<OsintInvestigation?> GetInvestigationByIdAsync(
            int id,
            [Service] OsintDbContext context)
        {
            return context.Investigations
                .Include(i => i.Results)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);
        }
    }
}
