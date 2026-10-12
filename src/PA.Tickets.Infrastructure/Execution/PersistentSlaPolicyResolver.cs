using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Sla;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class PersistentSlaPolicyResolver(ExecutionDbContext db) : ISlaPolicyResolver
{
    public async Task<SlaPolicyVersion?> ResolveAsync(SlaEvaluationContext context, DateTimeOffset at, CancellationToken ct)
    {
        var candidates = await db.SlaPolicies.AsNoTracking()
            .Where(x => x.Active && x.Status == "PUBLISHED" && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at))
            .ToListAsync(ct);
        var matching = candidates.Where(x => Matches(x, context)).Select(x => new { Row = x, Specificity = Specificity(x) }).ToList();
        if (matching.Count == 0) return null;
        var best = matching.Max(x => x.Specificity);
        var winners = matching.Where(x => x.Specificity == best)
            .OrderByDescending(x => x.Row.EffectiveFrom)
            .ThenByDescending(x => x.Row.VersionNo)
            .ToList();
        if (winners.Select(x => x.Row.PolicyId).Distinct().Count() > 1)
            throw new InvalidOperationException("SLA policy configuration is ambiguous for the ticket context.");
        var row = winners[0].Row;
        return new(row.PolicyId, row.Code, row.VersionNo, row.EffectiveFrom, row.EffectiveTo,
            row.ResponseTargetMinutes, row.ResolutionTargetMinutes, row.CalendarCode, row.WaitingRules,
            row.PauseRules, row.EscalationRules, row.BreachRules, row.NotificationRules, row.ServiceId,
            row.RequestTypeId, row.PriorityId, row.ClientId, row.DepartmentId, row.TeamId,
            row.TicketType, row.CategoryId, row.SubCategoryId);
    }

    private static bool Matches(SlaPolicyRow row, SlaEvaluationContext context) =>
        (!row.ServiceId.HasValue || row.ServiceId == context.ServiceId) &&
        (!row.RequestTypeId.HasValue || row.RequestTypeId == context.RequestTypeId) &&
        (!row.PriorityId.HasValue || row.PriorityId == context.PriorityId) &&
        (!row.ClientId.HasValue || row.ClientId == context.ClientId) &&
        (!row.DepartmentId.HasValue || row.DepartmentId == context.DepartmentId) &&
        (!row.TeamId.HasValue || row.TeamId == context.TeamId) &&
        (!row.TicketType.HasValue || row.TicketType == context.TicketType) &&
        (!row.CategoryId.HasValue || row.CategoryId == context.CategoryId) &&
        (!row.SubCategoryId.HasValue || row.SubCategoryId == context.SubCategoryId);

    private static int Specificity(SlaPolicyRow row)
    {
        var dimensions = (row.ServiceId.HasValue ? 1 : 0) + (row.RequestTypeId.HasValue ? 1 : 0) +
            (row.PriorityId.HasValue ? 1 : 0) + (row.ClientId.HasValue ? 1 : 0) +
            (row.DepartmentId.HasValue ? 1 : 0) + (row.TeamId.HasValue ? 1 : 0) +
            (row.TicketType.HasValue ? 1 : 0) + (row.CategoryId.HasValue ? 1 : 0) +
            (row.SubCategoryId.HasValue ? 1 : 0);
        var precedence = (row.RequestTypeId.HasValue ? 1 << 8 : 0) +
            (row.SubCategoryId.HasValue ? 1 << 7 : 0) +
            (row.CategoryId.HasValue ? 1 << 6 : 0) +
            (row.ServiceId.HasValue ? 1 << 5 : 0) +
            (row.ClientId.HasValue ? 1 << 4 : 0) +
            (row.TicketType.HasValue ? 1 << 3 : 0) +
            (row.PriorityId.HasValue ? 1 << 2 : 0) +
            (row.DepartmentId.HasValue ? 1 << 1 : 0) +
            (row.TeamId.HasValue ? 1 : 0);
        return dimensions * (1 << 9) + precedence;
    }
}

