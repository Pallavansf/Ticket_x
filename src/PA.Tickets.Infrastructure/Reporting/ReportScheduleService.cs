using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Execution;

namespace PA.Tickets.Infrastructure.Reporting;

public sealed class ReportScheduleService(ExecutionDbContext db, IReportingQueryService reports, TimeProvider clock) : IReportScheduleService
{
    public async Task<ReportSchedule> CreateAsync(long ownerUserId, string reportCode, ReportExportFormat format, ReportQueryRequest filters, ReportScheduleRecurrence recurrence, string timeZone, CancellationToken ct)
    {
        reports.GetDefinition(reportCode);
        recurrence.Validate();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var now = clock.GetUtcNow();
        var row = new ReportScheduleRow { OwnerUserId = ownerUserId, ReportCode = reportCode, Format = format.ToString().ToUpperInvariant(), FiltersJson = JsonSerializer.Serialize(filters), Frequency = recurrence.Frequency.ToString().ToUpperInvariant(), LocalHour = recurrence.LocalHour, LocalMinute = recurrence.LocalMinute, DayOfWeek = recurrence.DayOfWeek is null ? null : (int)recurrence.DayOfWeek, DayOfMonth = recurrence.DayOfMonth, TimeZone = timeZone, NextRunAt = ReportScheduleCalculator.Next(now, recurrence, zone), Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
        db.ReportSchedules.Add(row);
        await db.SaveChangesAsync(ct);
        return ToContract(row, filters);
    }
    public async Task<IReadOnlyCollection<ReportSchedule>> ListAsync(long actorUserId, CancellationToken ct) => (await db.ReportSchedules.AsNoTracking().Where(x => x.OwnerUserId == actorUserId).OrderBy(x => x.Id).ToListAsync(ct)).Select(x => ToContract(x, JsonSerializer.Deserialize<ReportQueryRequest>(x.FiltersJson) ?? new())).ToArray();

    public async Task<ReportSchedule> UpdateAsync(long actorUserId, long scheduleId, ReportExportFormat format, ReportQueryRequest filters, ReportScheduleRecurrence recurrence, string timeZone, CancellationToken ct)
    {
        recurrence.Validate(); var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); var row = await db.ReportSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId && x.OwnerUserId == actorUserId, ct) ?? throw new KeyNotFoundException("The schedule was not found.");
        reports.GetDefinition(row.ReportCode); row.Format = format.ToString().ToUpperInvariant(); row.FiltersJson = JsonSerializer.Serialize(filters); row.Frequency = recurrence.Frequency.ToString().ToUpperInvariant(); row.LocalHour = recurrence.LocalHour; row.LocalMinute = recurrence.LocalMinute; row.DayOfWeek = recurrence.DayOfWeek is null ? null : (int)recurrence.DayOfWeek; row.DayOfMonth = recurrence.DayOfMonth; row.TimeZone = timeZone; row.NextRunAt = ReportScheduleCalculator.Next(clock.GetUtcNow(), recurrence, zone); row.UpdatedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct); return ToContract(row, filters);
    }

    public async Task<IReadOnlyCollection<ReportScheduleExecution>> ExecutionsAsync(long actorUserId, long scheduleId, CancellationToken ct)
    {
        if (!await db.ReportSchedules.AnyAsync(x => x.Id == scheduleId && x.OwnerUserId == actorUserId, ct)) throw new KeyNotFoundException("The schedule was not found.");
        return await db.ReportScheduleExecutions.AsNoTracking().Where(x => x.ScheduleId == scheduleId).OrderByDescending(x => x.Id).Select(x => new ReportScheduleExecution(x.Id, x.ScheduleId, x.ExportId, x.Status, x.StartedAt, x.CompletedAt, x.CorrelationId)).ToArrayAsync(ct);
    }

    public async Task<ReportSchedule> GetAsync(long actorUserId, long scheduleId, CancellationToken ct)
    {
        var row = await db.ReportSchedules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == scheduleId && x.OwnerUserId == actorUserId, ct) ?? throw new KeyNotFoundException("The schedule was not found.");
        return ToContract(row, JsonSerializer.Deserialize<ReportQueryRequest>(row.FiltersJson) ?? new());
    }
    public async Task<ReportSchedule> SetStatusAsync(long actorUserId, long scheduleId, ReportScheduleStatus status, CancellationToken ct)
    {
        var row = await db.ReportSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId && x.OwnerUserId == actorUserId, ct) ?? throw new KeyNotFoundException("The schedule was not found.");
        row.Status = status.ToString().ToUpperInvariant(); row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return ToContract(row, JsonSerializer.Deserialize<ReportQueryRequest>(row.FiltersJson) ?? new());
    }
    private static ReportSchedule ToContract(ReportScheduleRow row, ReportQueryRequest filters) => new(row.Id, row.OwnerUserId, row.ReportCode, Enum.Parse<ReportExportFormat>(row.Format, true), filters, new(Enum.Parse<ReportScheduleFrequency>(row.Frequency, true), row.LocalHour, row.LocalMinute, row.DayOfWeek is null ? null : (DayOfWeek)row.DayOfWeek, row.DayOfMonth), row.TimeZone, row.NextRunAt, Enum.Parse<ReportScheduleStatus>(row.Status, true));
}