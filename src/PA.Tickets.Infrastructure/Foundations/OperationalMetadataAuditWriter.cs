using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class OperationalMetadataAuditWriter(
    TicketsDbContext db,
    AuditChainCoordinator auditLocks,
    TimeProvider clock)
{
    public async Task AppendAsync(
        string entityType,
        string entityId,
        string action,
        long actor,
        string? oldValue,
        string? newValue,
        string? reason,
        CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, entityType, entityId, ct);
        var utcNow = clock.GetUtcNow().UtcDateTime;
        var at = new DateTimeOffset(utcNow.Ticks - utcNow.Ticks % 10, TimeSpan.Zero);
        var previous = await db.Audits
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.Id)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(ct);
        var canonical = JsonSerializer.Serialize(new
        {
            entityType,
            entityId,
            action,
            actorUserId = actor,
            auditAtUtc = at.UtcDateTime,
            oldValue,
            newValue,
            reason,
            previousHash = previous
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorId = actor,
            At = at,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            Hash = hash,
            PreviousHash = previous
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }
}
