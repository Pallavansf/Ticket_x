using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class DatabaseAdministrationTargetReader(TicketsDbContext db) : IAdministrationTargetReader
{
    public Task<ActiveAdministrationTargetFact?> ReadActiveAsync(long userId, CancellationToken cancellationToken) =>
        db.AdministrationUsers.AsNoTracking()
            .Where(x => x.Id == userId && x.Active)
            .Select(x => new ActiveAdministrationTargetFact(x.Id, x.DepartmentId, x.TeamId, x.Active))
            .SingleOrDefaultAsync(cancellationToken);
}
