namespace PA.Tickets.Application.Reporting;

public enum ReportScheduleFrequency { Daily, Weekly, Monthly }
public enum ReportScheduleStatus { Active, Paused, Cancelled }
public sealed record ReportScheduleRecurrence(ReportScheduleFrequency Frequency, int LocalHour, int LocalMinute, DayOfWeek? DayOfWeek = null, int? DayOfMonth = null)
{
    public void Validate()
    {
        if (LocalHour is < 0 or > 23 || LocalMinute is < 0 or > 59) throw new ArgumentException("Schedule local time is invalid.");
        if (Frequency == ReportScheduleFrequency.Weekly && DayOfWeek is null) throw new ArgumentException("Weekly schedules require a day of week.");
        if (Frequency == ReportScheduleFrequency.Monthly && (DayOfMonth is < 1 or > 31)) throw new ArgumentException("Monthly schedules require a day from 1 to 31.");
        if (Frequency != ReportScheduleFrequency.Weekly && DayOfWeek is not null) throw new ArgumentException("Day of week is only valid for weekly schedules.");
        if (Frequency != ReportScheduleFrequency.Monthly && DayOfMonth is not null) throw new ArgumentException("Day of month is only valid for monthly schedules.");
    }
}
public sealed record ReportScheduleExecution(long Id, long ScheduleId, long? ExportId, string Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string CorrelationId);
public sealed record ReportSchedule(long Id, long OwnerUserId, string ReportCode, ReportExportFormat Format, ReportQueryRequest Filters, ReportScheduleRecurrence Recurrence, string TimeZone, DateTimeOffset NextRunAt, ReportScheduleStatus Status);
public interface IReportScheduleService
{
    Task<ReportSchedule> CreateAsync(long ownerUserId, string reportCode, ReportExportFormat format, ReportQueryRequest filters, ReportScheduleRecurrence recurrence, string timeZone, CancellationToken ct);
    Task<IReadOnlyCollection<ReportSchedule>> ListAsync(long actorUserId, CancellationToken ct);
    Task<ReportSchedule> GetAsync(long actorUserId, long scheduleId, CancellationToken ct);
    Task<ReportSchedule> UpdateAsync(long actorUserId, long scheduleId, ReportExportFormat format, ReportQueryRequest filters, ReportScheduleRecurrence recurrence, string timeZone, CancellationToken ct);
    Task<IReadOnlyCollection<ReportScheduleExecution>> ExecutionsAsync(long actorUserId, long scheduleId, CancellationToken ct);
    Task<ReportSchedule> SetStatusAsync(long actorUserId, long scheduleId, ReportScheduleStatus status, CancellationToken ct);
}