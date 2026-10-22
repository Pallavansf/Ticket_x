using System.Security.Cryptography;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class LocalAuthenticationService(
    TicketsDbContext db,
    AuditChainCoordinator auditLocks,
    TimeProvider clock) : ILocalAuthenticationService
{
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<AdministrationUserRow>().HashPassword(
            new AdministrationUserRow(),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    private readonly PasswordHasher<AdministrationUserRow> passwordHasher = new();

    public async Task<LocalAuthenticatedUser?> AuthenticateAsync(string loginName, string password, CancellationToken cancellationToken)
    {
        var normalizedLogin = loginName.Trim();
        var user = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.LoginName == normalizedLogin, cancellationToken);
        if (user is null || !user.Active || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            passwordHasher.VerifyHashedPassword(new AdministrationUserRow(), DummyPasswordHash.Value, password);
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return null;

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await db.SaveChangesAsync(cancellationToken);
        }

        var now = clock.GetUtcNow();
        var roleCodes = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                               join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                               where membership.UserId == user.Id && membership.Active && membership.ValidFrom <= now
                                   && (membership.ValidTo == null || membership.ValidTo > now) && role.Active
                               select role.Code).Distinct().ToListAsync(cancellationToken);
        var permissionGrants = await (from membership in db.AdministrationUserRoles.AsNoTracking()
                                      join role in db.AdministrationRoles.AsNoTracking() on membership.RoleId equals role.Id
                                      join grant in db.AdministrationRolePermissions.AsNoTracking() on role.Id equals grant.RoleId
                                      join permission in db.AdministrationPermissions.AsNoTracking() on grant.PermissionId equals permission.Id
                                      where membership.UserId == user.Id && membership.Active && membership.ValidFrom <= now
                                          && (membership.ValidTo == null || membership.ValidTo > now) && role.Active &&
                                          grant.Active && grant.EffectiveStatus == "ACTIVE" && grant.ValidFrom.HasValue && grant.ValidFrom <= now && (grant.ValidTo == null || grant.ValidTo > now) &&
                                          permission.Active
                                      select new
                                      {
                                          RoleCode = role.Code,
                                          role.SystemRole,
                                           grant.Active,
                                           grant.EffectiveStatus,
                                           grant.ValidFrom,
                                           grant.ValidTo,
                                          permission.Code,
                                          permission.SecurityClass,
                                          permission.ActionCode,
                                          permission.ReadOnly
                                      }).ToListAsync(cancellationToken);
        var hasSpectatorMembership = roleCodes.Contains(RoleAuthorizationPolicy.Spectator, StringComparer.OrdinalIgnoreCase);
        var permissionCodes = permissionGrants
            .Where(grant => EffectiveGrantAuthorization.IsEffective(grant.Active, grant.EffectiveStatus,
                grant.ValidFrom, grant.ValidTo, now))
            .Where(grant => RoleAuthorizationPolicy.AllowsPermission(grant.RoleCode, grant.SystemRole,
                grant.SecurityClass, grant.ActionCode, grant.ReadOnly))
            .Where(grant => !hasSpectatorMembership ||
                            grant.ReadOnly && grant.SecurityClass.Equals("READ_ONLY", StringComparison.OrdinalIgnoreCase))
            .Where(grant => grant.RoleCode != RoleAuthorizationPolicy.LegacyAdministrator ||
                            RoleAuthorizationPolicy.IsLegacyAdministratorPermission(grant.Code))
            .Select(grant => grant.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new LocalAuthenticatedUser(user.Id, user.LoginName, user.DisplayName, roleCodes, permissionCodes, user.PasswordRotationRequired);
    }

    public async Task<bool> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 12 || newPassword.Length > 1024)
            throw new ArgumentException("Password must contain between 12 and 1024 characters.", nameof(newPassword));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId && x.Active, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return false;

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.PasswordRotationRequired = false;
        var clockNow = clock.GetUtcNow().UtcDateTime;
        var now = new DateTimeOffset(clockNow.Ticks - clockNow.Ticks % 10, TimeSpan.Zero);
        user.UpdatedAt = now;
        user.UpdatedBy = userId;
        var entityId = userId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await auditLocks.AcquireAsync(db, "ADMINISTRATION", entityId, cancellationToken);

        var previous = await db.Audits.Where(x => x.EntityType == "ADMINISTRATION" && x.EntityId == entityId)
            .OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(cancellationToken);
        var canonical = JsonSerializer.Serialize(new
        {
            entityType = "ADMINISTRATION",
            entityId,
            action = "USER_PASSWORD_CHANGED",
            actorUserId = userId,
            auditAtUtc = now.UtcDateTime,
            oldValue = (string?)null,
            newValue = (string?)null,
            reason = (string?)null,
            previousHash = previous
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow
        {
            EntityType = "ADMINISTRATION",
            EntityId = entityId,
            Action = "USER_PASSWORD_CHANGED",
            ActorId = userId,
            At = now,
            Hash = hash,
            PreviousHash = previous
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}

