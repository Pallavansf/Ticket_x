using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class DatabaseAuthorizationFactsReader(TicketsDbContext db) : IAuthorizationFactsReader
{
    public async Task<AuthorizationFacts> ReadAsync(
        long actorUserId,
        string permission,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var actor = await db.AdministrationUsers.AsNoTracking()
            .Where(x => x.Id == actorUserId && x.Active)
            .Select(x => new AuthorizationActorFact(x.Id, x.DepartmentId, x.TeamId, x.Active))
            .SingleOrDefaultAsync(cancellationToken);

        var permissionMetadata = await db.AdministrationPermissions.AsNoTracking()
            .Where(x => x.Active && x.Code == permission)
            .Select(x => new AuthorizationPermissionFact(
                x.Id, x.Code, x.ModuleCode, x.ActionCode, x.ScopeClass,
                x.SecurityClass, x.ReadOnly, x.Active))
            .SingleOrDefaultAsync(cancellationToken);

        var activeRoles = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                                 join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                                 where membership.UserId == actorUserId && membership.Active &&
                                       membership.ValidFrom <= at && (membership.ValidTo == null || membership.ValidTo > at) &&
                                       role.Active
                                 select new AuthorizationRoleFact(membership.Id, role.Code, role.SystemRole))
            .ToListAsync(cancellationToken);

        var grants = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                            join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                            join rolePermission in db.AdministrationRolePermissions.AsNoTracking() on role.Id equals rolePermission.RoleId
                            join grantedPermission in db.AdministrationPermissions.AsNoTracking() on rolePermission.PermissionId equals grantedPermission.Id
                            where membership.UserId == actorUserId && membership.Active &&
                                  membership.ValidFrom <= at && (membership.ValidTo == null || membership.ValidTo > at) &&
                                  role.Active && rolePermission.Active && rolePermission.EffectiveStatus == "ACTIVE" &&
                                  rolePermission.ValidFrom.HasValue && rolePermission.ValidFrom <= at &&
                                  (rolePermission.ValidTo == null || rolePermission.ValidTo > at) &&
                                  grantedPermission.Active && grantedPermission.Code == permission
                            select new AuthorizationGrantFact(
                                membership.Id, role.Code, role.SystemRole, rolePermission.Active,
                                rolePermission.EffectiveStatus, rolePermission.ValidFrom, rolePermission.ValidTo,
                                grantedPermission.Code, grantedPermission.SecurityClass,
                                grantedPermission.ScopeClass, grantedPermission.ReadOnly))
            .ToListAsync(cancellationToken);

        var scopes = await db.AdministrationUserRoleScopes.AsNoTracking()
            .Where(x => x.Active && x.ValidFrom <= at && (x.ValidTo == null || x.ValidTo > at) &&
                        activeRoles.Select(role => role.MembershipId).Contains(x.UserRoleId))
            .Select(x => new AuthorizationScopeFact(
                x.Id, x.UserRoleId, x.ScopeType, x.OrganizationId, x.DepartmentId,
                x.TeamId, x.ClientId, x.ResourceType, x.ResourceId,
                x.ValidFrom, x.ValidTo, x.Active))
            .ToListAsync(cancellationToken);

        return new AuthorizationFacts(actor, permissionMetadata, activeRoles, grants, scopes);
    }
}
