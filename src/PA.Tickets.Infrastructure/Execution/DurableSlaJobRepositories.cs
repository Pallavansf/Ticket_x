using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class DurableSlaExecutionRepository(ExecutionDbContext db, IWaitingReasonCatalog waitingReasons, ProviderTransactionRetry retry) : ISlaExecutionRepository
{
    public async Task<DurableMutationResult> ApplyAsync(long slaId, DurableSlaOperation operation, string? reason, long actor, string key, string? correlationId, CancellationToken ct, DateTimeOffset? effectiveAt = null)
    {
        return await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            if (await db.Idempotency.AnyAsync(x => x.Scope == "SLA_OPERATION" && x.Key == key, ct))
                return new DurableMutationResult(false, true, "Already processed.");

            var sla = await db.Slas.SingleOrDefaultAsync(x => x.Id == slaId, ct) ?? throw new KeyNotFoundException("SLA instance was not found.");
            var now = effectiveAt ?? DateTimeOffset.UtcNow;

            switch (operation)
            {
                case DurableSlaOperation.Pause:
                    var definition = await waitingReasons.ResolveAsync(reason ?? "", now, ct);
                    if (!definition.PauseSla) throw new InvalidOperationException("This waiting reason does not pause SLA time.");
                    if (sla.Status != "RUNNING") throw new InvalidOperationException("Only running SLA can pause.");
                    sla.Status = "PAUSED";
                    sla.PausedAt = now;
                    db.SlaPauses.Add(new SlaPauseExecutionRow
                    {
                        SlaId = slaId,
                        ReasonCode = definition.Code,
                        Reason = reason,
                        StartedAt = now,
                        StartedBy = actor
                    });
                    break;

                case DurableSlaOperation.Resume:
                    if (sla.Status != "PAUSED") throw new InvalidOperationException("Only paused SLA can resume.");
                    sla.Status = "RUNNING";
                    sla.PausedAt = null;
                    var pause = await db.SlaPauses.Where(x => x.SlaId == slaId && x.EndedAt == null).OrderByDescending(x => x.StartedAt).FirstOrDefaultAsync(ct);
                    if (pause is not null)
                    {
                        pause.EndedAt = now;
                        pause.EndedBy = actor;
                    }
                    break;

                case DurableSlaOperation.Breach:
                    sla.Status = "BREACHED";
                    sla.BreachedAt ??= now;
                    var escalation = sla.PolicyId is null || sla.PolicyVersion is null ? null : await db.SlaPolicies.SingleOrDefaultAsync(x => x.PolicyId == sla.PolicyId && x.VersionNo == sla.PolicyVersion, ct);
                    if (escalation is not null && !string.IsNullOrWhiteSpace(escalation.EscalationRules))
                    {
                        var dedupeKey = $"SLA:{slaId}:BREACH";
                        if (!await db.Jobs.AnyAsync(x => x.Type == "SLA_ESCALATE" && x.DedupeKey == dedupeKey, ct))
                            db.Jobs.Add(new BackgroundJobRow
                        {
                            Type = "SLA_ESCALATE",
                            DedupeKey = dedupeKey,
                            Payload = $"{{\"slaId\":{slaId}}}",
                            Status = "PENDING",
                            ScheduledAt = now,
                            NextAttemptAt = now,
                            CorrelationId = correlationId
                        });
                    }
                    break;

                case DurableSlaOperation.Complete:
                    sla.Status = "COMPLETED";
                    sla.CompletedAt = now;
                    sla.PausedAt = null;
                    var activePause = await db.SlaPauses
                        .Where(x => x.SlaId == slaId && x.EndedAt == null)
                        .OrderByDescending(x => x.StartedAt)
                        .FirstOrDefaultAsync(ct);
                    if (activePause is not null)
                    {
                        activePause.EndedAt = now;
                        activePause.EndedBy = actor;
                    }
                    break;
            }

            sla.RowVersion++;
            var eventType = operation switch
            {
                DurableSlaOperation.Pause => "SLA_PAUSED",
                DurableSlaOperation.Resume => "SLA_RESUMED",
                DurableSlaOperation.Breach => "SLA_BREACH",
                DurableSlaOperation.Complete => "SLA_COMPLETED",
                _ => "SLA_UPDATED"
            };
            db.Events.Add(new EventRow
            {
                TicketId = sla.TicketId,
                Type = eventType,
                Category = "SLA",
                ActorId = actor,
                NewValue = sla.Status,
                At = now
            });
            db.Idempotency.Add(new IdempotencyRow
            {
                Scope = "SLA_OPERATION",
                Key = key,
                Fingerprint = $"{slaId}:{operation}:{reason ?? string.Empty}:{actor}:{correlationId ?? string.Empty}",
                Status = "COMPLETED",
                CreatedAt = now,
                CompletedAt = now
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new DurableMutationResult(true, false, sla.Status);
        }, ct);
    }
}

