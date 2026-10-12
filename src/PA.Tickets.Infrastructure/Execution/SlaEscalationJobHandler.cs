using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class SlaEscalationJobHandler(ExecutionDbContext db, AuditChainCoordinator auditLocks) : IBackgroundJobHandler
{
    public bool CanHandle(string type) => type == "SLA_ESCALATE";

    public async Task HandleAsync(BackgroundJobEnvelope job, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Idempotency.AnyAsync(x => x.Scope == "SLA_ESCALATION" && x.Key == job.DedupeKey, ct)) { await tx.CommitAsync(ct); return; }
        using var payload = JsonDocument.Parse(job.Payload);
        var slaId = payload.RootElement.GetProperty("slaId").GetInt64();
        var sla = await db.Slas.SingleAsync(x => x.Id == slaId, ct);
        if (sla.Status != "BREACHED") return;
        var policy = sla.PolicyId is null || sla.PolicyVersion is null
            ? null
            : await db.SlaPolicies.SingleOrDefaultAsync(x => x.PolicyId == sla.PolicyId && x.VersionNo == sla.PolicyVersion, ct);
        if (policy is null || string.IsNullOrWhiteSpace(policy.EscalationRules)) throw new InvalidOperationException("The breached SLA has no configured escalation policy.");
        var now = DateTimeOffset.UtcNow;
        db.Events.Add(new EventRow { TicketId = sla.TicketId, Type = "SLA_ESCALATED", Category = "SLA", ActorId = 0, NewValue = policy.EscalationRules, Reason = job.CorrelationId, At = now });
        await auditLocks.AcquireAsync(db, "SLA", sla.Id.ToString(), ct);
        var previous = await db.Audits.Where(x => x.EntityType == "SLA" && x.EntityId == sla.Id.ToString()).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var canonical = JsonSerializer.Serialize(new { entityType = "SLA", entityId = sla.Id.ToString(), action = "SLA_ESCALATED", actorUserId = 0L, auditAtUtc = now.UtcDateTime, oldValue = (string?)null, newValue = policy.EscalationRules, reason = job.CorrelationId, previousHash = previous });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow { EntityType = "SLA", EntityId = sla.Id.ToString(), Action = "SLA_ESCALATED", ActorId = 0, At = now, NewValue = policy.EscalationRules, Reason = job.CorrelationId, Hash = hash, PreviousHash = previous });
        db.Idempotency.Add(new IdempotencyRow { Scope = "SLA_ESCALATION", Key = job.DedupeKey, Fingerprint = $"{job.Id}:{job.Type}", Status = "COMPLETED", CreatedAt = now, CompletedAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
