using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class DatabaseAuthorizationService(
    TicketsDbContext db,
    TimeProvider clock,
    IAuthorizationFactsReader factsReader,
    IAdminHeadDelegationFactsReader delegationFactsReader) : IAuthorizationService
{
    public async Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct)
    {
        if (!await HasPermissionAsync(actorUserId, permission, resource, ct))
            throw new UnauthorizedAccessException("The authenticated user is not authorized for this operation.");
    }

    public async Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct)
    {
        if (actorUserId <= 0 || string.IsNullOrWhiteSpace(permission)) return false;
        var now = clock.GetUtcNow();
        var facts = await factsReader.ReadAsync(actorUserId, permission, now, ct);
        var user = facts.Actor;
        if (user is null) return false;

        var permissionMetadata = facts.Permission;
        if (permissionMetadata is null ||
            string.IsNullOrWhiteSpace(permissionMetadata.ModuleCode) ||
            string.IsNullOrWhiteSpace(permissionMetadata.ActionCode) ||
            string.IsNullOrWhiteSpace(permissionMetadata.ScopeClass) ||
            string.IsNullOrWhiteSpace(permissionMetadata.SecurityClass))
            return false;

        var activeRoles = facts.ActiveRoles;
        if (activeRoles.Any(x => x.Code == RoleAuthorizationPolicy.Spectator) &&
            !RoleAuthorizationPolicy.AllowsPermission(RoleAuthorizationPolicy.Spectator, true,
                permissionMetadata.SecurityClass, permissionMetadata.ActionCode, permissionMetadata.ReadOnly))
            return false;

        var grants = facts.Grants;

        foreach (var grant in grants)
        {
            if (!EffectiveGrantAuthorization.IsEffective(grant.Active, grant.EffectiveStatus,
                    grant.ValidFrom, grant.ValidTo, now))
                continue;
            if (!RoleAuthorizationPolicy.AllowsPermission(grant.RoleCode, grant.SystemRole, grant.SecurityClass,
                    permissionMetadata.ActionCode, grant.ReadOnly))
                continue;
            if (grant.RoleCode == RoleAuthorizationPolicy.LegacyAdministrator &&
                !RoleAuthorizationPolicy.IsLegacyAdministratorPermission(grant.PermissionCode))
                continue;
            if (!grant.SystemRole)
            {
                if (await HasCustomRoleScopeAsync(facts, grant.MembershipId, grant.ScopeClass, resource, now, ct))
                    return true;
                continue;
            }
            if (grant.RoleCode is RoleAuthorizationPolicy.Admin or RoleAuthorizationPolicy.AdminHead)
            {
                if (await HasAdministrativeScopeAsync(actorUserId, user, facts, grant.MembershipId, grant.RoleCode,
                        grant.ScopeClass, grant.SecurityClass, resource, now, ct))
                    return true;
                continue;
            }
            if (await HasStandardScopeAsync(actorUserId, facts, grant.MembershipId, grant.RoleCode, grant.ScopeClass, resource, now, ct))
                return true;
        }

        return false;
    }

    private async Task<bool> HasAdministrativeScopeAsync(
        long actorUserId,
        AuthorizationActorFact actor,
        AuthorizationFacts facts,
        long membershipId,
        string roleCode,
        string scopeClass,
        string securityClass,
        AuthorizationResource? resource,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var departmentScoped = securityClass.Equals("DEPARTMENT_ADMIN", StringComparison.OrdinalIgnoreCase) ||
                               scopeClass.Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase) ||
                               scopeClass.Equals("TEAM", StringComparison.OrdinalIgnoreCase) ||
                               scopeClass.Equals("ADMIN_GROUP", StringComparison.OrdinalIgnoreCase) ||
                               scopeClass.Equals("RECORD", StringComparison.OrdinalIgnoreCase);
        if (resource is null)
        {
            if (!departmentScoped)
                return scopeClass.Equals("NONE", StringComparison.OrdinalIgnoreCase);
            var scopeTypes = facts.Scopes.Where(x => x.UserRoleId == membershipId).Select(x => x.ScopeType);
            return scopeTypes.Any(type => ScopeMatchesClass(type, scopeClass));
        }
        if (departmentScoped)
        {
            if (string.Equals(resource.ResourceType, "USER", StringComparison.OrdinalIgnoreCase) &&
                resource.AssignedUserId.HasValue)
            {
                var delegationFacts = await delegationFactsReader.ReadAsync(
                    actorUserId, resource.AssignedUserId.Value, now, ct);
                return roleCode == RoleAuthorizationPolicy.AdminHead &&
                       delegationFacts.TargetIsPrivileged &&
                       facts.Scopes.Any(x => x.UserRoleId == membershipId && x.ScopeType == "ADMIN_GROUP") &&
                       delegationFacts.TargetIsDelegatedAdminOnly &&
                       delegationFacts.HasActiveDelegation;
            }
            return await HasCustomRoleScopeAsync(facts, membershipId, scopeClass, resource, now, ct);
        }
        if (resource.DepartmentId.HasValue && actor.DepartmentId != resource.DepartmentId) return false;
        if (resource.ClientId.HasValue && !await HasClientMembershipAsync(actorUserId, resource.ClientId.Value, now, ct)) return false;
        return true;
    }

    private async Task<bool> HasStandardScopeAsync(
        long actorUserId,
        AuthorizationFacts facts,
        long membershipId,
        string roleCode,
        string scopeClass,
        AuthorizationResource? resource,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (scopeClass.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            if (roleCode == RoleAuthorizationPolicy.SuperAdmin)
                return await HasCustomRoleScopeAsync(facts, membershipId, "ORGANIZATION", resource, now, ct);
            return true;
        }
        if (resource is null)
        {
            var scopeTypes = facts.Scopes.Where(x => x.UserRoleId == membershipId).Select(x => x.ScopeType);
            return scopeTypes.Any(type => ScopeMatchesClass(type, scopeClass));
        }
        if (resource.ClientId.HasValue && !await HasClientMembershipAsync(actorUserId, resource.ClientId.Value, now, ct)) return false;
        return await HasCustomRoleScopeAsync(facts, membershipId, scopeClass, resource, now, ct);
    }

    private async Task<bool> HasCustomRoleScopeAsync(
        AuthorizationFacts facts,
        long membershipId,
        string scopeClass,
        AuthorizationResource? resource,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (resource is null && scopeClass.Equals("NONE", StringComparison.OrdinalIgnoreCase)) return true;
        var scopes = facts.Scopes.Where(x => x.UserRoleId == membershipId).ToList();
        if (resource is null)
            return scopes.Any(x => ScopeMatchesClass(x.ScopeType, scopeClass));
        var matchingAdminGroup = scopes.Any(x => x.ScopeType == "ADMIN_GROUP" &&
            ScopeMatchesClass(x.ScopeType, scopeClass));
        if (matchingAdminGroup && resource.AssignedUserId.HasValue &&
            await HasAdminDelegationAsyncForScope(membershipId, resource.AssignedUserId.Value, now, ct))
            return true;
        return scopes.Any(x =>
            ScopeMatchesClass(x.ScopeType, scopeClass) &&
            ((x.ScopeType == "ORGANIZATION" && OrganizationScopeAuthorization.MatchesResourceOrganization(x.OrganizationId, resource.OrganizationId)) ||
             (x.ScopeType == "DEPARTMENT" && x.DepartmentId == resource.DepartmentId) ||
             (x.ScopeType == "TEAM" && x.DepartmentId == resource.DepartmentId && x.TeamId == resource.TeamId) ||
             (x.ScopeType == "CLIENT" && x.ClientId == resource.ClientId) ||
             (x.ScopeType == "RECORD" &&
               string.Equals(x.ResourceType, resource.ResourceType ?? (resource.TicketId.HasValue ? "TICKET" : null), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.ResourceId, resource.ResourceId ?? resource.TicketId?.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))));
    }

    private async Task<bool> HasAdminDelegationAsyncForScope(long membershipId, long adminUserId, DateTimeOffset now, CancellationToken ct)
    {
        var headId = await db.AdministrationUserRoles.AsNoTracking()
            .Where(x => x.Id == membershipId).Select(x => x.UserId).SingleOrDefaultAsync(ct);
        return headId > 0 && await HasAdminDelegationAsync(headId, adminUserId, now, ct);
    }

    internal static bool ScopeMatchesClass(string scopeType, string scopeClass) =>
        PA.Tickets.Application.Foundations.ScopeAuthorizationPolicy.ScopeMatchesClass(scopeType, scopeClass);

    private Task<bool> HasClientMembershipAsync(long userId, long clientId, DateTimeOffset now, CancellationToken ct) =>
        db.AdministrationUserClients.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ClientId == clientId && x.Active && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now), ct);

    private Task<bool> HasAdminDelegationAsync(long adminHeadUserId, long adminUserId, DateTimeOffset now, CancellationToken ct) =>
        (from delegation in db.AdminHeadDelegations.AsNoTracking()
         join membership in db.AdministrationUserRoles.AsNoTracking() on delegation.AdminUserId equals membership.UserId
         join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
         join target in db.AdministrationUsers.AsNoTracking() on delegation.AdminUserId equals target.Id
         where delegation.AdminHeadUserId == adminHeadUserId && delegation.AdminUserId == adminUserId &&
               delegation.Active && delegation.ValidFrom <= now && (delegation.ValidTo == null || delegation.ValidTo > now) &&
               membership.Active && membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
               role.Active && role.Code == RoleAuthorizationPolicy.Admin && target.Active
         select delegation.Id).AnyAsync(ct);

    private async Task<bool> IsDelegatedAdminOnlyAsync(long userId, DateTimeOffset now, CancellationToken ct)
    {
        var activeRoleCodes = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                                     join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                                     join user in db.AdministrationUsers.AsNoTracking() on membership.UserId equals user.Id
                                     where user.Id == userId && user.Active && membership.Active &&
                                           membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
                                           role.Active
                                     select role.Code).ToListAsync(ct);
        return PA.Tickets.Application.Foundations.AdminHeadDelegationPolicy.IsDelegatedAdminOnly(activeRoleCodes);
    }

    private Task<bool> IsPrivilegedTargetAsync(long userId, DateTimeOffset now, CancellationToken ct) =>
        (from membership in db.AdministrationUserRoles.AsNoTracking()
         join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
         where membership.UserId == userId && membership.Active &&
               membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
               role.Active && (role.Code == RoleAuthorizationPolicy.Admin ||
                               role.Code == RoleAuthorizationPolicy.AdminHead ||
                               role.Code == RoleAuthorizationPolicy.SuperAdmin ||
                               role.Code == RoleAuthorizationPolicy.LegacyAdministrator)
         select membership.Id).AnyAsync(ct);
}