public sealed class DurableBackgroundJobRepository(ExecutionDbContext db, IDatabaseProviderCapabilities capabilities, IConfiguration configuration, ILogger<DurableBackgroundJobRepository>? logger = null) : IBackgroundJobRepository
{
    public async Task<bool> EnqueueAsync(string type, string dedupeKey, string payload, DateTimeOffset scheduledAt, CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(x => x.Type == type && x.DedupeKey == dedupeKey, ct)) return false;
        db.Jobs.Add(new BackgroundJobRow { Type = type, DedupeKey = dedupeKey, Payload = payload, Status = "PENDING", ScheduledAt = scheduledAt, NextAttemptAt = scheduledAt, MaxAttempts = Math.Clamp(configuration.GetValue("Jobs:Retry:MaxAttempts", 5), 1, 20) });
        try { await db.SaveChangesAsync(ct); OperationalMetrics.JobsQueued.Add(1, new KeyValuePair<string, object?>("job.type", type)); return true; }
        catch (DbUpdateException) { return false; }
    }

    public async Task<IReadOnlyCollection<BackgroundJobEnvelope>> LeaseReadyAsync(string workerId, int limit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        await db.Jobs.Where(x => x.Status == "PROCESSING" && x.LockedAt < now.AddMinutes(-10) && x.Attempts >= x.MaxAttempts)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "EXHAUSTED").SetProperty(x => x.FailedAt, now).SetProperty(x => x.NextAttemptAt, (DateTimeOffset?)null).SetProperty(x => x.LockedAt, (DateTimeOffset?)null).SetProperty(x => x.LockedBy, (string?)null), ct);
        var rows = await db.Jobs.FromSqlRaw(capabilities.BuildJobLeaseSql(limit)).ToListAsync(ct);
        foreach (var row in rows)
        {
            row.Status = "PROCESSING"; row.LockedAt = now; row.LockedBy = workerId; row.Attempts++; row.LastAttemptAt = now;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        foreach (var row in rows) { OperationalMetrics.JobsLeased.Add(1, new KeyValuePair<string, object?>("job.type", row.Type)); logger?.LogInformation("Durable job leased {JobId} {JobType} {Attempt}/{MaxAttempts}", row.Id, row.Type, row.Attempts, row.MaxAttempts); }
        return rows.Select(x => new BackgroundJobEnvelope(x.Id, x.Type, x.DedupeKey, x.Payload ?? "", x.ScheduledAt, x.CorrelationId)).ToArray();
    }

    public async Task CompleteAsync(long id, CancellationToken ct)
    {
        var row = await db.Jobs.SingleAsync(x => x.Id == id, ct);
        if (row.Status == "COMPLETED") return;
        row.Status = "COMPLETED"; row.CompletedAt = DateTimeOffset.UtcNow; row.LockedAt = null; row.LockedBy = null; row.NextAttemptAt = null;
        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Durable job completed {JobId} {JobType}", row.Id, row.Type);
    }

    public async Task FailAsync(long id, string error, bool exhausted, CancellationToken ct)
    {
        var row = await db.Jobs.SingleAsync(x => x.Id == id, ct);
        if (row.Status is "COMPLETED" or "EXHAUSTED") return;
        var now = DateTimeOffset.UtcNow;
        row.LastError = error.Length > 2000 ? error[..2000] : error;
        row.FirstFailedAt ??= now;
        row.FailureClassification = exhausted ? "PERMANENT" : "TRANSIENT";
        row.LockedAt = null; row.LockedBy = null;
        if (exhausted || row.Attempts >= row.MaxAttempts)
        {
            row.Status = "EXHAUSTED"; row.NextAttemptAt = null; row.FailedAt = now; OperationalMetrics.JobDeadLettered.Add(1, new KeyValuePair<string, object?>("job.type", row.Type));
            logger?.LogError("Durable job moved to dead letter {JobId} {JobType} {Attempt}/{MaxAttempts} {FailureClassification}", row.Id, row.Type, row.Attempts, row.MaxAttempts, row.FailureClassification);
        }
        else
        {
            var initial = Math.Clamp(configuration.GetValue("Jobs:Retry:InitialDelaySeconds", 5), 1, 3600);
            var max = Math.Clamp(configuration.GetValue("Jobs:Retry:MaxDelaySeconds", 3600), initial, 86400);
            var seconds = DurableJobRetryPolicy.DelaySeconds(row.Attempts, initial, max);
            row.Status = "PENDING"; row.NextAttemptAt = now.AddSeconds(seconds); OperationalMetrics.JobRetries.Add(1, new KeyValuePair<string, object?>("job.type", row.Type));
            logger?.LogWarning("Durable job retry scheduled {JobId} {JobType} {Attempt}/{MaxAttempts} {NextAttemptAt} {FailureClassification}", row.Id, row.Type, row.Attempts, row.MaxAttempts, row.NextAttemptAt, row.FailureClassification);
        }
        await db.SaveChangesAsync(ct);
    }
}