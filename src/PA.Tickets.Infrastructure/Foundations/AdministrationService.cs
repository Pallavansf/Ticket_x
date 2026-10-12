using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class AdministrationService(
    TicketsDbContext db,
    IAuthorizationService authorization,
    AuditChainCoordinator auditLocks,
    IDatabaseProviderCapabilities capabilities,
    ProviderTransactionRetry retry,
    TimeProvider clock) : IAdministrationService
{
    public async Task<IReadOnlyCollection<AdministrationUser>> ListUsersAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        return await db.AdministrationUsers.AsNoTracking().OrderBy(x => x.LoginName).Select(MapUserExpression()).ToListAsync(ct);
    }

    public async Task<AdministrationUser> GetUserAsync(long actor, long userId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        var row = await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("User was not found.");
        return MapUser(row);
    }

    public async Task<AdministrationUser> CreateUserAsync(long actor, string userCode, string loginName, string displayName, string? email, long? departmentId, long? teamId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("User code, login name, and display name are required.");
        if (teamId.HasValue && !await db.AdministrationTeams.AnyAsync(x => x.Id == teamId && x.DepartmentId == departmentId && x.Active, ct)) throw new ArgumentException("The team does not belong to the selected active department.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = new AdministrationUserRow { Id = await NextIdAsync("PA_TICKETS_USER_SEQ", ct), UserCode = userCode.Trim(), LoginName = loginName.Trim(), DisplayName = displayName.Trim(), Email = email, DepartmentId = departmentId, TeamId = teamId, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationUsers.Add(row);
        await AddAuditAsync(row.Id.ToString(), "USER_CREATED", actor, row.LoginName, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapUser(row);
    }

    public Task<AdministrationUser> SetUserActiveAsync(long actor, long userId, bool active, CancellationToken ct) =>
        UpdateUserActiveAsync(actor, userId, active, ct);

    private async Task<AdministrationUser> UpdateUserActiveAsync(long actor, long userId, bool active, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        var row = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("User was not found.");
        row.Active = active; row.UpdatedAt = clock.GetUtcNow(); row.UpdatedBy = actor;
        await AddAuditAsync(row.Id.ToString(), active ? "USER_ACTIVATED" : "USER_DEACTIVATED", actor, null, ct);
        await db.SaveChangesAsync(ct);
        return MapUser(row);
    }

    public async Task<IReadOnlyCollection<AdministrationDepartment>> ListDepartmentsAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        return (await db.AdministrationDepartments.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(MapDepartment).ToArray();
    }

    public async Task<AdministrationDepartment> CreateDepartmentAsync(long actor, string code, string name, string? description, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = new AdministrationDepartmentRow { Id = await NextIdAsync("PA_TICKETS_DEPARTMENT_SEQ", ct), Code = Required(code), Name = Required(name), Description = description, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationDepartments.Add(row); await AddAuditAsync(row.Id.ToString(), "DEPARTMENT_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return MapDepartment(row);
    }

    public async Task<AdministrationDepartment> SetDepartmentActiveAsync(long actor, long departmentId, bool active, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        var row = await db.AdministrationDepartments.SingleOrDefaultAsync(x => x.Id == departmentId, ct) ?? throw new KeyNotFoundException("Department was not found.");
        row.Active = active; await AddAuditAsync(row.Id.ToString(), active ? "DEPARTMENT_ACTIVATED" : "DEPARTMENT_DEACTIVATED", actor, null, ct); await db.SaveChangesAsync(ct); return MapDepartment(row);
    }

    public async Task<IReadOnlyCollection<AdministrationTeam>> ListTeamsAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        return (await db.AdministrationTeams.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(MapTeam).ToArray();
    }

    public async Task<AdministrationTeam> CreateTeamAsync(long actor, long departmentId, string code, string name, string? description, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        if (!await db.AdministrationDepartments.AnyAsync(x => x.Id == departmentId && x.Active, ct)) throw new ArgumentException("The department is not active.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = new AdministrationTeamRow { Id = await NextIdAsync("PA_TICKETS_TEAM_SEQ", ct), DepartmentId = departmentId, Code = Required(code), Name = Required(name), Description = description, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationTeams.Add(row); await AddAuditAsync(row.Id.ToString(), "TEAM_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return MapTeam(row);
    }

    public async Task<AdministrationTeam> SetTeamActiveAsync(long actor, long teamId, bool active, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", null, ct);
        var row = await db.AdministrationTeams.SingleOrDefaultAsync(x => x.Id == teamId, ct) ?? throw new KeyNotFoundException("Team was not found.");
        row.Active = active; await AddAuditAsync(row.Id.ToString(), active ? "TEAM_ACTIVATED" : "TEAM_DEACTIVATED", actor, null, ct); await db.SaveChangesAsync(ct); return MapTeam(row);
    }

    public async Task<IReadOnlyCollection<AdministrationRole>> ListRolesAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_ADMIN", null, ct);
        return (await db.AdministrationRoles.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(x => new AdministrationRole(x.Id, x.Code, x.Name, x.Description, x.SystemRole, x.Active)).ToArray();
    }

    public async Task<IReadOnlyCollection<AdministrationPermission>> ListPermissionsAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_ADMIN", null, ct);
        return (await db.AdministrationPermissions.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(x => new AdministrationPermission(x.Id, x.Code, x.Name, x.Description, x.Active)).ToArray();
    }

    public Task<AdministrationMembership> AssignRoleAsync(long actor, long userId, long roleId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct) =>
        retry.ExecuteAsync(db, () => AssignRoleOnceAsync(actor, userId, roleId, validFrom, validTo, ct), ct);

    private async Task<AdministrationMembership> AssignRoleOnceAsync(long actor, long userId, long roleId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_ADMIN", null, ct); ValidateDates(validFrom, validTo);
        if (!await db.AdministrationUsers.AnyAsync(x => x.Id == userId && x.Active, ct) || !await db.AdministrationRoles.AnyAsync(x => x.Id == roleId && x.Active, ct)) throw new KeyNotFoundException("The user or role was not found or inactive.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.AdministrationUserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == roleId && x.Active &&
                (x.ValidTo == null || x.ValidTo > validFrom) && (!validTo.HasValue || x.ValidFrom < validTo.Value), ct))
            throw new InvalidOperationException("An effective role membership already exists for this period.");
        var row = new AdministrationUserRoleRow { Id = await NextIdAsync("PA_TICKETS_USER_ROLE_SEQ", ct), UserId = userId, RoleId = roleId, ValidFrom = validFrom, ValidTo = validTo, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationUserRoles.Add(row); await AddAuditAsync(row.Id.ToString(), "USER_ROLE_ASSIGNED", actor, $"{userId}:{roleId}", ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(row.Id, row.UserId, row.RoleId, row.ValidFrom, row.ValidTo, row.Active);
    }

    public Task<AdministrationClientMembership> AssignClientAsync(long actor, long userId, long clientId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct) =>
        retry.ExecuteAsync(db, () => AssignClientOnceAsync(actor, userId, clientId, validFrom, validTo, ct), ct);

    private async Task<AdministrationClientMembership> AssignClientOnceAsync(long actor, long userId, long clientId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "CLIENT_ADMIN", null, ct); ValidateDates(validFrom, validTo);
        if (!await db.AdministrationUsers.AnyAsync(x => x.Id == userId && x.Active, ct)) throw new KeyNotFoundException("The user was not found or inactive.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.AdministrationUserClients.AnyAsync(x => x.UserId == userId && x.ClientId == clientId && x.Active &&
                (x.ValidTo == null || x.ValidTo > validFrom) && (!validTo.HasValue || x.ValidFrom < validTo.Value), ct))
            throw new InvalidOperationException("An effective client membership already exists for this period.");
        var row = new AdministrationUserClientRow { Id = await NextIdAsync("PA_TICKETS_USER_CLIENT_SEQ", ct), UserId = userId, ClientId = clientId, ValidFrom = validFrom, ValidTo = validTo, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationUserClients.Add(row); await AddAuditAsync(row.Id.ToString(), "USER_CLIENT_ASSIGNED", actor, $"{userId}:{clientId}", ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(row.Id, row.UserId, row.ClientId, row.ValidFrom, row.ValidTo, row.Active);
    }

    public async Task RemoveRoleAsync(long actor, long membershipId, CancellationToken ct) => await RemoveRoleInternalAsync(actor, membershipId, ct);
    private async Task RemoveRoleInternalAsync(long actor, long membershipId, CancellationToken ct)
    { await authorization.EnsurePermissionAsync(actor, "ROLE_ADMIN", null, ct); var row = await db.AdministrationUserRoles.SingleOrDefaultAsync(x => x.Id == membershipId, ct) ?? throw new KeyNotFoundException("Role membership was not found."); row.Active = false; row.ValidTo = clock.GetUtcNow(); await AddAuditAsync(row.Id.ToString(), "USER_ROLE_REMOVED", actor, null, ct); await db.SaveChangesAsync(ct); }

    public async Task RemoveClientAsync(long actor, long membershipId, CancellationToken ct)
    { await authorization.EnsurePermissionAsync(actor, "CLIENT_ADMIN", null, ct); var row = await db.AdministrationUserClients.SingleOrDefaultAsync(x => x.Id == membershipId, ct) ?? throw new KeyNotFoundException("Client membership was not found."); row.Active = false; row.ValidTo = clock.GetUtcNow(); await AddAuditAsync(row.Id.ToString(), "USER_CLIENT_REMOVED", actor, null, ct); await db.SaveChangesAsync(ct); }

    private async Task<long> NextIdAsync(string sequence, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try { await using var command = db.Database.GetDbConnection().CreateCommand(); var quotedSequence = "\"" + sequence + "\""; command.CommandText = capabilities.Kind == DatabaseProviderKind.PostgreSql ? $"select nextval('{quotedSequence}'::regclass)" : $"select {sequence}.NEXTVAL from DUAL"; return Convert.ToInt64(await command.ExecuteScalarAsync(ct)); }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private async Task AddAuditAsync(string id, string action, long actor, string? value, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, "ADMINISTRATION", id, ct); var now = clock.GetUtcNow(); var previous = await db.Audits.Where(x => x.EntityType == "ADMINISTRATION" && x.EntityId == id).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct); var canonical = System.Text.Json.JsonSerializer.Serialize(new { entityType = "ADMINISTRATION", entityId = id, action, actorUserId = actor, auditAtUtc = now.UtcDateTime, oldValue = (string?)null, newValue = value, reason = (string?)null, previousHash = previous }); var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant(); db.Audits.Add(new AuditRow { EntityType = "ADMINISTRATION", EntityId = id, Action = action, ActorId = actor, At = now, NewValue = value, Hash = hash, PreviousHash = previous });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }
    private static void ValidateDates(DateTimeOffset from, DateTimeOffset? to) { if (to is not null && to <= from) throw new ArgumentException("Effective end must be after effective start."); }
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A required value is missing.") : value.Trim();
    private static AdministrationUser MapUser(AdministrationUserRow x) => new(x.Id, x.UserCode, x.LoginName, x.DisplayName, x.Email, x.DepartmentId, x.TeamId, x.Active);
    private static AdministrationDepartment MapDepartment(AdministrationDepartmentRow x) => new(x.Id, x.Code, x.Name, x.Description, x.Active);
    private static AdministrationTeam MapTeam(AdministrationTeamRow x) => new(x.Id, x.DepartmentId, x.Code, x.Name, x.Description, x.Active);
    private static System.Linq.Expressions.Expression<Func<AdministrationUserRow, AdministrationUser>> MapUserExpression() => x => new AdministrationUser(x.Id, x.UserCode, x.LoginName, x.DisplayName, x.Email, x.DepartmentId, x.TeamId, x.Active);
}