public sealed class DatabaseTicketAccessAuthorizer(IAuthorizationService authorization, TicketsDbContext db, TimeProvider clock) : ITicketAccessAuthorizer
{
    public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) =>
        authorization.EnsurePermissionAsync(actorUserId, "TICKET_UPDATE", Resource(ticket), ct);

    public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) =>
        authorization.EnsurePermissionAsync(actorUserId, "TICKET_READ", Resource(ticket), ct);

    public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) =>
        authorization.EnsurePermissionAsync(actorUserId, "TICKET_READ", null, ct);

    public async Task EnsureCanAssignAsync(long actorUserId, AuthorizationResource ticketResource, long? departmentId, long? teamId, long? assigneeUserId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actorUserId, "TICKET_UPDATE", ticketResource, ct);

        var targetTeam = teamId.HasValue
            ? await db.AdministrationTeams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == teamId && x.Active, ct)
                ?? throw new ArgumentException("The assignment team is not active.")
            : null;
        var targetUser = assigneeUserId.HasValue
            ? await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assigneeUserId && x.Active, ct)
                ?? throw new ArgumentException("The assignment user is not active.")
            : null;

        var effectiveDepartmentId = departmentId ?? targetTeam?.DepartmentId ?? targetUser?.DepartmentId;
        var effectiveTeamId = teamId ?? targetUser?.TeamId;

        if (targetTeam is not null && effectiveDepartmentId != targetTeam.DepartmentId)
            throw new ArgumentException("The assignment team does not belong to the selected department.");
        if (targetUser is not null &&
            (targetUser.DepartmentId != effectiveDepartmentId || targetUser.TeamId != effectiveTeamId))
            throw new ArgumentException("The assignment user does not belong to the selected department and team.");

        await authorization.EnsurePermissionAsync(
            actorUserId,
            "TICKET_UPDATE",
            new AuthorizationResource(ticketResource.TicketId, effectiveDepartmentId, effectiveTeamId, ticketResource.ClientId),
            ct);
    }

    public async Task<TicketSearchCriteria> ScopeSearchAsync(long actorUserId, TicketSearchCriteria criteria, CancellationToken ct)
    {
        if (actorUserId <= 0) throw new UnauthorizedAccessException("A valid authenticated actor is required.");
        if (!await authorization.HasPermissionAsync(actorUserId, "TICKET_READ", null, ct))
            throw new UnauthorizedAccessException("The authenticated user is not authorized to search tickets.");

        var user = await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorUserId && x.Active, ct);
        if (user is null) throw new UnauthorizedAccessException("The authenticated user is not authorized to search tickets.");

        if (await authorization.HasPermissionAsync(actorUserId, "TICKET_READ", new AuthorizationResource(OrganizationId: null), ct) && await OrganizationScopeAuthorization.HasEffectiveSuperAdminOrganizationScopeAsync(db, actorUserId, clock.GetUtcNow(), ct)) return criteria;
        var now = clock.GetUtcNow();
        var departmentAdmin = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                                     join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                                     where membership.UserId == actorUserId && membership.Active && role.Active &&
                                           membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
                                           (role.Code == RoleAuthorizationPolicy.Admin || role.Code == RoleAuthorizationPolicy.AdminHead)
                                     select membership.Id).AnyAsync(ct);

        var hasExplicitScope =
            criteria.DepartmentId.HasValue ||
            criteria.TeamId.HasValue ||
            criteria.ClientId.HasValue ||
            criteria.AssigneeId.HasValue ||
            !string.IsNullOrWhiteSpace(criteria.Number) ||
            criteria.Status is not null ||
            criteria.Type is not null ||
            criteria.PriorityId.HasValue ||
            criteria.ServiceId.HasValue ||
            criteria.CategoryId.HasValue ||
            criteria.RequestTypeId.HasValue ||
            criteria.CreatedFrom is not null ||
            criteria.CreatedTo is not null;

        if (!hasExplicitScope)
            throw new UnauthorizedAccessException("Ticket search requires a department, team, client, assignee, or other explicit scope filter.");

        if (criteria.DepartmentId.HasValue && criteria.DepartmentId != user.DepartmentId)
            throw new UnauthorizedAccessException("The requested ticket search scope is outside the authenticated user's department.");

        if (criteria.TeamId.HasValue && !departmentAdmin && criteria.TeamId != user.TeamId)
            throw new UnauthorizedAccessException("The requested ticket search scope is outside the authenticated user's team.");
        if (departmentAdmin && criteria.TeamId.HasValue &&
            !await db.AdministrationTeams.AsNoTracking().AnyAsync(x =>
                x.Id == criteria.TeamId && x.DepartmentId == user.DepartmentId && x.Active, ct))
            throw new UnauthorizedAccessException("The requested ticket search team is outside the authenticated user's department.");

        if (criteria.ClientId.HasValue && !await HasClientMembershipAsync(actorUserId, criteria.ClientId.Value, clock.GetUtcNow(), ct))
            throw new UnauthorizedAccessException("The requested ticket search scope is outside the authenticated user's client access.");

        if (criteria.AssigneeId.HasValue)
        {
            if (user.DepartmentId is null && user.TeamId is null && criteria.AssigneeId != actorUserId)
                throw new UnauthorizedAccessException("A user without an organizational scope may only search their own assignments.");
            var assignee = await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == criteria.AssigneeId && x.Active, ct);
            if (assignee is null) throw new UnauthorizedAccessException("The assignee is not available to this ticket search.");
            if (user.DepartmentId is not null && assignee.DepartmentId is not null && assignee.DepartmentId != user.DepartmentId)
                throw new UnauthorizedAccessException("The assignee scope falls outside the authenticated user's department.");
            if (!departmentAdmin && user.TeamId is not null && assignee.TeamId is not null && assignee.TeamId != user.TeamId)
                throw new UnauthorizedAccessException("The assignee scope falls outside the authenticated user's team.");
        }

        var hasOrganizationalScope = user.DepartmentId.HasValue || user.TeamId.HasValue;
        var hasClientScope = criteria.ClientId.HasValue;
        var hasSelfAssignmentScope = criteria.AssigneeId == actorUserId;
        if (!hasOrganizationalScope && !hasClientScope && !hasSelfAssignmentScope)
            throw new UnauthorizedAccessException("Ticket search requires an active department, team, authorized client, or own-assignment scope.");

        return criteria with
        {
            DepartmentId = criteria.DepartmentId ?? user.DepartmentId,
            TeamId = criteria.TeamId ?? (departmentAdmin ? null : user.TeamId),
            AssigneeId = hasOrganizationalScope || hasClientScope ? criteria.AssigneeId : actorUserId
        };
    }
    private Task<bool> HasClientMembershipAsync(long userId, long clientId, DateTimeOffset now, CancellationToken ct) =>
        db.AdministrationUserClients.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ClientId == clientId && x.Active && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now), ct);

    private static AuthorizationResource Resource(Ticket ticket) =>
        new(ticket.Id, ticket.CurrentDepartmentId, ticket.CurrentTeamId, ticket.ClientId, ticket.CurrentAssignee);
}

