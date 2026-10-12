using PA.Tickets.Infrastructure.Execution;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Reporting;

namespace PA.Tickets.Infrastructure.Reporting;

public sealed class ReportScheduleJobHandler(ExecutionDbContext db, IReportExportService exports, TimeProvider clock) : IBackgroundJobHandler
{
    public bool CanHandle(string type) => string.Equals(type, "REPORT_SCHEDULE", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(BackgroundJobEnvelope job, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ScheduleJobPayload>(job.Payload) ?? throw new InvalidOperationException("Invalid report schedule job payload.");
        var schedule = await db.ReportSchedules.SingleOrDefaultAsync(x => x.Id == payload.ScheduleId, ct) ?? throw new KeyNotFoundException("Scheduled report was not found.");
        if (!string.Equals(schedule.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) return;
        var key = job.DedupeKey;
        var execution = await db.ReportScheduleExecutions.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (execution is not null && string.Equals(execution.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)) return;
        if (execution is null)
        {
            execution = new ReportScheduleExecutionRow { ScheduleId = schedule.Id, Status = "RUNNING", StartedAt = clock.GetUtcNow(), CorrelationId = job.CorrelationId ?? key, IdempotencyKey = key };
            db.ReportScheduleExecutions.Add(execution);
        }
        else
        {
            execution.Status = "RUNNING"; execution.StartedAt ??= clock.GetUtcNow(); execution.FailureMessage = null;
        }
        await db.SaveChangesAsync(ct);
        try
        {
            var filters = JsonSerializer.Deserialize<ReportQueryRequest>(schedule.FiltersJson) ?? new();
            var format = Enum.Parse<ReportExportFormat>(schedule.Format, true);
            var export = await exports.CreateAsync(schedule.ReportCode, new(format, filters), schedule.OwnerUserId, execution.CorrelationId, ct);
            execution.ExportId = export.Id; execution.Status = "COMPLETED"; execution.CompletedAt = clock.GetUtcNow();
            var recurrence = new ReportScheduleRecurrence(Enum.Parse<ReportScheduleFrequency>(schedule.Frequency, true), schedule.LocalHour, schedule.LocalMinute, schedule.DayOfWeek is null ? null : (DayOfWeek)schedule.DayOfWeek, schedule.DayOfMonth);
            schedule.NextRunAt = ReportScheduleCalculator.Next(clock.GetUtcNow(), recurrence, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone));
            schedule.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            execution.Status = "FAILED"; execution.CompletedAt = clock.GetUtcNow(); execution.FailureMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
    private sealed record ScheduleJobPayload(long ScheduleId);
}