public sealed class PersistentSlaCalendarStore(ExecutionDbContext db)
{
    public async Task<BusinessCalendar> ResolveAsync(string code, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var row = await db.SlaCalendars.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == code && x.Active && x.EffectiveFrom <= now &&
                (x.EffectiveTo == null || x.EffectiveTo > now), ct)
            ?? throw new KeyNotFoundException($"SLA calendar '{code}' was not found.");
        var hours = JsonSerializer.Deserialize<Dictionary<DayOfWeek, TimeWindow>>(row.WorkingHoursJson)
            ?? throw new InvalidOperationException($"SLA calendar '{code}' has invalid working hours.");
        var holidays = JsonSerializer.Deserialize<HashSet<DateOnly>>(row.HolidaysJson) ?? [];
        return new(row.TimeZoneId, hours.ToDictionary(x => x.Key, x => (x.Value.Start, x.Value.End)), holidays);
    }

    public sealed record TimeWindow(TimeSpan Start, TimeSpan End);
}

[Table("PA_TICKETS_SLA_POLICY")]
public sealed class SlaPolicyRow
{
    [Key]
    [Column("N_SLA_POLICY_VERSION_ID")]
    public long Id { get; set; }
    [Column("N_POLICY_ID")]
    public long PolicyId { get; set; }
    [Column("S_POLICY_CODE")]
    public string Code { get; set; } = "";
    [Column("N_VERSION_NO")]
    public int VersionNo { get; set; }
    [Column("N_ACTIVE")]
    public bool Active { get; set; }
    [Column("S_STATUS")]
    public string Status { get; set; } = "DRAFT";
    [Column("D_CREATED_AT")]
    public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")]
    public long CreatedBy { get; set; }
    [Column("D_PUBLISHED_AT")]
    public DateTimeOffset? PublishedAt { get; set; }
    [Column("N_PUBLISHED_BY")]
    public long? PublishedBy { get; set; }
    [Column("D_RETIRED_AT")]
    public DateTimeOffset? RetiredAt { get; set; }
    [Column("N_RETIRED_BY")]
    public long? RetiredBy { get; set; }
    [ConcurrencyCheck]
    [Column("N_ROW_VERSION")]
    public int RowVersion { get; set; }
    [Column("D_EFFECTIVE_FROM")]
    public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")]
    public DateTimeOffset? EffectiveTo { get; set; }
    [Column("N_SERVICE_ID")]
    public long? ServiceId { get; set; }
    [Column("N_REQUEST_TYPE_ID")]
    public long? RequestTypeId { get; set; }
    [Column("N_PRIORITY_ID")]
    public long? PriorityId { get; set; }
    [Column("N_CLIENT_ID")]
    public long? ClientId { get; set; }
    [Column("N_DEPARTMENT_ID")]
    public long? DepartmentId { get; set; }
    [Column("N_TEAM_ID")]
    public long? TeamId { get; set; }
    [Column("N_TICKET_TYPE")]
    public int? TicketType { get; set; }
    [Column("N_CATEGORY_ID")]
    public long? CategoryId { get; set; }
    [Column("N_SUB_CATEGORY_ID")]
    public long? SubCategoryId { get; set; }
    [Column("N_RESPONSE_TARGET_MINUTES")]
    public int? ResponseTargetMinutes { get; set; }
    [Column("N_RESOLUTION_TARGET_MINUTES")]
    public int? ResolutionTargetMinutes { get; set; }
    [Column("S_CALENDAR_CODE")]
    public string CalendarCode { get; set; } = "DEFAULT";
    [Column("S_WAITING_RULES")]
    public string? WaitingRules { get; set; }
    [Column("S_PAUSE_RULES")]
    public string? PauseRules { get; set; }
    [Column("S_ESCALATION_RULES")]
    public string? EscalationRules { get; set; }
    [Column("S_BREACH_RULES")]
    public string? BreachRules { get; set; }
    [Column("S_NOTIFICATION_RULES")]
    public string? NotificationRules { get; set; }
}

[Table("PA_TICKETS_SLA_CALENDAR")]
public sealed class SlaCalendarRow
{
    [Key]
    [Column("N_SLA_CALENDAR_ID")]
    public long Id { get; set; }
    [Column("S_CALENDAR_CODE")]
    public string Code { get; set; } = "";
    [Column("N_ACTIVE")]
    public bool Active { get; set; }
    [Column("S_TIME_ZONE_ID")]
    public string TimeZoneId { get; set; } = "UTC";
    [Column("S_WORKING_HOURS_JSON")]
    public string WorkingHoursJson { get; set; } = "{}";
    [Column("S_HOLIDAYS_JSON")]
    public string HolidaysJson { get; set; } = "[]";
    [Column("D_EFFECTIVE_FROM")]
    public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")]
    public DateTimeOffset? EffectiveTo { get; set; }
    [ConcurrencyCheck]
    [Column("N_ROW_VERSION")]
    public int RowVersion { get; set; }
}
