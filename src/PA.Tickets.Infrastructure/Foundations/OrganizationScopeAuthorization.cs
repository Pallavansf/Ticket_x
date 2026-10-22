using Microsoft.EntityFrameworkCore;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

internal static class OrganizationScopeAuthorization
{
    internal static bool MatchesResourceOrganization(long? scopeOrganizationId, long? resourceOrganizationId) =>
        scopeOrganizationId == resourceOrganizationId;

    public static Task<bool> HasEffectiveSuperAdminOrganizationScopeAsync(
        TicketsDbContext db, long userId, DateTimeOffset now, CancellationToken ct) =>
        (from membership in db.AdministrationUserRoles.AsNoTracking()
         join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
         join scope in db.AdministrationUserRoleScopes.AsNoTracking() on membership.Id equals scope.UserRoleId
         where membership.UserId == userId && membership.Active &&
               membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
               role.Active && role.Code == RoleAuthorizationPolicy.SuperAdmin &&
               scope.Active && scope.ScopeType == "ORGANIZATION" && scope.OrganizationId == null &&
               scope.ValidFrom <= now && (scope.ValidTo == null || scope.ValidTo > now)
         select scope.Id).AnyAsync(ct);
}
