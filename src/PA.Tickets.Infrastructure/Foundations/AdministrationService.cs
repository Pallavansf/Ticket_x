using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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
    TimeProvider clock,
    IPasswordHasher<AdministrationUserRow> passwordHasher,
    IAdministrationTargetReader targetReader) : IAdministrationService
{
    public async Task<IReadOnlyCollection<AdministrationUser>> ListUsersAsync(long actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await IsAdministratorAsync(actor, ct))
        {
            var users = await db.AdministrationUsers.AsNoTracking()
                .OrderBy(x => x.LoginName)
                .Select(MapUserExpression()).ToListAsync(ct);
            await transaction.CommitAsync(ct);
            return users;
        }
        var actorRow = await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor && x.Active, ct)
            ?? throw new UnauthorizedAccessException("The authenticated user is not authorized for this operation.");
        if (!actorRow.DepartmentId.HasValue)
            throw new UnauthorizedAccessException("Department-scoped user administration requires an active department.");
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", new AuthorizationResource(DepartmentId: actorRow.DepartmentId), ct);
        var assignedAdminIds = await ActiveDelegatedAdminIdsAsync(actor, ct);
        var departmentId = actorRow.DepartmentId.Value;
        var allUsers = await db.AdministrationUsers.AsNoTracking()
            .Where(x => x.DepartmentId == departmentId || assignedAdminIds.Contains(x.Id))
            .OrderBy(x => x.LoginName).Select(MapUserExpression()).ToListAsync(ct);
        var privilegedRoleHolderIds = await PrivilegedRoleHolderIdsAsync(allUsers.Select(x => x.Id), ct);
        allUsers = allUsers.Where(x => !privilegedRoleHolderIds.Contains(x.Id) || assignedAdminIds.Contains(x.Id)).ToList();
        await transaction.CommitAsync(ct);
        return allUsers;
    }

    public async Task<AdministrationUser> GetUserAsync(long actor, long userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new KeyNotFoundException("User was not found.");
        if (await IsAdministratorAsync(actor, ct))
        {
            await EnsureSuperAdminProfileAccessAsync(actor, row, ct);
        }
        else if (actor != userId)
        {
            await EnsureUserAdministrationAsync(actor, row, ct);
            if (await HasAdministratorRoleAsync(row.Id, ct))
                throw new UnauthorizedAccessException("An administrator profile is not available through ordinary user administration.");
        }
        else if (!await db.AdministrationUsers.AnyAsync(x => x.Id == actor && x.Active, ct))
        {
            throw new UnauthorizedAccessException("The authenticated user is not authorized for this operation.");
        }
        var profile = MapUser(row);
        await transaction.CommitAsync(ct);
        return profile;
    }

    public async Task<AdministrationUser> UpdateUserProfileAsync(
        long actor,
        long userId,
        UserProfileUpdate profile,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new KeyNotFoundException("User was not found.");
        var administrator = await IsAdministratorAsync(actor, ct);
        if (actor != userId)
        {
            if (administrator)
                await EnsureSuperAdminProfileAccessAsync(actor, user, ct);
            else
            {
                await EnsureUserAdministrationAsync(actor, user, ct);
                if (await HasAdministratorRoleAsync(user.Id, ct))
                    throw new UnauthorizedAccessException("A department administrator cannot modify a privileged root account.");
            }
        }
        else if (!await db.AdministrationUsers.AnyAsync(x => x.Id == actor && x.Active, ct))
        {
            throw new UnauthorizedAccessException("The authenticated user is not authorized for this operation.");
        }

        var displayName = Required(profile.DisplayName, nameof(profile.DisplayName), 300);
        var email = NormalizeEmail(profile.Email, nameof(profile.Email));
        var phone = Optional(profile.Phone, nameof(profile.Phone), 50);
        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        if (email is not null && await db.AdministrationUsers.AnyAsync(
                x => x.Id != userId && x.Email != null && x.Email.ToUpper() == email.ToUpper(), ct))
            throw new ArgumentException("Email address is already in use.", nameof(profile));

        var before = new { displayName = user.DisplayName, email = user.Email, phone = user.Phone };
        var changedFields = new List<string>();
        if (!string.Equals(user.DisplayName, displayName, StringComparison.Ordinal)) changedFields.Add("displayName");
        if (emailChanged) changedFields.Add("email");
        if (!string.Equals(user.Phone, phone, StringComparison.Ordinal)) changedFields.Add("phone");
        if (changedFields.Count == 0) return MapUser(user);

        user.DisplayName = displayName;
        user.Email = email;
        user.Phone = phone;
        if (emailChanged) user.EmailVerified = false;
        var now = clock.GetUtcNow();
        user.UpdatedAt = now;
        user.UpdatedBy = actor;
        var after = new { displayName = user.DisplayName, email = user.Email, phone = user.Phone };
        await AddSensitiveUserAuditAsync(
            userId,
            actor,
            "USER_PROFILE_UPDATED",
            changedFields,
            before,
            after,
            now,
            actor == userId ? "SELF_PROFILE_UPDATE" : "SUBORDINATE_PROFILE_UPDATE",
            ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MapUser(user);
    }

    public async Task<AdministrationUser> SetReportingManagerAsync(
        long actor,
        long userId,
        long? managerUserId,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId && x.Active, ct)
            ?? throw new KeyNotFoundException("Active user was not found.");
        await EnsureUserAdministrationAsync(actor, user, ct);
        if (await IsAdministratorAsync(userId, ct))
            throw new UnauthorizedAccessException("A Super Admin cannot be placed under another reporting manager.");
        if (managerUserId == userId)
            throw new ArgumentException("A user cannot report to themselves.", nameof(managerUserId));
        if (managerUserId.HasValue &&
            !await db.AdministrationUsers.AnyAsync(x => x.Id == managerUserId && x.Active, ct))
            throw new ArgumentException("Reporting manager must be an active user.", nameof(managerUserId));
        if (managerUserId.HasValue && !await IsAdministratorAsync(actor, ct))
        {
            var manager = await db.AdministrationUsers.AsNoTracking().SingleAsync(x => x.Id == managerUserId.Value, ct);
            if (user.DepartmentId is null || user.DepartmentId != manager.DepartmentId)
                throw new UnauthorizedAccessException("Department administrators may only set reporting relationships within the user's department.");
        }
        if (managerUserId.HasValue &&
            (await SubordinateIdsAsync(userId, ct)).Contains(managerUserId.Value))
            throw new ArgumentException("Reporting-manager assignment would create a hierarchy cycle.", nameof(managerUserId));
        if (user.ManagerUserId == managerUserId) return MapUser(user);

        var oldManagerId = user.ManagerUserId;
        user.ManagerUserId = managerUserId;
        user.UpdatedAt = clock.GetUtcNow();
        user.UpdatedBy = actor;
        await AddSensitiveUserAuditAsync(
            userId,
            actor,
            "USER_REPORTING_MANAGER_CHANGED",
            ["managerUserId"],
            new { managerUserId = oldManagerId },
            new { managerUserId },
            user.UpdatedAt.Value,
            "REPORTING_MANAGER_ADMINISTRATION",
            ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MapUser(user);
    }

    public async Task<AdministrationUser> CreateUserAsync(long actor, string userCode, string loginName, string displayName, string? email, long? departmentId, long? teamId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", new AuthorizationResource(DepartmentId: departmentId), ct);
        if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("User code, login name, and display name are required.");
        email = NormalizeEmail(email, nameof(email));
        if (email is not null && await db.AdministrationUsers.AnyAsync(
                x => x.Email != null && x.Email.ToUpper() == email.ToUpper(), ct))
            throw new ArgumentException("Email address is already in use.", nameof(email));
        if (teamId.HasValue && !await db.AdministrationTeams.AnyAsync(x => x.Id == teamId && x.DepartmentId == departmentId && x.Active, ct)) throw new ArgumentException("The team does not belong to the selected active department.");
        var row = new AdministrationUserRow { Id = await NextIdAsync("PA_TICKETS_USER_SEQ", ct), UserCode = userCode.Trim(), LoginName = loginName.Trim(), DisplayName = displayName.Trim(), Email = email, DepartmentId = departmentId, TeamId = teamId, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationUsers.Add(row);
        await AddAuditAsync(row.Id.ToString(), "USER_CREATED", actor, row.LoginName, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapUser(row);
    }

    public Task<AdministrationUser> SetUserActiveAsync(long actor, long userId, bool active, CancellationToken ct) =>
        UpdateUserActiveAsync(actor, userId, active, ct);

    public async Task SetUserPasswordAsync(long actor, long userId, string password, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 1024)
            throw new ArgumentException("Password must contain between 12 and 1024 characters.");
        var row = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId && x.Active, ct)
            ?? throw new KeyNotFoundException("The active user was not found.");
        await EnsureUserAdministrationAsync(actor, row, ct);
        await EnsureNotOtherAdministratorAsync(actor, row, ct);
        row.PasswordHash = passwordHasher.HashPassword(row, password);
        row.PasswordRotationRequired = true;
        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = actor;
        await AddAuditAsync(row.Id.ToString(), "USER_PASSWORD_SET", actor, null, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<AdministrationUser> UpdateUserActiveAsync(long actor, long userId, bool active, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("User was not found.");
        if (row.Id == actor)
            throw new UnauthorizedAccessException("Users cannot change their own active status.");
        await EnsureUserAdministrationAsync(actor, row, ct);
        await EnsureNotOtherAdministratorAsync(actor, row, ct);
        row.Active = active; row.UpdatedAt = clock.GetUtcNow(); row.UpdatedBy = actor;
        await AddAuditAsync(row.Id.ToString(), active ? "USER_ACTIVATED" : "USER_DEACTIVATED", actor, null, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MapUser(row);
    }

    public async Task<IReadOnlyCollection<AdministrationDepartment>> ListDepartmentsAsync(long actor, CancellationToken ct)
    {
        if (await IsAdministratorAsync(actor, ct))
            return (await db.AdministrationDepartments.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(MapDepartment).ToArray();
        var actorDepartmentId = await ActiveDepartmentIdAsync(actor, ct);
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", new AuthorizationResource(DepartmentId: actorDepartmentId), ct);
        return (await db.AdministrationDepartments.AsNoTracking().Where(x => x.Id == actorDepartmentId).OrderBy(x => x.Code).ToListAsync(ct)).Select(MapDepartment).ToArray();
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
        await authorization.EnsurePermissionAsync(actor, "CONFIG_ADMIN", new AuthorizationResource(DepartmentId: departmentId), ct);
        var row = await db.AdministrationDepartments.SingleOrDefaultAsync(x => x.Id == departmentId, ct) ?? throw new KeyNotFoundException("Department was not found.");
        row.Active = active; await AddAuditAsync(row.Id.ToString(), active ? "DEPARTMENT_ACTIVATED" : "DEPARTMENT_DEACTIVATED", actor, null, ct); await db.SaveChangesAsync(ct); return MapDepartment(row);
    }

    public async Task<IReadOnlyCollection<AdministrationTeam>> ListTeamsAsync(long actor, CancellationToken ct)
    {
        if (await IsAdministratorAsync(actor, ct))
            return (await db.AdministrationTeams.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(MapTeam).ToArray();
        var actorDepartmentId = await ActiveDepartmentIdAsync(actor, ct);
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", new AuthorizationResource(DepartmentId: actorDepartmentId), ct);
        return (await db.AdministrationTeams.AsNoTracking().Where(x => x.DepartmentId == actorDepartmentId).OrderBy(x => x.Code).ToListAsync(ct)).Select(MapTeam).ToArray();
    }

    public async Task<AdministrationTeam> CreateTeamAsync(long actor, long departmentId, string code, string name, string? description, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", new AuthorizationResource(DepartmentId: departmentId), ct);
        if (!await db.AdministrationDepartments.AnyAsync(x => x.Id == departmentId && x.Active, ct)) throw new ArgumentException("The department is not active.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = new AdministrationTeamRow { Id = await NextIdAsync("PA_TICKETS_TEAM_SEQ", ct), DepartmentId = departmentId, Code = Required(code), Name = Required(name), Description = description, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationTeams.Add(row); await AddAuditAsync(row.Id.ToString(), "TEAM_CREATED", actor, row.Code, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return MapTeam(row);
    }

    public async Task<AdministrationTeam> SetTeamActiveAsync(long actor, long teamId, bool active, CancellationToken ct)
    {
        var row = await db.AdministrationTeams.SingleOrDefaultAsync(x => x.Id == teamId, ct) ?? throw new KeyNotFoundException("Team was not found.");
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", new AuthorizationResource(DepartmentId: row.DepartmentId, TeamId: row.Id), ct);
        row.Active = active; await AddAuditAsync(row.Id.ToString(), active ? "TEAM_ACTIVATED" : "TEAM_DEACTIVATED", actor, null, ct); await db.SaveChangesAsync(ct); return MapTeam(row);
    }

    public async Task<IReadOnlyCollection<AdministrationRole>> ListRolesAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        return (await db.AdministrationRoles.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(x => new AdministrationRole(x.Id, x.Code, x.Name, x.Description, x.SystemRole, x.Active)).ToArray();
    }

    public async Task<IReadOnlyCollection<AdministrationPermission>> ListPermissionsAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        return (await db.AdministrationPermissions.AsNoTracking().OrderBy(x => x.ModuleCode).ThenBy(x => x.ActionCode).ThenBy(x => x.Code).ToListAsync(ct))
            .Select(x => new AdministrationPermission(x.Id, x.Code, x.Name, x.Description, x.ModuleCode, x.ActionCode, x.ScopeClass, x.SecurityClass, x.ReadOnly, x.Active)).ToArray();
    }

    public async Task<AdministrationRole> UpdateSystemRoleDisplayLabelAsync(
        long actor, long roleId, string displayLabel, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        var role = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == roleId && x.Active, ct)
            ?? throw new KeyNotFoundException("Active role was not found.");
        var label = SystemRoleDisplayLabelPolicy.ValidateAndNormalize(role.Code, role.SystemRole, displayLabel);
        reason = Required(reason, nameof(reason), 1000);
        if (string.Equals(role.Name, label, StringComparison.Ordinal))
        {
            await tx.CommitAsync(ct);
            return new(role.Id, role.Code, role.Name, role.Description, role.SystemRole, role.Active);
        }

        var oldLabel = role.Name;
        role.Name = label;
        await AddAuditAsync($"ROLE:{role.Id}", "SYSTEM_ROLE_DISPLAY_LABEL_UPDATED", actor,
            $"{role.Code}:{label}", ct, reason, $"{role.Code}:{oldLabel}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(role.Id, role.Code, role.Name, role.Description, role.SystemRole, role.Active);
    }

    public async Task<AdministrationRole> CreateCustomRoleAsync(long actor, string code, string name, string? description, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        var normalizedCode = NormalizeCustomRoleCode(code);
        name = Required(name, nameof(name), 200);
        description = Optional(description, nameof(description), 1000);
        if (await db.AdministrationRoles.AnyAsync(x => x.Code.ToUpper() == normalizedCode, ct))
            throw new InvalidOperationException("A role with that code already exists.");

        var row = new AdministrationRoleRow
        {
            Id = await NextIdAsync("PA_TICKETS_ROLE_SEQ", ct),
            Code = normalizedCode,
            Name = name,
            Description = description,
            SystemRole = false,
            Active = true
        };
        db.AdministrationRoles.Add(row);
        await AddAuditAsync($"ROLE:{row.Id}", "CUSTOM_ROLE_CREATED", actor, $"{row.Code}:{row.Name}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(row.Id, row.Code, row.Name, row.Description, row.SystemRole, row.Active);
    }

    public async Task<AdministrationRole> UpdateCustomRoleAsync(long actor, long roleId, string name, string? description, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        var row = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == roleId, ct)
            ?? throw new KeyNotFoundException("Role was not found.");
        EnsureCustomRole(row);
        name = Required(name, nameof(name), 200);
        description = Optional(description, nameof(description), 1000);
        var oldValue = $"{row.Name}:{row.Description}";
        row.Name = name;
        row.Description = description;
        await AddAuditAsync($"ROLE:{row.Id}", "CUSTOM_ROLE_UPDATED", actor,
            $"{row.Name}:{row.Description}", ct, "Custom role details updated", oldValue);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(row.Id, row.Code, row.Name, row.Description, row.SystemRole, row.Active);
    }

    public async Task DeactivateCustomRoleAsync(long actor, long roleId, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        reason = Required(reason, nameof(reason), 1000);
        var row = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == roleId, ct)
            ?? throw new KeyNotFoundException("Role was not found.");
        EnsureCustomRole(row);
        if (row.Active)
        {
            var now = clock.GetUtcNow();
            row.Active = false;
            var memberships = await db.AdministrationUserRoles
                .Where(x => x.RoleId == row.Id && x.Active).ToListAsync(ct);
            foreach (var membership in memberships)
            {
                var membershipBefore = $"{membership.UserId}:{row.Code}:active;validFrom={membership.ValidFrom:O};validTo={membership.ValidTo:O}";
                membership.Active = false;
                if (now > membership.ValidFrom && (!membership.ValidTo.HasValue || now < membership.ValidTo.Value))
                    membership.ValidTo = now;
                await AddAuditAsync(membership.Id.ToString(), "USER_ROLE_REMOVED_WITH_ROLE", actor,
                    $"{membership.UserId}:{row.Code}:inactive;validFrom={membership.ValidFrom:O};validTo={membership.ValidTo:O}",
                    ct, reason, membershipBefore);
                var scopes = await db.AdministrationUserRoleScopes
                    .Where(x => x.UserRoleId == membership.Id && x.Active).ToListAsync(ct);
                foreach (var scope in scopes)
                {
                    var scopeBefore = $"{membership.Id}:{scope.ScopeType}:{scope.DepartmentId}:{scope.TeamId}:active;validFrom={scope.ValidFrom:O};validTo={scope.ValidTo:O}";
                    scope.Active = false;
                    if (now > scope.ValidFrom && (!scope.ValidTo.HasValue || now < scope.ValidTo.Value))
                        scope.ValidTo = now;
                    await AddAuditAsync($"ROLE_SCOPE:{scope.Id}", "ROLE_SCOPE_REMOVED_WITH_ROLE", actor,
                        $"{membership.Id}:{scope.ScopeType}:{scope.DepartmentId}:{scope.TeamId}:inactive;validFrom={scope.ValidFrom:O};validTo={scope.ValidTo:O}",
                        ct, reason, scopeBefore);
                }
            }
            await AddAuditAsync($"ROLE:{row.Id}", "CUSTOM_ROLE_DEACTIVATED", actor,
                $"{row.Code}:inactive", ct, reason, $"{row.Code}:active");
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyCollection<AdministrationRolePermission>> ListRolePermissionsAsync(long actor, long roleId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        if (!await db.AdministrationRoles.AnyAsync(x => x.Id == roleId, ct))
            throw new KeyNotFoundException("Role was not found.");
        return await db.AdministrationRolePermissions.AsNoTracking().Where(x => x.RoleId == roleId)
            .OrderBy(x => x.PermissionId).ThenBy(x => x.ValidFrom)
            .Select(x => new AdministrationRolePermission(x.RoleId, x.PermissionId, x.ValidFrom, x.ValidTo, x.Active, x.EffectiveStatus))
            .ToArrayAsync(ct);
    }

    public async Task SetRolePermissionAsync(
        long actor,
        long roleId,
        long permissionId,
        bool granted,
        string confirmation,
        string reason,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        var role = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == roleId && x.Active, ct)
            ?? throw new KeyNotFoundException("Active role was not found.");
        var permission = await db.AdministrationPermissions.SingleOrDefaultAsync(x => x.Id == permissionId && x.Active, ct)
            ?? throw new KeyNotFoundException("Active permission was not found.");
        if (role.Code == RoleAuthorizationPolicy.SuperAdmin && granted)
            throw new UnauthorizedAccessException("Super Admin permissions are immutable; privileged permission changes require a separately reviewed two-person workflow.");
        if (granted)
        {
            EnsureRolePermissionCeiling(role, permission);
            EnsureDirectPermissionGrantAllowed(role, permission);
        }
        else
        {
            RoleAuthorizationPolicy.EnsurePermissionRevocationAllowed(role.Code, permission.Code);
        }

        var now = clock.GetUtcNow();
        var activeGrant = await db.AdministrationRolePermissions
            .Where(x => x.RoleId == role.Id && x.PermissionId == permission.Id && x.Active && x.EffectiveStatus == "ACTIVE" && x.ValidFrom.HasValue &&
                         x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now))
            .OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(ct);
        reason = Optional(reason, nameof(reason), 1000) ?? "Role permission configuration";

        if (granted && activeGrant is null)
        {
            var row = new AdministrationRolePermissionRow
            {
                Id = await NextIdAsync("PA_TICKETS_ROLE_PERMISSION_SEQ", ct),
                RoleId = role.Id,
                PermissionId = permission.Id,
                    ValidFrom = now,
                    EffectiveStatus = "ACTIVE",
                    Active = true,
                UpdatedAt = now,
                UpdatedBy = actor
            };
            db.AdministrationRolePermissions.Add(row);
            await AddAuditAsync($"ROLE:{role.Id}", "ROLE_PERMISSION_GRANTED", actor,
                $"{role.Code}:{permission.Code}:granted", ct, reason, null);
        }
        else if (!granted && activeGrant is not null)
        {
            var before = $"{role.Code}:{permission.Code}:granted;validFrom={activeGrant.ValidFrom:O};validTo={activeGrant.ValidTo:O}";
            activeGrant.Active = false;
            activeGrant.ValidTo = now > activeGrant.ValidFrom!.Value ? now : activeGrant.ValidFrom!.Value.AddMilliseconds(1);
            activeGrant.UpdatedAt = now;
            activeGrant.UpdatedBy = actor;
            await AddAuditAsync($"ROLE:{role.Id}", "ROLE_PERMISSION_REVOKED", actor,
                $"{role.Code}:{permission.Code}:revoked;validFrom={activeGrant.ValidFrom:O};validTo={activeGrant.ValidTo:O}",
                ct, reason, before);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task SetRolePermissionsAsync(
        long actor,
        long roleId,
        IReadOnlyCollection<long> expectedActivePermissionIds,
        IReadOnlyCollection<AdministrationRolePermissionChange> changes,
        string reason,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(expectedActivePermissionIds);
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count is < 1 or > 250)
            throw new ArgumentException("A permission update batch must contain between 1 and 250 changes.", nameof(changes));
        if (expectedActivePermissionIds.Any(id => id <= 0) ||
            expectedActivePermissionIds.Distinct().Count() != expectedActivePermissionIds.Count)
            throw new ArgumentException("The expected permission snapshot contains invalid or duplicate IDs.", nameof(expectedActivePermissionIds));
        if (changes.Any(change => change.PermissionId <= 0) ||
            changes.Select(change => change.PermissionId).Distinct().Count() != changes.Count)
            throw new ArgumentException("A permission update batch contains invalid or duplicate permission IDs.", nameof(changes));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        var role = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == roleId && x.Active, ct)
            ?? throw new KeyNotFoundException("Active role was not found.");
        if (role.Code == RoleAuthorizationPolicy.SuperAdmin && changes.Any(change => change.Granted))
            throw new UnauthorizedAccessException("Super Admin permissions are immutable; privileged permission changes require a separately reviewed two-person workflow.");

        var now = clock.GetUtcNow();
        var currentIds = await db.AdministrationRolePermissions
            .Where(x => x.RoleId == role.Id && x.Active && x.EffectiveStatus == "ACTIVE" && x.ValidFrom.HasValue &&
                         x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now))
            .Select(x => x.PermissionId)
            .ToListAsync(ct);
        if (!currentIds.ToHashSet().SetEquals(expectedActivePermissionIds))
            throw new InvalidOperationException("Role permissions changed since they were loaded. Reload the role before applying changes.");

        var permissions = await db.AdministrationPermissions
            .Where(x => x.Active && changes.Select(change => change.PermissionId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        if (permissions.Count != changes.Count)
            throw new KeyNotFoundException("One or more active permissions were not found.");
        foreach (var change in changes.Where(change => change.Granted))
        {
            var permission = permissions[change.PermissionId];
            EnsureRolePermissionCeiling(role, permission);
            EnsureDirectPermissionGrantAllowed(role, permission);
        }
        foreach (var change in changes.Where(change => !change.Granted))
            RoleAuthorizationPolicy.EnsurePermissionRevocationAllowed(role.Code, permissions[change.PermissionId].Code);
        reason = Required(reason, nameof(reason), 1000);

        foreach (var change in changes)
        {
            var permission = permissions[change.PermissionId];
            var current = currentIds.Contains(change.PermissionId);
            if (current == change.Granted)
                throw new ArgumentException("Every batch change must differ from the current effective permission snapshot.", nameof(changes));

            if (change.Granted)
            {
                var row = new AdministrationRolePermissionRow
                {
                    Id = await NextIdAsync("PA_TICKETS_ROLE_PERMISSION_SEQ", ct),
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    ValidFrom = now,
                    EffectiveStatus = "ACTIVE",
                    Active = true,
                    UpdatedAt = now,
                    UpdatedBy = actor
                };
                db.AdministrationRolePermissions.Add(row);
                await AddAuditAsync($"ROLE:{role.Id}", "ROLE_PERMISSION_GRANTED", actor,
                    $"{role.Code}:{permission.Code}:granted;validFrom={now:O}", ct, reason, null);
            }
            else
            {
                var row = await db.AdministrationRolePermissions
                    .Where(x => x.RoleId == role.Id && x.PermissionId == permission.Id && x.Active && x.EffectiveStatus == "ACTIVE" && x.ValidFrom.HasValue &&
                         x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now))
                    .OrderByDescending(x => x.ValidFrom)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException("The permission grant changed during the batch update.");
                var before = $"{role.Code}:{permission.Code}:granted;validFrom={row.ValidFrom:O};validTo={row.ValidTo:O}";
                row.Active = false;
                row.ValidTo = now > row.ValidFrom!.Value ? now : row.ValidFrom!.Value.AddMilliseconds(1);
                row.UpdatedAt = now;
                row.UpdatedBy = actor;
                await AddAuditAsync($"ROLE:{role.Id}", "ROLE_PERMISSION_REVOKED", actor,
                    $"{role.Code}:{permission.Code}:revoked;validFrom={row.ValidFrom:O};validTo={row.ValidTo:O}",
                    ct, reason, before);
            }
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<RbacProposal> CreateSensitivePermissionGrantProposalAsync(
        long actor,
        long roleId,
        long permissionId,
        IReadOnlyCollection<long> expectedActivePermissionIds,
        string reason,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(expectedActivePermissionIds);
        if (expectedActivePermissionIds.Count > 5000 ||
            expectedActivePermissionIds.Any(id => id <= 0) ||
            expectedActivePermissionIds.Distinct().Count() != expectedActivePermissionIds.Count)
            throw new ArgumentException("The expected permission snapshot is invalid.", nameof(expectedActivePermissionIds));
        reason = Required(reason, nameof(reason), 1000);
        idempotencyKey = Required(idempotencyKey, nameof(idempotencyKey), 100);
        if (idempotencyKey.Length < 16 ||
            idempotencyKey.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.' or ':')))
            throw new ArgumentException("The idempotency key is invalid.", nameof(idempotencyKey));
        correlationId = Required(correlationId, nameof(correlationId), 100);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await EnsureApprovalAuthorityAsync(actor, ct);
            var duplicate = await db.RbacProposals.SingleOrDefaultAsync(
                x => x.InitiatorUserId == actor && x.IdempotencyKey == idempotencyKey, ct);
            if (duplicate is not null)
            {
                var priorPayload = DeserializeProposalPayload(duplicate);
                if (duplicate.TargetRoleId != roleId || duplicate.TargetPermissionId != permissionId ||
                    duplicate.Reason != reason ||
                    !priorPayload.ExpectedActivePermissionIds.Order().SequenceEqual(expectedActivePermissionIds.Order()))
                    throw new InvalidOperationException("The idempotency key was already used for a different proposal.");
                await tx.CommitAsync(ct);
                return MapProposal(duplicate, actor, true);
            }
            var role = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == roleId && x.Active, ct)
                ?? throw new KeyNotFoundException("Active role was not found.");
            var permission = await db.AdministrationPermissions.SingleOrDefaultAsync(x => x.Id == permissionId && x.Active, ct)
                ?? throw new KeyNotFoundException("Active permission was not found.");
            if (!SensitiveRbacApprovalPolicy.CanCreatePermissionProposal(
                    role.Code, role.SystemRole, permission.SecurityClass))
                throw new UnauthorizedAccessException("Super Admin permissions are immutable.");
            EnsureRolePermissionCeiling(role, permission);

            var now = clock.GetUtcNow();
            var proposalId = Guid.NewGuid().ToString("N");
            var expiresAt = now.AddHours(24);
            var currentIds = await db.AdministrationRolePermissions
                .Where(x => x.RoleId == role.Id && x.Active && x.EffectiveStatus == "ACTIVE" && x.ValidFrom.HasValue &&
                         x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now))
                .Select(x => x.PermissionId)
                .ToListAsync(ct);
            if (!SensitiveRbacApprovalPolicy.HasCurrentPermissionSnapshot(
                    expectedActivePermissionIds, currentIds, permissionId))
                throw new InvalidOperationException(
                    "Role permissions changed or the requested permission is already granted. Reload the role before proposing a grant.");

            var payload = BuildPermissionGrantPayload(
                role, permission, expectedActivePermissionIds, proposalId, actor, now, expiresAt,
                reason, idempotencyKey, correlationId);
            var payloadJson = SensitiveRbacApprovalPolicy.SerializePermissionGrantPayload(payload);
            var payloadHash = SensitiveRbacApprovalPolicy.ComputePayloadHash(payloadJson);
            var proposal = new RbacProposalRow
            {
                Id = proposalId,
                OperationType = SensitiveRbacApprovalPolicy.PermissionGrantOperation,
                Status = SensitiveRbacApprovalPolicy.Pending,
                TargetRoleId = role.Id,
                TargetPermissionId = permission.Id,
                InitiatorUserId = actor,
                CreatedAt = now,
                ExpiresAt = expiresAt,
                Reason = reason,
                IdempotencyKey = idempotencyKey,
                PayloadJson = payloadJson,
                PayloadSha256 = payloadHash,
                CorrelationId = correlationId,
                RowVersion = 1
            };
            db.RbacProposals.Add(proposal);
            await AddAuditAsync($"RBAC_PROPOSAL:{proposal.Id}", "RBAC_PROPOSAL_CREATED", actor,
                $"{proposal.OperationType}:{role.Code}:{permission.Code}:PENDING;payloadSha256={payloadHash};correlationId={correlationId}",
                ct, reason, $"effectivePermissions={string.Join(',', expectedActivePermissionIds.Order())}");
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, true);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            await tx.DisposeAsync();
            db.ChangeTracker.Clear();
            var duplicate = await db.RbacProposals.AsNoTracking().SingleOrDefaultAsync(
                x => x.InitiatorUserId == actor && x.IdempotencyKey == idempotencyKey, ct);
            if (duplicate is null || duplicate.TargetRoleId != roleId || duplicate.TargetPermissionId != permissionId ||
                duplicate.Reason != reason ||
                !SensitiveRbacApprovalPolicy.PayloadHashMatches(duplicate.PayloadJson, duplicate.PayloadSha256))
                throw;
            var payload = DeserializeProposalPayload(duplicate);
            if (payload.ExpectedActivePermissionIds.Order().SequenceEqual(expectedActivePermissionIds.Order()))
                return MapProposal(duplicate, actor, true);
            throw new InvalidOperationException("The idempotency key was already used for a different proposal.");
        }
    }

    public async Task<IReadOnlyCollection<RbacProposal>> ListRbacProposalsAsync(long actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var userActive = await db.AdministrationUsers.AsNoTracking().AnyAsync(x => x.Id == actor && x.Active, ct);
        if (!userActive)
            throw new UnauthorizedAccessException("The authenticated user is not authorized for this operation.");
        var canReview = await HasApprovalAuthorityAsync(actor, ct);
        var now = clock.GetUtcNow();
        var expired = await db.RbacProposals
            .Where(x => x.Status == SensitiveRbacApprovalPolicy.Pending && x.ExpiresAt <= now &&
                        (canReview || x.InitiatorUserId == actor))
            .OrderBy(x => x.ExpiresAt)
            .Take(50)
            .ToListAsync(ct);
        foreach (var proposal in expired)
            await ExpireProposalIfNeededAsync(proposal, actor, now, ct);
        var rows = await db.RbacProposals.AsNoTracking()
            .Where(x => canReview || x.InitiatorUserId == actor)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(ct);
        var result = rows.Select(row => MapProposal(row, actor, canReview)).ToArray();
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<RbacProposal> GetRbacProposalAsync(long actor, string proposalId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await FindProposalAsync(proposalId, ct);
        if (!await db.AdministrationUsers.AsNoTracking().AnyAsync(x => x.Id == actor && x.Active, ct))
            throw new KeyNotFoundException("Proposal was not found.");
        var canReview = await HasApprovalAuthorityAsync(actor, ct);
        if (!canReview && row.InitiatorUserId != actor)
            throw new KeyNotFoundException("Proposal was not found.");
        var result = MapProposal(row, actor, canReview);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<RbacProposal> ApproveRbacProposalAsync(
        long actor, string proposalId, int expectedVersion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await EnsureApprovalAuthorityAsync(actor, ct);
        var proposal = await FindProposalAsync(proposalId, ct, tracking: true);
        if (SensitiveRbacApprovalPolicy.CanReturnAppliedRetry(
                proposal.Status, proposal.ApproverUserId, actor))
        {
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, true);
        }
        await EnsureIndependentDecisionMakerAsync(proposal, actor, ct);
        EnsureExpectedProposalVersion(proposal, expectedVersion);
        var now = clock.GetUtcNow();
        if (await ExpireProposalIfNeededAsync(proposal, actor, now, ct))
        {
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, true);
        }
        EnsurePendingProposal(proposal);
        var payload = DeserializeProposalPayload(proposal);
        var role = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == proposal.TargetRoleId && x.Active, ct);
        var permission = await db.AdministrationPermissions.SingleOrDefaultAsync(x => x.Id == proposal.TargetPermissionId && x.Active, ct);
        var currentIds = role is null ? [] : await db.AdministrationRolePermissions
            .Where(x => x.RoleId == role.Id && x.Active && x.EffectiveStatus == "ACTIVE" && x.ValidFrom.HasValue &&
                         x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now))
            .Select(x => x.PermissionId)
            .ToListAsync(ct);
        var snapshotMatches = role is not null && permission is not null &&
            SensitiveRbacApprovalPolicy.PayloadHashMatches(proposal.PayloadJson, proposal.PayloadSha256) &&
            proposal.OperationType == SensitiveRbacApprovalPolicy.PermissionGrantOperation &&
            payload.TargetRoleId == role.Id && payload.TargetPermissionId == permission.Id &&
            SensitiveRbacApprovalPolicy.HasCurrentPermissionSnapshot(
                payload.ExpectedActivePermissionIds, currentIds, permission.Id) &&
            SensitiveRbacApprovalPolicy.SerializePermissionGrantPayload(
                BuildPermissionGrantPayload(role, permission, payload.ExpectedActivePermissionIds,
                    proposal.Id, proposal.InitiatorUserId, proposal.CreatedAt, proposal.ExpiresAt,
                    proposal.Reason, proposal.IdempotencyKey, proposal.CorrelationId)) == proposal.PayloadJson;
        if (!snapshotMatches)
        {
            TransitionProposal(proposal, SensitiveRbacApprovalPolicy.Rejected);
            proposal.ApproverUserId = actor;
            proposal.DecisionAt = now;
            proposal.DecisionReason = "The proposal snapshot or permission policy changed before approval.";
            proposal.ApplicationOutcome = "SNAPSHOT_MISMATCH";
            await AddAuditAsync($"RBAC_PROPOSAL:{proposal.Id}", "RBAC_PROPOSAL_SNAPSHOT_MISMATCH", actor,
                $"{proposal.Status}:SNAPSHOT_MISMATCH;correlationId={proposal.CorrelationId}", ct,
                proposal.DecisionReason, "PENDING");
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, true);
        }

        EnsureRolePermissionCeiling(role!, permission!);
        if (!SensitiveRbacApprovalPolicy.CanCreatePermissionProposal(
                role!.Code, role.SystemRole, permission!.SecurityClass))
            throw new UnauthorizedAccessException("The proposal is outside the supported independent-approval operation set.");
        TransitionProposal(proposal, SensitiveRbacApprovalPolicy.Approved);
        proposal.ApproverUserId = actor;
        proposal.DecisionAt = now;
        proposal.DecisionReason = "Approved by an independent active Super Admin.";
        await AddAuditAsync($"RBAC_PROPOSAL:{proposal.Id}", "RBAC_PROPOSAL_APPROVED", actor,
            $"{proposal.OperationType}:{proposal.Status};correlationId={proposal.CorrelationId}", ct,
            proposal.DecisionReason, "PENDING");

        db.AdministrationRolePermissions.Add(new AdministrationRolePermissionRow
        {
            Id = await NextIdAsync("PA_TICKETS_ROLE_PERMISSION_SEQ", ct),
            RoleId = role!.Id,
            PermissionId = permission!.Id,
            ValidFrom = now,
            EffectiveStatus = "ACTIVE",
            Active = true,
            UpdatedAt = now,
            UpdatedBy = actor
        });
        await AddAuditAsync($"ROLE:{role.Id}", "ROLE_PERMISSION_GRANTED_BY_APPROVAL", actor,
            $"{role.Code}:{permission.Code}:granted;proposalId={proposal.Id};validFrom={now:O}",
            ct, proposal.Reason, null);

        TransitionProposal(proposal, SensitiveRbacApprovalPolicy.Applied);
        proposal.ApplicationOutcome = "GRANTED";
        await AddAuditAsync($"RBAC_PROPOSAL:{proposal.Id}", "RBAC_PROPOSAL_APPLIED", actor,
            $"{proposal.OperationType}:{proposal.Status}:GRANTED;correlationId={proposal.CorrelationId}", ct,
            proposal.Reason, SensitiveRbacApprovalPolicy.Approved);
        await tx.CommitAsync(ct);
        return MapProposal(proposal, actor, true);
    }

    public async Task<RbacProposal> RejectRbacProposalAsync(
        long actor, string proposalId, int expectedVersion, string reason, CancellationToken ct)
    {
        reason = Required(reason, nameof(reason), 1000);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await EnsureApprovalAuthorityAsync(actor, ct);
        var proposal = await FindProposalAsync(proposalId, ct, tracking: true);
        if (SensitiveRbacApprovalPolicy.CanReturnRejectedRetry(
                proposal.Status, proposal.ApproverUserId, proposal.DecisionReason, actor, reason))
        {
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, true);
        }
        await EnsureIndependentDecisionMakerAsync(proposal, actor, ct);
        EnsureExpectedProposalVersion(proposal, expectedVersion);
        var now = clock.GetUtcNow();
        if (await ExpireProposalIfNeededAsync(proposal, actor, now, ct))
        {
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, true);
        }
        EnsurePendingProposal(proposal);
        TransitionProposal(proposal, SensitiveRbacApprovalPolicy.Rejected);
        proposal.ApproverUserId = actor;
        proposal.DecisionAt = now;
        proposal.DecisionReason = reason;
        proposal.ApplicationOutcome = "REJECTED";
        await AddAuditAsync($"RBAC_PROPOSAL:{proposal.Id}", "RBAC_PROPOSAL_REJECTED", actor,
            $"{proposal.OperationType}:{proposal.Status};correlationId={proposal.CorrelationId}", ct, reason, "PENDING");
        await tx.CommitAsync(ct);
        return MapProposal(proposal, actor, true);
    }

    public async Task<RbacProposal> CancelRbacProposalAsync(
        long actor, string proposalId, int expectedVersion, string reason, CancellationToken ct)
    {
        reason = Required(reason, nameof(reason), 1000);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var proposal = await FindProposalAsync(proposalId, ct, tracking: true);
        if (!await db.AdministrationUsers.AnyAsync(x => x.Id == actor && x.Active, ct) ||
            proposal.InitiatorUserId != actor)
            throw new KeyNotFoundException("Proposal was not found.");
        if (SensitiveRbacApprovalPolicy.CanReturnCancelledRetry(
                proposal.Status, proposal.CancelledByUserId, proposal.DecisionReason, actor, reason))
        {
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, false);
        }
        EnsureExpectedProposalVersion(proposal, expectedVersion);
        var now = clock.GetUtcNow();
        if (await ExpireProposalIfNeededAsync(proposal, actor, now, ct))
        {
            await tx.CommitAsync(ct);
            return MapProposal(proposal, actor, false);
        }
        EnsurePendingProposal(proposal);
        TransitionProposal(proposal, SensitiveRbacApprovalPolicy.Cancelled);
        proposal.CancelledByUserId = actor;
        proposal.DecisionAt = now;
        proposal.DecisionReason = reason;
        proposal.ApplicationOutcome = "CANCELLED";
        await AddAuditAsync($"RBAC_PROPOSAL:{proposal.Id}", "RBAC_PROPOSAL_CANCELLED", actor,
            $"{proposal.OperationType}:{proposal.Status};correlationId={proposal.CorrelationId}", ct, reason, "PENDING");
        await tx.CommitAsync(ct);
        return MapProposal(proposal, actor, false);
    }

    public async Task<IReadOnlyCollection<AdministrationMembership>> ListMembershipsAsync(long actor, long userId, CancellationToken ct)
    {
        var target = await targetReader.ReadActiveAsync(userId, ct)
            ?? throw new KeyNotFoundException("Active user was not found.");
        await authorization.EnsurePermissionAsync(actor, "ROLE_ADMIN", new AuthorizationResource(
            DepartmentId: target.DepartmentId, TeamId: target.TeamId, AssignedUserId: target.Id,
            ResourceType: "USER", ResourceId: target.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), ct);
        return await db.AdministrationUserRoles.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ValidFrom)
            .Select(x => new AdministrationMembership(x.Id, x.UserId, x.RoleId, x.ValidFrom, x.ValidTo, x.Active))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<AdministrationRoleScope>> ListRoleScopesAsync(long actor, long membershipId, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_SCOPE_ADMIN", null, ct);
        if (!await db.AdministrationUserRoles.AnyAsync(x => x.Id == membershipId, ct))
            throw new KeyNotFoundException("Role membership was not found.");
        return await db.AdministrationUserRoleScopes.AsNoTracking().Where(x => x.UserRoleId == membershipId)
            .OrderBy(x => x.ValidFrom)
            .Select(x => new AdministrationRoleScope(x.Id, x.UserRoleId, x.ScopeType, x.OrganizationId, x.DepartmentId, x.TeamId,
                x.ClientId, x.ResourceType, x.ResourceId, x.ValidFrom, x.ValidTo, x.Active))
            .ToArrayAsync(ct);
    }

    public async Task<AdministrationRoleScope> AssignRoleScopeAsync(
        long actor,
        long membershipId,
        string scopeType,
        long? organizationId,
        long? departmentId,
        long? teamId,
        long? clientId,
        string? resourceType,
        string? resourceId,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        string reason,
        CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_SCOPE_ADMIN", null, ct);
        throw new InvalidOperationException(
            "Role scope grants require pending independent approval, which is not yet available.");
    }

    public async Task RemoveRoleScopeAsync(long actor, long scopeId, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_SCOPE_ADMIN", null, ct);
        reason = Required(reason, nameof(reason), 1000);
        var row = await db.AdministrationUserRoleScopes.SingleOrDefaultAsync(x => x.Id == scopeId, ct)
            ?? throw new KeyNotFoundException("Role scope was not found.");
        if (row.Active)
        {
            var before = $"{row.UserRoleId}:{row.ScopeType}:{row.OrganizationId}:{row.DepartmentId}:{row.TeamId}:{row.ClientId}:{row.ResourceType}:{row.ResourceId}:active;validFrom={row.ValidFrom:O};validTo={row.ValidTo:O}";
            row.Active = false;
            var now = clock.GetUtcNow();
            if (now > row.ValidFrom && (!row.ValidTo.HasValue || now < row.ValidTo.Value))
                row.ValidTo = now;
            await AddAuditAsync($"ROLE_SCOPE:{row.Id}", "ROLE_SCOPE_REMOVED", actor,
                $"{row.UserRoleId}:{row.ScopeType}:{row.OrganizationId}:{row.DepartmentId}:{row.TeamId}:{row.ClientId}:{row.ResourceType}:{row.ResourceId}:inactive;validFrom={row.ValidFrom:O};validTo={row.ValidTo:O}",
                ct, reason, before);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public Task<AdministrationMembership> AssignRoleAsync(long actor, long userId, long roleId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct) =>
        retry.ExecuteAsync(db, () => AssignRoleOnceAsync(actor, userId, roleId, validFrom, validTo, ct), ct);

    private async Task<AdministrationMembership> AssignRoleOnceAsync(long actor, long userId, long roleId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        ValidateDates(validFrom, validTo);
        var target = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId && x.Active, ct);
        var role = await db.AdministrationRoles.SingleOrDefaultAsync(x => x.Id == roleId && x.Active, ct);
        if (target is null || role is null) throw new KeyNotFoundException("The user or role was not found or inactive.");
        await authorization.EnsurePermissionAsync(actor, "ROLE_ADMIN", new AuthorizationResource(
            DepartmentId: target.DepartmentId, TeamId: target.TeamId, AssignedUserId: target.Id,
            ResourceType: "USER", ResourceId: target.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), ct);
        if (target.Id == actor)
            throw new UnauthorizedAccessException("Users cannot assign roles to themselves.");
        if (role.Code == "ADMINISTRATOR")
            throw new UnauthorizedAccessException("The legacy administrator role can only be changed through an approved, audited transition.");
        if (await HasAdministratorRoleAsync(target.Id, ct))
            throw new UnauthorizedAccessException("Role memberships for administrator accounts cannot be changed through ordinary role administration.");
        var actorRoles = await ActiveRoleCodesAsync(actor, ct);
        if (role.Code == RoleAuthorizationPolicy.SuperAdmin)
            throw new UnauthorizedAccessException("Super Admin membership grants require the separately reviewed bootstrap or a two-person privileged-role workflow.");
        if (role.Code == RoleAuthorizationPolicy.AdminHead && !actorRoles.Contains(RoleAuthorizationPolicy.SuperAdmin, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("Only a Super Admin may grant privileged administrative roles.");
        if (role.Code == RoleAuthorizationPolicy.Admin &&
            !actorRoles.Contains(RoleAuthorizationPolicy.SuperAdmin, StringComparer.Ordinal) &&
            !(actorRoles.Contains(RoleAuthorizationPolicy.AdminHead, StringComparer.Ordinal) &&
              await HasCurrentAdminDelegationAsync(actor, target.Id, ct)))
            throw new UnauthorizedAccessException("Only a Super Admin or the assigned Admin Head may assign Admin memberships.");
        if (role.Code is RoleAuthorizationPolicy.Admin or RoleAuthorizationPolicy.AdminHead)
            throw new InvalidOperationException(
                "Administrative role grants require pending independent approval, which is not yet available.");
        var isBuiltIn = role.Code is "USER" or "SPECTATOR" or "ADMIN" or "ADMIN_HEAD" or "SUPER_ADMIN";
        if (role.SystemRole != isBuiltIn)
            throw new UnauthorizedAccessException("Built-in role identities are protected from being represented as custom roles, and vice versa.");
        if (!role.SystemRole)
        {
            EnsureValidCustomRoleCode(role.Code);
            if (!actorRoles.Contains(RoleAuthorizationPolicy.SuperAdmin, StringComparer.Ordinal))
                throw new UnauthorizedAccessException("Only a Super Admin may assign custom roles.");
        }
        else if (!isBuiltIn)
            throw new UnauthorizedAccessException("This role is retained for compatibility and cannot be assigned through ordinary role administration.");
        if (await db.AdministrationUserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == roleId && x.Active &&
                (x.ValidTo == null || x.ValidTo > validFrom) && (!validTo.HasValue || x.ValidFrom < validTo.Value), ct))
            throw new InvalidOperationException("An effective role membership already exists for this period.");
        var overlappingCodes = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                                      join existingRole in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals existingRole.Id
                                      where membership.UserId == userId && membership.Active && existingRole.Active &&
                                            (membership.ValidTo == null || membership.ValidTo > validFrom) &&
                                            (!validTo.HasValue || membership.ValidFrom < validTo.Value)
                                      select existingRole.Code).ToListAsync(ct);
        if (role.Code == RoleAuthorizationPolicy.SuperAdmin && overlappingCodes.Count > 0 ||
            overlappingCodes.Contains(RoleAuthorizationPolicy.SuperAdmin, StringComparer.Ordinal) ||
            (role.Code == RoleAuthorizationPolicy.Admin && overlappingCodes.Contains(RoleAuthorizationPolicy.AdminHead, StringComparer.Ordinal)) ||
            (role.Code == RoleAuthorizationPolicy.AdminHead && overlappingCodes.Contains(RoleAuthorizationPolicy.Admin, StringComparer.Ordinal)))
            throw new InvalidOperationException("This role combination violates the protected role-combination policy.");
        var row = new AdministrationUserRoleRow { Id = await NextIdAsync("PA_TICKETS_USER_ROLE_SEQ", ct), UserId = userId, RoleId = roleId, ValidFrom = validFrom, ValidTo = validTo, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationUserRoles.Add(row);
        await AddDefaultRoleScopesAsync(row, target, role.Code, actor, ct);
        await AddAuditAsync(row.Id.ToString(), "USER_ROLE_ASSIGNED", actor, $"{userId}:{roleId}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(row.Id, row.UserId, row.RoleId, row.ValidFrom, row.ValidTo, row.Active);
    }

    private async Task AddDefaultRoleScopesAsync(
        AdministrationUserRoleRow membership,
        AdministrationUserRow target,
        string roleCode,
        long actor,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var scopes = new List<(string Type, long? DepartmentId, long? TeamId)>();
        if (roleCode is RoleAuthorizationPolicy.Admin or RoleAuthorizationPolicy.AdminHead)
        {
            if (target.DepartmentId.HasValue)
                scopes.Add(("DEPARTMENT", target.DepartmentId, null));
            if (roleCode == RoleAuthorizationPolicy.AdminHead)
                scopes.Add(("ADMIN_GROUP", null, null));
        }
        else if (roleCode is RoleAuthorizationPolicy.User or RoleAuthorizationPolicy.Spectator)
        {
            if (target.DepartmentId.HasValue && target.TeamId.HasValue)
                scopes.Add(("TEAM", target.DepartmentId, target.TeamId));
            else if (target.DepartmentId.HasValue)
                scopes.Add(("DEPARTMENT", target.DepartmentId, null));
        }

        foreach (var scope in scopes)
        {
            var row = new AdministrationUserRoleScopeRow
            {
                Id = await NextIdAsync("PA_TICKETS_ROLE_SCOPE_SEQ", ct),
                UserRoleId = membership.Id,
                ScopeType = scope.Type,
                DepartmentId = scope.DepartmentId,
                TeamId = scope.TeamId,
                ValidFrom = membership.ValidFrom,
                ValidTo = membership.ValidTo,
                Active = true,
                CreatedAt = now,
                CreatedBy = actor
            };
            db.AdministrationUserRoleScopes.Add(row);
            await AddAuditAsync($"ROLE_SCOPE:{row.Id}", "ROLE_SCOPE_ASSIGNED", actor,
                $"{membership.Id}:{scope.Type}:{scope.DepartmentId}:{scope.TeamId}:{membership.ValidFrom:O}:{membership.ValidTo:O}",
                ct, "Default scope created with role assignment");
        }
    }

    public async Task<IReadOnlyCollection<AdminHeadDelegation>> ListAdminDelegationsAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_SCOPE_ADMIN", null, ct);
        return await db.AdminHeadDelegations.AsNoTracking()
            .OrderByDescending(x => x.ValidFrom)
            .Select(x => new AdminHeadDelegation(x.Id, x.AdminHeadUserId, x.AdminUserId, x.ValidFrom, x.ValidTo, x.Active))
            .ToArrayAsync(ct);
    }

    public async Task<AdminHeadDelegation> AssignAdminDelegationAsync(
        long actor,
        long adminHeadUserId,
        long adminUserId,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        string reason,
        CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_SCOPE_ADMIN", null, ct);
        throw new InvalidOperationException(
            "Admin Head delegation changes require pending independent approval, which is not yet available.");
    }

    public async Task RemoveAdminDelegationAsync(long actor, long delegationId, string reason, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_SCOPE_ADMIN", null, ct);
        throw new InvalidOperationException(
            "Admin Head delegation changes require pending independent approval, which is not yet available.");
    }

    public Task<AdministrationClientMembership> AssignClientAsync(long actor, long userId, long clientId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct) =>
        retry.ExecuteAsync(db, () => AssignClientOnceAsync(actor, userId, clientId, validFrom, validTo, ct), ct);

    private async Task<AdministrationClientMembership> AssignClientOnceAsync(long actor, long userId, long clientId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "CLIENT_ADMIN", null, ct); ValidateDates(validFrom, validTo);
        var target = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId && x.Active, ct);
        if (target is null) throw new KeyNotFoundException("The user was not found or inactive.");
        if (await db.AdministrationUserClients.AnyAsync(x => x.UserId == userId && x.ClientId == clientId && x.Active &&
                (x.ValidTo == null || x.ValidTo > validFrom) && (!validTo.HasValue || x.ValidFrom < validTo.Value), ct))
            throw new InvalidOperationException("An effective client membership already exists for this period.");
        var row = new AdministrationUserClientRow { Id = await NextIdAsync("PA_TICKETS_USER_CLIENT_SEQ", ct), UserId = userId, ClientId = clientId, ValidFrom = validFrom, ValidTo = validTo, Active = true, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
        db.AdministrationUserClients.Add(row); await AddAuditAsync(row.Id.ToString(), "USER_CLIENT_ASSIGNED", actor, $"{userId}:{clientId}", ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(row.Id, row.UserId, row.ClientId, row.ValidFrom, row.ValidTo, row.Active);
    }

    public async Task RemoveRoleAsync(long actor, long membershipId, CancellationToken ct) => await RemoveRoleInternalAsync(actor, membershipId, ct);
    private async Task RemoveRoleInternalAsync(long actor, long membershipId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.AdministrationUserRoles.SingleOrDefaultAsync(x => x.Id == membershipId, ct)
            ?? throw new KeyNotFoundException("Role membership was not found.");
        var target = await db.AdministrationUsers.SingleAsync(x => x.Id == row.UserId, ct);
        await authorization.EnsurePermissionAsync(actor, "ROLE_ADMIN", new AuthorizationResource(
            DepartmentId: target.DepartmentId, TeamId: target.TeamId, AssignedUserId: target.Id,
            ResourceType: "USER", ResourceId: target.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), ct);
        if (target.Id == actor)
            throw new UnauthorizedAccessException("Users cannot change their own role memberships.");
        if (await HasAdministratorRoleAsync(target.Id, ct))
            throw new UnauthorizedAccessException("Role memberships for administrator accounts cannot be changed through ordinary role administration.");
        var targetRole = await db.AdministrationRoles.AsNoTracking().Where(x => x.Id == row.RoleId)
            .Select(x => new { x.Code, x.SystemRole }).SingleAsync(ct);
        var targetRoleCode = targetRole.Code;
        var actorRoles = await ActiveRoleCodesAsync(actor, ct);
        if (!targetRole.SystemRole &&
            !actorRoles.Contains(RoleAuthorizationPolicy.SuperAdmin, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("Only a Super Admin may remove a custom-role membership.");
        if (targetRoleCode is "ADMIN_HEAD" or "SUPER_ADMIN" &&
            !actorRoles.Contains(RoleAuthorizationPolicy.SuperAdmin, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("Only a Super Admin may remove this privileged membership.");
        if (targetRoleCode == "ADMIN" &&
            !actorRoles.Contains(RoleAuthorizationPolicy.SuperAdmin, StringComparer.Ordinal) &&
            !(actorRoles.Contains(RoleAuthorizationPolicy.AdminHead, StringComparer.Ordinal) &&
              await HasCurrentAdminDelegationAsync(actor, target.Id, ct)))
            throw new UnauthorizedAccessException("Only a Super Admin or the assigned Admin Head may remove this Admin membership.");
        var now = clock.GetUtcNow();
        var membershipBefore = $"{row.UserId}:{targetRoleCode}:active;validFrom={row.ValidFrom:O};validTo={row.ValidTo:O}";
        row.Active = false;
        if (now > row.ValidFrom && (!row.ValidTo.HasValue || now < row.ValidTo.Value))
            row.ValidTo = now;
        await AddAuditAsync(row.Id.ToString(), "USER_ROLE_REMOVED", actor,
            $"{row.UserId}:{targetRoleCode}:inactive;validFrom={row.ValidFrom:O};validTo={row.ValidTo:O}",
            ct, "Role membership revoked", membershipBefore);
        var scopes = await db.AdministrationUserRoleScopes
            .Where(x => x.UserRoleId == row.Id && x.Active).ToListAsync(ct);
        foreach (var scope in scopes)
        {
            var scopeBefore = $"{row.Id}:{scope.ScopeType}:{scope.DepartmentId}:{scope.TeamId}:active;validFrom={scope.ValidFrom:O};validTo={scope.ValidTo:O}";
            scope.Active = false;
            if (now > scope.ValidFrom && (!scope.ValidTo.HasValue || now < scope.ValidTo.Value))
                scope.ValidTo = now;
            await AddAuditAsync($"ROLE_SCOPE:{scope.Id}", "ROLE_SCOPE_REMOVED_WITH_MEMBERSHIP", actor,
                $"{row.Id}:{scope.ScopeType}:{scope.DepartmentId}:{scope.TeamId}:inactive;validFrom={scope.ValidFrom:O};validTo={scope.ValidTo:O}",
                ct, "Parent role membership revoked", scopeBefore);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RemoveClientAsync(long actor, long membershipId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorization.EnsurePermissionAsync(actor, "CLIENT_ADMIN", null, ct);
        var row = await db.AdministrationUserClients.SingleOrDefaultAsync(x => x.Id == membershipId, ct)
            ?? throw new KeyNotFoundException("Client membership was not found.");
        row.Active = false;
        var now = clock.GetUtcNow();
        if (now > row.ValidFrom) row.ValidTo = now;
        await AddAuditAsync(row.Id.ToString(), "USER_CLIENT_REMOVED", actor, null, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<long> NextIdAsync(string sequence, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try { await using var command = db.Database.GetDbConnection().CreateCommand(); var quotedSequence = "\"" + sequence + "\""; command.CommandText = capabilities.Kind == DatabaseProviderKind.PostgreSql ? $"select nextval('{quotedSequence}'::regclass)" : $"select {sequence}.NEXTVAL from DUAL"; return Convert.ToInt64(await command.ExecuteScalarAsync(ct)); }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private async Task<long> NextUnusedDelegationIdAsync(CancellationToken ct)
    {
        long id;
        do
        {
            id = await NextIdAsync("PA_TICKETS_ADMIN_DELEGATION_SEQ", ct);
        }
        while (await db.AdminHeadDelegations.AnyAsync(x => x.Id == id, ct));

        return id;
    }

    private static string DelegationAuditId(long delegationId) =>
        $"ADMIN_HEAD_DELEGATION:{delegationId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private async Task AddAuditAsync(
        string id,
        string action,
        long actor,
        string? value,
        CancellationToken ct,
        string? reason = null,
        string? oldValue = null)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, "ADMINISTRATION", id, ct);
        var now = clock.GetUtcNow();
        var previous = await db.Audits.Where(x => x.EntityType == "ADMINISTRATION" && x.EntityId == id)
            .OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var canonical = System.Text.Json.JsonSerializer.Serialize(new
        {
            entityType = "ADMINISTRATION",
            entityId = id,
            action,
            actorUserId = actor,
            auditAtUtc = now.UtcDateTime,
            oldValue,
            newValue = value,
            reason,
            previousHash = previous
        });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow
        {
            EntityType = "ADMINISTRATION",
            EntityId = id,
            Action = action,
            ActorId = actor,
            At = now,
            OldValue = oldValue,
            NewValue = value,
            Reason = reason,
            Hash = hash,
            PreviousHash = previous
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }

    private async Task AddSensitiveUserAuditAsync(
        long targetUserId,
        long actorUserId,
        string action,
        IReadOnlyCollection<string> changedFields,
        object before,
        object after,
        DateTimeOffset auditAt,
        string reason,
        CancellationToken ct)
    {
        var entityId = targetUserId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await auditLocks.AcquireAsync(db, "ADMINISTRATION", entityId, ct);
        var previousHash = await db.Audits
            .Where(x => x.EntityType == "ADMINISTRATION" && x.EntityId == entityId)
            .OrderByDescending(x => x.Id)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(ct);
        var preciseAuditAt = auditAt.ToUniversalTime();
        preciseAuditAt = new DateTimeOffset(
            preciseAuditAt.UtcDateTime.Ticks - preciseAuditAt.UtcDateTime.Ticks % 10,
            TimeSpan.Zero);
        var oldValue = JsonSerializer.Serialize(before);
        var newValue = JsonSerializer.Serialize(new { changedFields, values = after });
        var canonical = JsonSerializer.Serialize(new
        {
            entityType = "ADMINISTRATION",
            entityId,
            action,
            actorUserId,
            auditAtUtc = preciseAuditAt.UtcDateTime,
            oldValue,
            newValue,
            reason,
            previousHash
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow
        {
            EntityType = "ADMINISTRATION",
            EntityId = entityId,
            Action = action,
            ActorId = actorUserId,
            At = preciseAuditAt,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            Hash = hash,
            PreviousHash = previousHash
        });
    }

    private async Task EnsureSuperAdminProfileAccessAsync(
        long actor,
        AdministrationUserRow target,
        CancellationToken ct)
    {
        if (!await db.AdministrationUsers.AnyAsync(x => x.Id == actor && x.Active, ct))
            throw new UnauthorizedAccessException("The authenticated Super Admin is inactive.");
        if (target.Id != actor && await HasAdministratorRoleAsync(target.Id, ct))
            throw new UnauthorizedAccessException("A Super Admin may not modify another privileged root account.");
    }

    private async Task EnsureNotOtherAdministratorAsync(
        long actor,
        AdministrationUserRow target,
        CancellationToken ct)
    {
        if (target.Id != actor && await HasAdministratorRoleAsync(target.Id, ct))
            throw new UnauthorizedAccessException("An administrator may not modify another Super Admin account.");
    }

    private Task<bool> IsAdministratorAsync(long userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return (from user in db.AdministrationUsers.AsNoTracking()
                join membership in db.AdministrationUserRoles.AsNoTracking() on user.Id equals membership.UserId
                join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                where user.Id == userId && user.Active && membership.Active &&
                      membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
                      role.Active && role.Code == RoleAuthorizationPolicy.SuperAdmin
                select user.Id).AnyAsync(ct);
    }

    private Task<bool> HasAdministratorRoleAsync(long userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return (from membership in db.AdministrationUserRoles.AsNoTracking()
                join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                where membership.UserId == userId && membership.Active &&
                      membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
                      role.Active && (role.Code == RoleAuthorizationPolicy.LegacyAdministrator ||
                                      role.Code == RoleAuthorizationPolicy.SuperAdmin)
                select membership.UserId).AnyAsync(ct);
    }

    private async Task<HashSet<long>> PrivilegedRoleHolderIdsAsync(IEnumerable<long> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var now = clock.GetUtcNow();
        return (await (from membership in db.AdministrationUserRoles.AsNoTracking()
                       join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                       where ids.Contains(membership.UserId) && membership.Active &&
                             membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
                             role.Active && (role.Code == RoleAuthorizationPolicy.Admin ||
                                             role.Code == RoleAuthorizationPolicy.AdminHead ||
                                             role.Code == RoleAuthorizationPolicy.LegacyAdministrator ||
                                             role.Code == RoleAuthorizationPolicy.SuperAdmin)
                       select membership.UserId).Distinct().ToListAsync(ct)).ToHashSet();
    }

    private async Task EnsureUserAdministrationAsync(long actor, AdministrationUserRow target, CancellationToken ct) =>
        await authorization.EnsurePermissionAsync(actor, "USER_ADMIN", new AuthorizationResource(
            DepartmentId: target.DepartmentId, TeamId: target.TeamId, AssignedUserId: target.Id,
            ResourceType: "USER", ResourceId: target.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), ct);

    private async Task<long> ActiveDepartmentIdAsync(long actor, CancellationToken ct)
    {
        var departmentId = await db.AdministrationUsers.AsNoTracking()
            .Where(x => x.Id == actor && x.Active && x.DepartmentId.HasValue)
            .Select(x => x.DepartmentId)
            .SingleOrDefaultAsync(ct);
        return departmentId ?? throw new UnauthorizedAccessException("Department-scoped administration requires an active department.");
    }

    private async Task<List<string>> ActiveRoleCodesAsync(long userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await (from membership in db.AdministrationUserRoles.AsNoTracking()
                      join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                      where membership.UserId == userId && membership.Active && role.Active &&
                            membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now)
                      select role.Code).Distinct().ToListAsync(ct);
    }

    private async Task<bool> IsEffectiveRoleMembershipAsync(
        long userId,
        string roleCode,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        CancellationToken ct) =>
        await (from membership in db.AdministrationUserRoles.AsNoTracking()
               join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
               join user in db.AdministrationUsers.AsNoTracking() on membership.UserId equals user.Id
               where membership.UserId == userId && user.Active && membership.Active && role.Active && role.Code == roleCode &&
                     membership.ValidFrom <= validFrom &&
                     (membership.ValidTo == null || (validTo.HasValue && membership.ValidTo >= validTo.Value))
               select membership.Id).AnyAsync(ct);

    private async Task<bool> HasCurrentAdminDelegationAsync(long adminHeadUserId, long adminUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.AdminHeadDelegations.AsNoTracking().AnyAsync(x =>
            x.AdminHeadUserId == adminHeadUserId && x.AdminUserId == adminUserId && x.Active &&
            x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now), ct);
    }

    private async Task<List<long>> ActiveDelegatedAdminIdsAsync(long adminHeadUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await (from delegation in db.AdminHeadDelegations.AsNoTracking()
                      join membership in db.AdministrationUserRoles.AsNoTracking() on delegation.AdminUserId equals membership.UserId
                      join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                      where delegation.AdminHeadUserId == adminHeadUserId && delegation.Active &&
                            delegation.ValidFrom <= now && (delegation.ValidTo == null || delegation.ValidTo > now) &&
                            membership.Active && membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now) &&
                            role.Active && role.Code == RoleAuthorizationPolicy.Admin
                      select delegation.AdminUserId).Distinct().ToListAsync(ct);
    }

    private async Task<HashSet<long>> SubordinateIdsAsync(long managerUserId, CancellationToken ct)
    {
        var reportingLinks = await db.AdministrationUsers.AsNoTracking()
            .Select(x => new { x.Id, x.ManagerUserId })
            .ToListAsync(ct);
        var childrenByManager = reportingLinks
            .Where(x => x.ManagerUserId.HasValue)
            .GroupBy(x => x.ManagerUserId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToArray());
        var visible = new HashSet<long> { managerUserId };
        var frontier = new Queue<long>();
        frontier.Enqueue(managerUserId);
        while (frontier.TryDequeue(out var manager))
        {
            if (!childrenByManager.TryGetValue(manager, out var children)) continue;
            foreach (var child in children)
                if (visible.Add(child))
                    frontier.Enqueue(child);
        }
        return visible;
    }

    private async Task EnsureApprovalAuthorityAsync(long actor, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        if (!await HasCurrentSuperAdminMembershipAsync(actor, ct))
            throw new UnauthorizedAccessException("An active Super Admin membership is required for sensitive RBAC approval.");
    }

    private async Task<bool> HasApprovalAuthorityAsync(long actor, CancellationToken ct) =>
        await HasCurrentSuperAdminMembershipAsync(actor, ct) &&
        await authorization.HasPermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);

    private async Task<bool> HasCurrentSuperAdminMembershipAsync(long actor, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await (from user in db.AdministrationUsers.AsNoTracking()
                      join membership in db.AdministrationUserRoles.AsNoTracking() on user.Id equals membership.UserId
                      join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                      where user.Id == actor && user.Active && membership.Active && role.Active &&
                            role.SystemRole && role.Code == RoleAuthorizationPolicy.SuperAdmin &&
                            membership.ValidFrom <= now && (membership.ValidTo == null || membership.ValidTo > now)
                      select membership.Id).AnyAsync(ct);
    }

    private async Task<RbacProposalRow> FindProposalAsync(string proposalId, CancellationToken ct, bool tracking = false)
    {
        if (!Guid.TryParseExact(proposalId, "N", out var parsed))
            throw new KeyNotFoundException("Proposal was not found.");
        var canonicalId = parsed.ToString("N");
        var query = tracking ? db.RbacProposals : db.RbacProposals.AsNoTracking();
        var row = await query.SingleOrDefaultAsync(x => x.Id == canonicalId, ct);
        return row ?? throw new KeyNotFoundException("Proposal was not found.");
    }

    private async Task<bool> ExpireProposalIfNeededAsync(
        RbacProposalRow proposal, long actor, DateTimeOffset now, CancellationToken ct)
    {
        if (!SensitiveRbacApprovalPolicy.IsPendingAndExpired(proposal.Status, proposal.ExpiresAt, now))
            return false;
        TransitionProposal(proposal, SensitiveRbacApprovalPolicy.Expired);
        proposal.DecisionAt = now;
        proposal.DecisionReason = "Automatically expired by lifecycle processing; no human decision was made.";
        proposal.ApplicationOutcome = "EXPIRED";
        await AddAuditAsync($"RBAC_PROPOSAL:{proposal.Id}", "RBAC_PROPOSAL_EXPIRED", actor,
            $"{proposal.OperationType}:{proposal.Status};correlationId={proposal.CorrelationId}", ct,
            proposal.DecisionReason, "PENDING");
        return true;
    }

    private static void EnsureExpectedProposalVersion(RbacProposalRow proposal, int expectedVersion)
    {
        if (!SensitiveRbacApprovalPolicy.HasExpectedVersion(proposal.RowVersion, expectedVersion))
            throw new InvalidOperationException("The proposal changed since it was loaded. Refresh before deciding.");
    }

    private static void EnsurePendingProposal(RbacProposalRow proposal)
    {
        if (proposal.Status != SensitiveRbacApprovalPolicy.Pending)
            throw new InvalidOperationException("The proposal is no longer pending.");
    }

    private async Task EnsureIndependentDecisionMakerAsync(RbacProposalRow proposal, long actor, CancellationToken ct)
    {
        var active = await db.AdministrationUsers.AsNoTracking().AnyAsync(x => x.Id == actor && x.Active, ct);
        var membership = active && await HasCurrentSuperAdminMembershipAsync(actor, ct);
        var permission = membership && await authorization.HasPermissionAsync(actor, "ROLE_PERMISSION_ADMIN", null, ct);
        if (!SensitiveRbacApprovalPolicy.IsIndependentApprover(
                proposal.InitiatorUserId, actor, active, membership, permission))
            throw new UnauthorizedAccessException("The proposal initiator cannot approve or reject their own proposal.");
    }

    private static void TransitionProposal(RbacProposalRow proposal, string next)
    {
        if (!SensitiveRbacApprovalPolicy.CanTransition(proposal.Status, next))
            throw new InvalidOperationException($"The proposal cannot transition from {proposal.Status} to {next}.");
        proposal.Status = next;
        proposal.RowVersion++;
    }

    private static SensitivePermissionGrantPayload BuildPermissionGrantPayload(
        AdministrationRoleRow role,
        AdministrationPermissionRow permission,
        IEnumerable<long> expectedActivePermissionIds,
        string proposalId,
        long initiatorUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string reason,
        string idempotencyKey,
        string correlationId) =>
        new(
            SensitiveRbacApprovalPolicy.PermissionGrantOperation,
            proposalId,
            initiatorUserId,
            createdAt,
            expiresAt,
            reason,
            idempotencyKey,
            correlationId,
            role.Id,
            role.Code,
            role.SystemRole,
            permission.Id,
            permission.Code,
            permission.Name,
            permission.ModuleCode,
            permission.ActionCode,
            permission.ScopeClass.ToUpperInvariant(),
            permission.SecurityClass.ToUpperInvariant(),
            permission.ReadOnly,
            expectedActivePermissionIds.Order().ToArray());

    private static SensitivePermissionGrantPayload DeserializeProposalPayload(RbacProposalRow proposal)
    {
        if (!SensitiveRbacApprovalPolicy.PayloadHashMatches(proposal.PayloadJson, proposal.PayloadSha256))
            throw new InvalidOperationException("The proposal integrity check failed; no operation was applied.");
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<SensitivePermissionGrantPayload>(proposal.PayloadJson)
                ?? throw new InvalidOperationException("The proposal payload is unavailable.");
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new InvalidOperationException("The proposal payload is invalid; no operation was applied.", exception);
        }
    }

    private static RbacProposal MapProposal(RbacProposalRow row, long actor, bool canReview)
    {
        var payload = DeserializeProposalPayload(row);
        if (!SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, new SensitivePermissionGrantEnvelope(
                row.Id, row.OperationType, row.InitiatorUserId, row.CreatedAt, row.ExpiresAt,
                row.Reason, row.IdempotencyKey, row.CorrelationId,
                row.TargetRoleId, row.TargetPermissionId)))
            throw new InvalidOperationException("The proposal integrity check failed; no operation was applied.");
        return new RbacProposal(
            row.Id,
            row.OperationType,
            row.Status,
            payload.TargetRoleId,
            payload.TargetRoleCode,
            payload.TargetPermissionId,
            payload.TargetPermissionCode,
            payload.TargetPermissionName,
            payload.ModuleCode,
            payload.ActionCode,
            payload.ScopeClass,
            payload.SecurityClass,
            row.InitiatorUserId,
            row.CreatedAt,
            row.ExpiresAt,
            row.Reason,
            row.ApproverUserId,
            row.CancelledByUserId,
            row.DecisionAt,
            row.DecisionReason,
            row.ApplicationOutcome,
            row.CorrelationId,
            row.RowVersion,
            canReview && row.InitiatorUserId != actor && row.Status == SensitiveRbacApprovalPolicy.Pending,
            canReview && row.InitiatorUserId != actor && row.Status == SensitiveRbacApprovalPolicy.Pending,
            row.InitiatorUserId == actor && row.Status == SensitiveRbacApprovalPolicy.Pending);
    }

    private static string NormalizeCustomRoleCode(string code)
    {
        var normalized = Required(code, nameof(code), 64).ToUpperInvariant();
        EnsureValidCustomRoleCode(normalized);
        if (normalized is "USER" or "ADMIN" or "SUPER_ADMIN" or "ADMIN_HEAD" or "SPECTATOR" or
            "SUPERADMIN" or "ADMINHEAD" or "OPERATIONS" or "ADMINISTRATOR" or "AUDITOR")
            throw new ArgumentException("The code is reserved for a built-in or retained legacy role.", nameof(code));
        return normalized;
    }

    private static void EnsureValidCustomRoleCode(string code)
    {
        if (code.Length is < 2 or > 64 ||
            !char.IsAsciiLetterUpper(code[0]) ||
            code.Any(character => !char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_'))
            throw new ArgumentException("Custom role codes must use 2-64 uppercase letters, digits, and underscores, starting with a letter.", nameof(code));
        if (code is "USER" or "ADMIN" or "SUPER_ADMIN" or "ADMIN_HEAD" or "SPECTATOR" or
            "SUPERADMIN" or "ADMINHEAD" or "OPERATIONS" or "ADMINISTRATOR" or "AUDITOR")
            throw new ArgumentException("The code is reserved for a built-in or retained legacy role.", nameof(code));
    }

    private static void EnsureCustomRole(AdministrationRoleRow role)
    {
        if (role.SystemRole || role.Code is "USER" or "ADMIN" or "SUPER_ADMIN" or "ADMIN_HEAD" or "SPECTATOR" or
            "SUPERADMIN" or "ADMINHEAD" or "OPERATIONS" or "ADMINISTRATOR" or "AUDITOR")
            throw new UnauthorizedAccessException("Built-in and retained legacy role identities cannot be edited or deactivated as custom roles.");
        EnsureValidCustomRoleCode(role.Code);
    }

    private static void EnsureRolePermissionCeiling(AdministrationRoleRow role, AdministrationPermissionRow permission)
    {
        var builtIn = role.Code is "USER" or "ADMIN" or "SUPER_ADMIN" or "ADMIN_HEAD" or "SPECTATOR";
        if (role.SystemRole != builtIn)
            throw new UnauthorizedAccessException("Role identity metadata is inconsistent; permission changes are denied.");
        if (role.SystemRole && !builtIn)
            throw new UnauthorizedAccessException("Retained legacy role mappings cannot be changed through ordinary role administration.");
        if (!builtIn)
        {
            EnsureValidCustomRoleCode(role.Code);
            if (role.Code is "OPERATIONS" or "ADMINISTRATOR" or "AUDITOR" or "SUPERADMIN" or "ADMINHEAD")
                throw new UnauthorizedAccessException("Retained legacy role mappings cannot be changed through ordinary role administration.");
        }
        if (string.IsNullOrWhiteSpace(permission.ModuleCode) ||
            string.IsNullOrWhiteSpace(permission.ActionCode) ||
            string.IsNullOrWhiteSpace(permission.ScopeClass) ||
            string.IsNullOrWhiteSpace(permission.SecurityClass))
            throw new UnauthorizedAccessException("Permission metadata is incomplete; the permission cannot be granted.");
        if (!RoleAuthorizationPolicy.AllowsPermissionCode(role.Code, permission.Code))
            throw new UnauthorizedAccessException("This permission is outside the role's immutable permission-code ceiling.");

        var securityClass = permission.SecurityClass.ToUpperInvariant();
        if (securityClass is not ("READ_ONLY" or "BUSINESS" or "DEPARTMENT_ADMIN" or "ORGANIZATION_ADMIN" or "SECURITY_GOVERNANCE"))
            throw new UnauthorizedAccessException("Permission security metadata is unknown; the permission cannot be granted.");
        if (permission.ScopeClass.ToUpperInvariant() is not ("ORGANIZATION" or "DEPARTMENT" or "TEAM" or "ADMIN_GROUP" or "RECORD" or "NONE"))
            throw new UnauthorizedAccessException("Permission scope metadata is unknown; the permission cannot be granted.");
        if (!RoleAuthorizationPolicy.AllowsPermission(role.Code, role.SystemRole, permission.SecurityClass,
                permission.ActionCode, permission.ReadOnly))
            throw new UnauthorizedAccessException("The requested permission violates the role's immutable security ceiling.");
        if (role.Code == RoleAuthorizationPolicy.Spectator && (!permission.ReadOnly || securityClass != "READ_ONLY"))
            throw new UnauthorizedAccessException("Spectator can only receive explicitly read-only permissions.");
        if (role.Code is "USER" or "ADMIN" or "ADMIN_HEAD" &&
            securityClass is "ORGANIZATION_ADMIN" or "SECURITY_GOVERNANCE")
            throw new UnauthorizedAccessException("This role cannot receive organization-wide or security-governance permissions.");
        if (!builtIn && securityClass is "ORGANIZATION_ADMIN" or "SECURITY_GOVERNANCE")
            throw new UnauthorizedAccessException("Custom roles cannot receive organization-wide or security-governance permissions.");
        if (securityClass == "READ_ONLY" && !permission.ReadOnly)
            throw new UnauthorizedAccessException("Read-only security metadata is inconsistent; the permission cannot be granted.");
    }

    private static void EnsureDirectPermissionGrantAllowed(
        AdministrationRoleRow role, AdministrationPermissionRow permission)
    {
        if (SensitiveRbacApprovalPolicy.RequiresProposalForDirectGrant(
                role.SystemRole, permission.SecurityClass))
            throw new InvalidOperationException(
                "This permission grant requires an independent approval proposal.");
    }

    private static void ValidateRoleScope(
        string scopeType,
        string roleCode,
        long? organizationId,
        long? departmentId,
        long? teamId,
        long? clientId,
        string? resourceType,
        string? resourceId)
    {
        if (scopeType == "ORGANIZATION" && roleCode != RoleAuthorizationPolicy.SuperAdmin)
            throw new UnauthorizedAccessException("Organization-level scope is reserved for an explicitly scoped Super Admin membership.");
        var valid = scopeType switch
        {
            "ORGANIZATION" => organizationId is null && departmentId is null && teamId is null && clientId is null && resourceType is null && resourceId is null,
            "DEPARTMENT" => organizationId is null && departmentId > 0 && teamId is null && clientId is null && resourceType is null && resourceId is null,
            "TEAM" => organizationId is null && departmentId > 0 && teamId > 0 && clientId is null && resourceType is null && resourceId is null,
            "CLIENT" => organizationId is null && departmentId is null && teamId is null && clientId > 0 && resourceType is null && resourceId is null,
            "ADMIN_GROUP" => organizationId is null && departmentId is null && teamId is null && clientId is null && resourceType is null && resourceId is null &&
                             roleCode == RoleAuthorizationPolicy.AdminHead,
            "RECORD" => organizationId is null && departmentId is null && teamId is null && clientId is null &&
                        !string.IsNullOrWhiteSpace(resourceType) && !string.IsNullOrWhiteSpace(resourceId),
            _ => false
        };
        if (!valid)
            throw new ArgumentException("The scope type and its resource identifiers are inconsistent.");
    }

    private static void ValidateDates(DateTimeOffset from, DateTimeOffset? to) { if (to is not null && to <= from) throw new ArgumentException("Effective end must be after effective start."); }
    private static string Required(string value, string parameter, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            throw new ArgumentException($"Value must contain between 1 and {maximumLength} characters.", parameter);
        return value.Trim();
    }
    private static string? Optional(string? value, string parameter, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Trim().Length > maximumLength)
            throw new ArgumentException($"Value must not exceed {maximumLength} characters.", parameter);
        return value.Trim();
    }
    private static string? NormalizeEmail(string? value, string parameter)
    {
        var email = Optional(value, parameter, 320);
        if (email is not null && !new EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("Email address is invalid.", parameter);
        return email;
    }
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A required value is missing.") : value.Trim();
    private static AdministrationUser MapUser(AdministrationUserRow x) => new(x.Id, x.UserCode, x.LoginName, x.DisplayName, x.Email, x.DepartmentId, x.TeamId, x.Active, x.Phone, x.ManagerUserId, x.EmailVerified);
    private static AdministrationDepartment MapDepartment(AdministrationDepartmentRow x) => new(x.Id, x.Code, x.Name, x.Description, x.Active);
    private static AdministrationTeam MapTeam(AdministrationTeamRow x) => new(x.Id, x.DepartmentId, x.Code, x.Name, x.Description, x.Active);
    private static AdminHeadDelegation MapDelegation(AdminHeadDelegationRow x) => new(x.Id, x.AdminHeadUserId, x.AdminUserId, x.ValidFrom, x.ValidTo, x.Active);
    private static System.Linq.Expressions.Expression<Func<AdministrationUserRow, AdministrationUser>> MapUserExpression() => x => new AdministrationUser(x.Id, x.UserCode, x.LoginName, x.DisplayName, x.Email, x.DepartmentId, x.TeamId, x.Active, x.Phone, x.ManagerUserId, x.EmailVerified);
}


