using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Sla;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Sla;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class PersistentSlaOrchestrationService(
    ExecutionDbContext db,
    ISlaPolicyResolver policies,
    PersistentSlaCalendarStore calendars,
    ProviderTransactionRetry retry) : ISlaOrchestrationService
{
    public Task<SlaStartResult> StartAsync(long slaId, long ticketId, SlaEvaluationContext context, long actor, DateTimeOffset startedAt, CancellationToken ct)
        => retry.ExecuteAsync(db, () => StartOnceAsync(slaId, ticketId, context, actor, startedAt, ct), ct);

    private async Task<SlaStartResult> StartOnceAsync(long slaId, long ticketId, SlaEvaluationContext context, long actor, DateTimeOffset startedAt, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var policy = await policies.ResolveAsync(context, startedAt, ct)
            ?? throw new InvalidOperationException("No effective SLA policy matched the ticket context.");
        var row = await db.Slas.SingleOrDefaultAsync(x => x.Id == slaId, ct);
        if (row is not null && (row.TicketId != ticketId || row.PolicyId != policy.PolicyId || row.PolicyVersion != policy.VersionNo))
            throw new InvalidOperationException("The SLA instance already has a different ticket or selected policy.");
        if (row?.StartedAt is DateTimeOffset existingStart)
        {
            if (existingStart < startedAt.AddTicks(-TimeSpan.TicksPerMicrosecond) ||
                existingStart > startedAt.AddTicks(TimeSpan.TicksPerMicrosecond) ||
                row.CalendarCode != policy.CalendarCode ||
                policy.ResponseTargetMinutes is not null && row.ResponseDueAt is null ||
                policy.ResolutionTargetMinutes is not null && row.ResolutionDueAt is null)
                throw new InvalidOperationException("The persisted SLA instance is incomplete or does not match its original start.");
            await tx.CommitAsync(ct);
            return new(slaId, policy.PolicyId, policy.VersionNo, row.CalendarCode, row.ResponseDueAt, row.ResolutionDueAt);
        }

        var calendar = await calendars.ResolveAsync(policy.CalendarCode, ct);
        var clock = new SlaClock(policy, calendar, startedAt);
        row ??= new SlaExecutionRow { Id = slaId, TicketId = ticketId };
        row.TicketId = ticketId;
        row.Status = "RUNNING";
        row.PolicyId = policy.PolicyId;
        row.PolicyVersion = policy.VersionNo;
        row.CalendarCode = policy.CalendarCode;
        row.StartedAt = startedAt;
        row.ResponseDueAt = clock.ResponseDueAt;
        row.ResolutionDueAt = clock.ResolutionDueAt;
        if (db.Entry(row).State == EntityState.Detached) db.Slas.Add(row);
        db.Events.Add(new PA.Tickets.Infrastructure.Persistence.EventRow { TicketId = ticketId, Type = "SLA_STARTED", Category = "SLA", ActorId = actor, NewValue = $"{policy.Code}:{policy.VersionNo}", At = startedAt });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(slaId, policy.PolicyId, policy.VersionNo, policy.CalendarCode, clock.ResponseDueAt, clock.ResolutionDueAt);
    }
}
