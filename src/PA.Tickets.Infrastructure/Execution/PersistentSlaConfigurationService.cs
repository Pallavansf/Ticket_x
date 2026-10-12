using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Sla;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class PersistentSlaConfigurationService(ExecutionDbContext db, AuditChainCoordinator auditLocks, ProviderTransactionRetry retry) : ISlaConfigurationService
{
    public async Task<SlaPolicyVersion> CreateVersionAsync(SlaPolicyVersion policy, long actor, string reason, CancellationToken ct)
        => await retry.ExecuteAsync(db, () => CreateVersionOnceAsync(policy, actor, reason, ct), ct);

    private async Task<SlaPolicyVersion> CreateVersionOnceAsync(SlaPolicyVersion policy, long actor, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var existing = await db.SlaPolicies.AnyAsync(x => x.PolicyId == policy.PolicyId && x.VersionNo == policy.VersionNo, ct);
        if (existing) throw new InvalidOperationException("The SLA policy version already exists and is immutable.");
        var id = (await db.SlaPolicies.Select(x => x.Id).DefaultIfEmpty().MaxAsync(ct)) + 1;
        db.SlaPolicies.Add(new SlaPolicyRow { Id = id, PolicyId = policy.PolicyId, Code = policy.Code, VersionNo = policy.VersionNo, Active = false, Status = "DRAFT", CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actor, EffectiveFrom = policy.EffectiveFrom, EffectiveTo = policy.EffectiveTo, ServiceId = policy.ServiceId, RequestTypeId = policy.RequestTypeId, PriorityId = policy.PriorityId, ClientId = policy.ClientId, DepartmentId = policy.DepartmentId, TeamId = policy.TeamId, TicketType = policy.TicketType, CategoryId = policy.CategoryId, SubCategoryId = policy.SubCategoryId, ResponseTargetMinutes = policy.ResponseTargetMinutes, ResolutionTargetMinutes = policy.ResolutionTargetMinutes, CalendarCode = policy.CalendarCode, WaitingRules = policy.WaitingRules, PauseRules = policy.PauseRules, EscalationRules = policy.EscalationRules, BreachRules = policy.BreachRules, NotificationRules = policy.NotificationRules });
        await AddAuditAsync(id.ToString(), "SLA_POLICY_VERSION_CREATED", actor, reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return policy;
    }

    public async Task<SlaPolicyVersion> ActivateAsync(long policyId, int version, long actor, string reason, CancellationToken ct)
        => await retry.ExecuteAsync(db, () => ActivateOnceAsync(policyId, version, actor, reason, ct), ct);

    private async Task<SlaPolicyVersion> ActivateOnceAsync(long policyId, int version, long actor, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.SlaPolicies.SingleOrDefaultAsync(x => x.PolicyId == policyId && x.VersionNo == version, ct) ?? throw new KeyNotFoundException("SLA policy version was not found.");
        row.Active = true;
        row.Status = "PUBLISHED";
        row.PublishedAt = DateTimeOffset.UtcNow;
        row.PublishedBy = actor;
        await AddAuditAsync(row.Id.ToString(), "SLA_POLICY_VERSION_ACTIVATED", actor, reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(row.PolicyId, row.Code, row.VersionNo, row.EffectiveFrom, row.EffectiveTo, row.ResponseTargetMinutes, row.ResolutionTargetMinutes, row.CalendarCode, row.WaitingRules, row.PauseRules, row.EscalationRules, row.BreachRules, row.NotificationRules, row.ServiceId, row.RequestTypeId, row.PriorityId, row.ClientId, row.DepartmentId, row.TeamId, row.TicketType, row.CategoryId, row.SubCategoryId);
    }

    public async Task CreateCalendarAsync(string code, BusinessCalendar calendar, long actor, string reason, CancellationToken ct)
        => await retry.ExecuteAsync(db, () => CreateCalendarOnceAsync(code, calendar, actor, reason, ct), ct);

    private async Task<object?> CreateCalendarOnceAsync(string code, BusinessCalendar calendar, long actor, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.SlaCalendars.AnyAsync(x => x.Code == code, ct)) throw new InvalidOperationException("The SLA calendar already exists and is immutable.");
        var hours = calendar.WorkingHours.ToDictionary(x => x.Key, x => new PersistentSlaCalendarStore.TimeWindow(x.Value.Start, x.Value.End));
        var id = (await db.SlaCalendars.Select(x => x.Id).DefaultIfEmpty().MaxAsync(ct)) + 1;
        db.SlaCalendars.Add(new SlaCalendarRow { Id = id, Code = code, Active = true, TimeZoneId = calendar.TimeZoneId, WorkingHoursJson = JsonSerializer.Serialize(hours), HolidaysJson = JsonSerializer.Serialize(calendar.Holidays), EffectiveFrom = DateTimeOffset.UtcNow });
        await AddAuditAsync(code, "SLA_CALENDAR_CREATED", actor, reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    private async Task AddAuditAsync(string id, string action, long actor, string reason, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, "SLA_POLICY", id, ct);
        var at = DateTimeOffset.UtcNow;
        var previous = await db.Audits.Where(x => x.EntityType == "SLA_POLICY" && x.EntityId == id).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var canonical = System.Text.Json.JsonSerializer.Serialize(new { entityType = "SLA_POLICY", entityId = id, action, actorUserId = actor, auditAtUtc = at.UtcDateTime, oldValue = (string?)null, newValue = (string?)null, reason, previousHash = previous });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow { EntityType = "SLA_POLICY", EntityId = id, Action = action, ActorId = actor, At = at, Reason = reason, Hash = hash, PreviousHash = previous });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }
}
