using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class DatabaseDelegationFactsReader(TicketsDbContext db) : IAdminHeadDelegationFactsReader
{
    public async Task<AdminHeadDelegationFacts> ReadAsync(
        long adminHeadUserId,
        long targetUserId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var targetIsPrivileged = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                                        join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                                        where membership.UserId == targetUserId && membership.Active &&
                                              membership.ValidFrom <= at && (membership.ValidTo == null || membership.ValidTo > at) &&
                                              role.Active && (role.Code == RoleAuthorizationPolicy.Admin ||
                                                              role.Code == RoleAuthorizationPolicy.AdminHead ||
                                                              role.Code == RoleAuthorizationPolicy.SuperAdmin ||
                                                              role.Code == RoleAuthorizationPolicy.LegacyAdministrator)
                                        select membership.Id).AnyAsync(cancellationToken);

        var targetRoleCodes = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                                     join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                                     join user in db.AdministrationUsers.AsNoTracking() on membership.UserId equals user.Id
                                     where user.Id == targetUserId && user.Active && membership.Active &&
                                           membership.ValidFrom <= at && (membership.ValidTo == null || membership.ValidTo > at) &&
                                           role.Active
                                     select role.Code).ToListAsync(cancellationToken);

        var hasActiveDelegation = await (from delegation in db.AdminHeadDelegations.AsNoTracking()
                                         join membership in db.AdministrationUserRoles.AsNoTracking() on delegation.AdminUserId equals membership.UserId
                                         join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                                         join target in db.AdministrationUsers.AsNoTracking() on delegation.AdminUserId equals target.Id
                                         where delegation.AdminHeadUserId == adminHeadUserId && delegation.AdminUserId == targetUserId &&
                                               delegation.Active && delegation.ValidFrom <= at &&
                                               (delegation.ValidTo == null || delegation.ValidTo > at) &&
                                               membership.Active && membership.ValidFrom <= at &&
                                               (membership.ValidTo == null || membership.ValidTo > at) &&
                                               role.Active && role.Code == RoleAuthorizationPolicy.Admin && target.Active
                                         select delegation.Id).AnyAsync(cancellationToken);

        return new AdminHeadDelegationFacts(
            targetIsPrivileged,
            AdminHeadDelegationPolicy.IsDelegatedAdminOnly(targetRoleCodes),
            hasActiveDelegation);
    }
}
