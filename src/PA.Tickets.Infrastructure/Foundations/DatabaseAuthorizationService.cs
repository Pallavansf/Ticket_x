using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class DatabaseAuthorizationService(TicketsDbContext db, TimeProvider clock) : IAuthorizationService
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
        var user = await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorUserId && x.Active, ct);
        if (user is null) return false;

        var grants = from membership in db.AdministrationUserRoles.AsNoTracking()
                     join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                     join rolePermission in db.AdministrationRolePermissions.AsNoTracking() on role.Id equals rolePermission.RoleId
                     join grantedPermission in db.AdministrationPermissions.AsNoTracking() on rolePermission.PermissionId equals grantedPermission.Id
                     where membership.UserId == actorUserId && membership.Active
                         && membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now)
                         && role.Active && grantedPermission.Active && grantedPermission.Code == permission
                     select role.Code;
        var roles = await grants.ToListAsync(ct);
        if (roles.Count == 0) return false;

        if (roles.Contains("ADMINISTRATOR", StringComparer.OrdinalIgnoreCase)) return true;
        if (resource is null) return true;

        if (resource.DepartmentId.HasValue && user.DepartmentId != resource.DepartmentId) return false;
        if (resource.TeamId.HasValue && user.TeamId != resource.TeamId) return false;
        if (resource.ClientId.HasValue && !await HasClientMembershipAsync(actorUserId, resource.ClientId.Value, now, ct)) return false;
        if (resource.AssignedUserId.HasValue && resource.AssignedUserId == actorUserId) return true;
        return true;
    }

    private Task<bool> HasClientMembershipAsync(long userId, long clientId, DateTimeOffset now, CancellationToken ct) =>
        db.AdministrationUserClients.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ClientId == clientId && x.Active && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now), ct);
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

        if (await IsAdministratorAsync(actorUserId, ct)) return criteria;

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

        if (criteria.TeamId.HasValue && criteria.TeamId != user.TeamId)
            throw new UnauthorizedAccessException("The requested ticket search scope is outside the authenticated user's team.");

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
            if (user.TeamId is not null && assignee.TeamId is not null && assignee.TeamId != user.TeamId)
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
            TeamId = criteria.TeamId ?? user.TeamId,
            AssigneeId = hasOrganizationalScope || hasClientScope ? criteria.AssigneeId : actorUserId
        };
    }

    private async Task<bool> IsAdministratorAsync(long actorUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.AdministrationUserRoles.AsNoTracking()
            .Join(db.AdministrationRoles.AsNoTracking(), membership => membership.RoleId, role => role.Id, (membership, role) => new { membership.UserId, role.Code, RoleActive = role.Active, MembershipActive = membership.Active, membership.ValidFrom, membership.ValidTo })
            .AnyAsync(x => x.UserId == actorUserId && x.MembershipActive && x.RoleActive && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now) && x.Code == "ADMINISTRATOR", ct);
    }

    private Task<bool> HasClientMembershipAsync(long userId, long clientId, DateTimeOffset now, CancellationToken ct) =>
        db.AdministrationUserClients.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ClientId == clientId && x.Active && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now), ct);

    private static AuthorizationResource Resource(Ticket ticket) =>
        new(ticket.Id, ticket.CurrentDepartmentId, ticket.CurrentTeamId, ticket.ClientId, ticket.CurrentAssignee);
}