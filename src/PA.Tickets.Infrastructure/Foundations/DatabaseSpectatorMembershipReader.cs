using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class DatabaseSpectatorMembershipReader(TicketsDbContext db) : ISpectatorMembershipReader
{
    public Task<bool> IsActiveSpectatorAsync(
        long userId,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        (from user in db.AdministrationUsers.AsNoTracking()
         join membership in db.AdministrationUserRoles.AsNoTracking() on user.Id equals membership.UserId
         join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
         where user.Id == userId && user.Active && membership.Active &&
               membership.ValidFrom <= at && (membership.ValidTo == null || membership.ValidTo > at) &&
               role.Active && role.Code == "SPECTATOR"
         select user.Id).AnyAsync(cancellationToken);
}
