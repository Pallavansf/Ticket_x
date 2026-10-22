using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class InitialAdministratorProvisioner(
    TicketsDbContext db,
    IDatabaseProviderCapabilities capabilities,
    AuditChainCoordinator auditLocks,
    TimeProvider clock,
    IPasswordHasher<AdministrationUserRow> passwordHasher) : IInitialAdministratorProvisioner
{
    private const string BootstrapAuditEntity = "INITIAL_SUPER_ADMIN_BOOTSTRAP";
    private const string LegacyBootstrapAuditEntity = "INITIAL_ADMINISTRATOR_BOOTSTRAP";

    public async Task ProvisionAsync(
        string loginName,
        string displayName,
        string? email,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        loginName = Required(loginName, nameof(loginName), 200);
        displayName = Required(displayName, nameof(displayName), 300);
        email = string.IsNullOrWhiteSpace(email) ? null : Required(email, nameof(email), 320);
        if (temporaryPassword.Length is < 16 or > 1024)
            throw new ArgumentException("Temporary password must contain between 16 and 1024 characters.", nameof(temporaryPassword));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await auditLocks.AcquireAsync(db, "ADMINISTRATION", BootstrapAuditEntity, cancellationToken);
        if (await db.Audits.AnyAsync(x =>
                x.EntityType == "ADMINISTRATION" &&
                (x.EntityId == BootstrapAuditEntity || x.EntityId == LegacyBootstrapAuditEntity) &&
                (x.Action == "INITIAL_SUPER_ADMIN_BOOTSTRAPPED" ||
                 x.Action == "INITIAL_ADMINISTRATOR_BOOTSTRAPPED"), cancellationToken))
            throw new InvalidOperationException("Initial Super Admin bootstrap has already been completed.");

        var clockNow = clock.GetUtcNow().UtcDateTime;
        var now = new DateTimeOffset(clockNow.Ticks - clockNow.Ticks % 10, TimeSpan.Zero);
        var superAdminRole = await db.AdministrationRoles.SingleOrDefaultAsync(
            x => x.Code == RoleAuthorizationPolicy.SuperAdmin && x.Active, cancellationToken)
            ?? throw new InvalidOperationException("The active SUPER_ADMIN role is not configured.");
        var alreadyHasPrivilegedAdministrator = await (
            from membership in db.AdministrationUserRoles
            join account in db.AdministrationUsers on membership.UserId equals account.Id
            join role in db.AdministrationRoles on membership.RoleId equals role.Id
            where account.Active && membership.Active && membership.ValidFrom <= now &&
                  (membership.ValidTo == null || membership.ValidTo > now) &&
                  role.Active && (role.Code == RoleAuthorizationPolicy.SuperAdmin ||
                                  role.Code == RoleAuthorizationPolicy.LegacyAdministrator)
            select account.Id).AnyAsync(cancellationToken);
        if (alreadyHasPrivilegedAdministrator)
            throw new InvalidOperationException("An active legacy Administrator or Super Admin exists; use the approved transition process.");
        if (await db.AdministrationUsers.AnyAsync(x => x.LoginName == loginName, cancellationToken))
            throw new InvalidOperationException("The requested login name is already in use.");

        var userId = await NextIdAsync("PA_TICKETS_USER_SEQ", cancellationToken);
        var membershipId = await NextIdAsync("PA_TICKETS_USER_ROLE_SEQ", cancellationToken);
        var user = new AdministrationUserRow
        {
            Id = userId,
            UserCode = $"BOOTSTRAP-{Guid.NewGuid():N}",
            LoginName = loginName,
            DisplayName = displayName,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(new AdministrationUserRow(), temporaryPassword),
            PasswordRotationRequired = true,
            Active = true,
            CreatedAt = now,
            CreatedBy = 0
        };
        db.AdministrationUsers.Add(user);
        await AddAuditAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture), "USER_CREATED", userId, loginName, cancellationToken);

        db.AdministrationUserRoles.Add(new AdministrationUserRoleRow
        {
            Id = membershipId,
            UserId = userId,
            RoleId = superAdminRole.Id,
            ValidFrom = now,
            Active = true,
            CreatedAt = now,
            CreatedBy = userId
        });
        await AddAuditAsync(membershipId.ToString(System.Globalization.CultureInfo.InvariantCulture), "USER_ROLE_ASSIGNED", userId, $"{userId}:{superAdminRole.Id}", cancellationToken);
        await AddAuditAsync(BootstrapAuditEntity, "INITIAL_SUPER_ADMIN_BOOTSTRAPPED", userId, loginName, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<long> NextIdAsync(string sequence, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            var quotedSequence = $"\"{sequence}\"";
            command.CommandText = capabilities.Kind == DatabaseProviderKind.PostgreSql
                ? $"select nextval('{quotedSequence}'::regclass)"
                : $"select {sequence}.NEXTVAL from DUAL";
            return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task AddAuditAsync(string entityId, string action, long actorId, string newValue, CancellationToken cancellationToken)
    {
        await auditLocks.AcquireAsync(db, "ADMINISTRATION", entityId, cancellationToken);
        var previousHash = await db.Audits
            .Where(x => x.EntityType == "ADMINISTRATION" && x.EntityId == entityId)
            .OrderByDescending(x => x.Id)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(cancellationToken);
        var clockNow = clock.GetUtcNow().UtcDateTime;
        var now = new DateTimeOffset(clockNow.Ticks - clockNow.Ticks % 10, TimeSpan.Zero);
        var canonical = JsonSerializer.Serialize(new
        {
            entityType = "ADMINISTRATION",
            entityId,
            action,
            actorUserId = actorId,
            auditAtUtc = now.UtcDateTime,
            oldValue = (string?)null,
            newValue,
            reason = (string?)null,
            previousHash
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow
        {
            EntityType = "ADMINISTRATION",
            EntityId = entityId,
            Action = action,
            ActorId = actorId,
            At = now,
            NewValue = newValue,
            Hash = hash,
            PreviousHash = previousHash
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Required(string value, string parameter, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            throw new ArgumentException($"Value must contain between 1 and {maximumLength} characters.", parameter);
        return value.Trim();
    }
}
