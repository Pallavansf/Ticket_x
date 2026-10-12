using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Execution;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class SlaDeadlineMonitor(
    ExecutionDbContext db,
    TicketsDbContext tickets,
    PersistentSlaCalendarStore calendars,
    ISlaExecutionRepository slaOperations,
    ProviderTransactionRetry retry,
    TimeProvider clock)
{
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var terminalTicketIds = await tickets.Tickets.AsNoTracking()
            .Where(x => x.StatusId == (int)TicketStatus.Resolved ||
                x.StatusId == (int)TicketStatus.Closed ||
                x.StatusId == (int)TicketStatus.Cancelled)
            .Select(x => x.Id)
            .ToArrayAsync(ct);
        var slas = await db.Slas.AsNoTracking()
            .Where(x => (x.Status == "RUNNING" || x.Status == "PAUSED" || x.Status == "BREACHED") &&
                x.StartedAt != null &&
                ((x.ResponseDueAt != null && x.ResponseDueAt <= now) ||
                 (x.ResolutionDueAt != null && x.ResolutionDueAt <= now) ||
                 terminalTicketIds.Contains(x.TicketId) ||
                 db.Events.Any(e => e.TicketId == x.TicketId && e.At >= x.StartedAt &&
                     (e.Type == "TICKET_RESOLVED" || e.Type == "TICKET_CANCELLED" ||
                      (e.Type == "TICKET_STATUS_CHANGED" &&
                       (e.NewValue == "RESOLVED" || e.NewValue == "CLOSED" || e.NewValue == "CANCELLED"))))))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        if (slas.Count == 0) return 0;

        var ticketIds = slas.Select(x => x.TicketId).Distinct().ToArray();
        var slaIds = slas.Select(x => x.Id).ToArray();
        var ticketStates = await tickets.Tickets.AsNoTracking()
            .Where(x => ticketIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var lifecycleEvents = await db.Events.AsNoTracking()
            .Where(x => ticketIds.Contains(x.TicketId) &&
                (x.Type == "TICKET_RESOLVED" || x.Type == "TICKET_CANCELLED" || x.Type == "TICKET_STATUS_CHANGED"))
            .ToListAsync(ct);
        var pauseRows = await db.SlaPauses.AsNoTracking()
            .Where(x => slaIds.Contains(x.SlaId))
            .OrderBy(x => x.StartedAt)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var sla in slas)
        {
            if (sla.StartedAt is null || !ticketStates.TryGetValue(sla.TicketId, out var ticket)) continue;
            var terminalAt = FindTerminalAt(sla, ticket, lifecycleEvents, now);
            if (sla.Status == "PAUSED" && terminalAt is null) continue;

            var evaluationAt = terminalAt ?? now;
            var pauses = new List<(DateTimeOffset Start, DateTimeOffset End)>();
            foreach (var pause in pauseRows.Where(x => x.SlaId == sla.Id))
            {
                var pauseEnd = pause.EndedAt ?? terminalAt;
                if (pauseEnd is null) continue;
                var end = Min(pauseEnd.Value, evaluationAt);
                if (end > pause.StartedAt) pauses.Add((pause.StartedAt, end));
            }
            var calendar = pauses.Count == 0
                ? null
                : await calendars.ResolveAsync(sla.CalendarCode ?? throw new InvalidOperationException($"SLA {sla.Id} has pause history but no calendar code."), ct);

            var responseDueAt = ExtendDeadline(sla.ResponseDueAt, pauses, calendar);
            var resolutionDueAt = ExtendDeadline(sla.ResolutionDueAt, pauses, calendar);
            if (sla.Status != "PAUSED" || terminalAt is not null)
            {
                if (responseDueAt is not null && responseDueAt <= evaluationAt)
                    processed += await RecordBreachAsync(sla.Id, "RESPONSE", responseDueAt.Value, now, ct);
                if (resolutionDueAt is not null && resolutionDueAt <= evaluationAt)
                    processed += await RecordBreachAsync(sla.Id, "RESOLUTION", resolutionDueAt.Value, now, ct);
            }

            if (terminalAt is not null)
            {
                await slaOperations.ApplyAsync(
                    sla.Id,
                    DurableSlaOperation.Complete,
                    "Ticket reached a terminal lifecycle state.",
                    0,
                    $"SLA:{sla.Id}:TERMINAL",
                    null,
                    ct,
                    terminalAt);
            }
        }

        return processed;
    }

    private async Task<int> RecordBreachAsync(long slaId, string target, DateTimeOffset occurredAt, DateTimeOffset observedAt, CancellationToken ct)
    {
        return await retry.ExecuteAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var key = $"SLA:{slaId}:DEADLINE:{target}";
            if (await db.Idempotency.AnyAsync(x => x.Scope == "SLA_DEADLINE" && x.Key == key, ct))
            {
                await tx.CommitAsync(ct);
                return 0;
            }

            var sla = await db.Slas.SingleAsync(x => x.Id == slaId, ct);
            if (sla.Status == "COMPLETED")
            {
                await tx.CommitAsync(ct);
                return 0;
            }

            sla.Status = "BREACHED";
            if (sla.BreachedAt is null || occurredAt < sla.BreachedAt.Value) sla.BreachedAt = occurredAt;
            sla.RowVersion++;
            db.Events.Add(new EventRow
            {
                TicketId = sla.TicketId,
                Type = target == "RESPONSE" ? "SLA_RESPONSE_BREACH" : "SLA_RESOLUTION_BREACH",
                Category = "SLA",
                ActorId = 0,
                NewValue = target,
                At = occurredAt
            });
            db.Idempotency.Add(new IdempotencyRow
            {
                Scope = "SLA_DEADLINE",
                Key = key,
                Fingerprint = $"{slaId}:{target}",
                Status = "COMPLETED",
                CreatedAt = observedAt,
                CompletedAt = observedAt
            });

            var policy = sla.PolicyId is null || sla.PolicyVersion is null
                ? null
                : await db.SlaPolicies.SingleOrDefaultAsync(x => x.PolicyId == sla.PolicyId && x.VersionNo == sla.PolicyVersion, ct);
            if (policy is not null && !string.IsNullOrWhiteSpace(policy.EscalationRules))
            {
                var dedupeKey = $"SLA:{slaId}:BREACH";
                if (!await db.Jobs.AnyAsync(x => x.Type == "SLA_ESCALATE" && x.DedupeKey == dedupeKey, ct))
                    db.Jobs.Add(new BackgroundJobRow
                    {
                        Type = "SLA_ESCALATE",
                        DedupeKey = dedupeKey,
                        Payload = $"{{\"slaId\":{slaId}}}",
                        Status = "PENDING",
                        ScheduledAt = observedAt,
                        NextAttemptAt = observedAt,
                        CorrelationId = null
                    });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return 1;
        }, ct);
    }

    private static DateTimeOffset? ExtendDeadline(
        DateTimeOffset? dueAt,
        IReadOnlyCollection<(DateTimeOffset Start, DateTimeOffset End)> pauses,
        PA.Tickets.Domain.Sla.BusinessCalendar? calendar)
    {
        if (dueAt is null || calendar is null) return dueAt;
        var adjusted = dueAt.Value;
        foreach (var pause in pauses)
        {
            if (pause.Start > adjusted) break;
            var pausedMinutes = calendar.WorkingMinutesBetween(pause.Start, pause.End);
            if (pausedMinutes > 0) adjusted = calendar.AddWorkingMinutes(adjusted, pausedMinutes);
        }
        return adjusted;
    }

    private static DateTimeOffset? FindTerminalAt(SlaExecutionRow sla, TicketRow ticket, IReadOnlyCollection<EventRow> events, DateTimeOffset now)
    {
        var terminalEvents = events.Where(x => x.TicketId == sla.TicketId && x.At >= sla.StartedAt)
            .Where(x => x.Type is "TICKET_RESOLVED" or "TICKET_CANCELLED" ||
                (x.Type == "TICKET_STATUS_CHANGED" && (x.NewValue ?? "").Trim().ToUpperInvariant() is "RESOLVED" or "CLOSED" or "CANCELLED"))
            .Select(x => (DateTimeOffset?)x.At)
            .ToList();

        if (ticket.StatusId == (int)TicketStatus.Resolved && ticket.ResolvedAt >= sla.StartedAt)
            terminalEvents.Add(ticket.ResolvedAt);
        if (ticket.StatusId == (int)TicketStatus.Closed && ticket.ClosedAt >= sla.StartedAt)
            terminalEvents.Add(ticket.ClosedAt);
        if (ticket.StatusId == (int)TicketStatus.Resolved && terminalEvents.Count == 0)
            terminalEvents.Add(now);
        if ((ticket.StatusId == (int)TicketStatus.Closed || ticket.StatusId == (int)TicketStatus.Cancelled) && terminalEvents.Count == 0)
            terminalEvents.Add(now);
        return terminalEvents.Min();
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
