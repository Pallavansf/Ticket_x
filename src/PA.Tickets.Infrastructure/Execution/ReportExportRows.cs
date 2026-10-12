using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Execution;

[Table("PA_TICKETS_REPORT_EXPORT")]
public sealed class ReportExportRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("N_EXPORT_ID")] public long Id { get; set; }
    [Column("S_REPORT_CODE")] public string ReportCode { get; set; } = "";
    [Column("S_FORMAT")] public string Format { get; set; } = "CSV";
    [Column("N_REQUESTED_BY")] public long RequestedBy { get; set; }
    [Column("S_FILTERS_JSON")] public string FiltersJson { get; set; } = "{}";
    [Column("S_STATUS")] public string Status { get; set; } = "QUEUED";
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_STARTED_AT")] public DateTimeOffset? StartedAt { get; set; }
    [Column("D_COMPLETED_AT")] public DateTimeOffset? CompletedAt { get; set; }
    [Column("N_ROW_COUNT")] public int? RowCount { get; set; }
    [Column("N_FILE_SIZE")] public long? FileSize { get; set; }
    [Column("S_CONTENT_TYPE")] public string? ContentType { get; set; }
    [Column("S_FILE_HASH")] public string? FileHash { get; set; }
    [Column("S_STORAGE_KEY")] public string? StorageKey { get; set; }
    [Column("S_FAILURE_CLASSIFICATION")] public string? FailureClassification { get; set; }
    [Column("S_FAILURE_MESSAGE")] public string? FailureMessage { get; set; }
    [Column("S_CORRELATION_ID")] public string? CorrelationId { get; set; }
    [Column("D_EXPIRES_AT")] public DateTimeOffset? ExpiresAt { get; set; }
}

[Table("PA_TICKETS_REPORT_SCHEDULE")]
public sealed class ReportScheduleRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("N_SCHEDULE_ID")] public long Id { get; set; }
    [Column("N_OWNER_USER_ID")] public long OwnerUserId { get; set; }
    [Column("S_REPORT_CODE")] public string ReportCode { get; set; } = "";
    [Column("S_FORMAT")] public string Format { get; set; } = "CSV";
    [Column("S_FILTERS_JSON")] public string FiltersJson { get; set; } = "{}";
    [Column("S_FREQUENCY")] public string Frequency { get; set; } = "DAILY";
    [Column("N_LOCAL_HOUR")] public int LocalHour { get; set; }
    [Column("N_LOCAL_MINUTE")] public int LocalMinute { get; set; }
    [Column("N_DAY_OF_WEEK")] public int? DayOfWeek { get; set; }
    [Column("N_DAY_OF_MONTH")] public int? DayOfMonth { get; set; }
    [Column("S_TIME_ZONE")] public string TimeZone { get; set; } = "Asia/Kolkata";
    [Column("D_NEXT_RUN_AT")] public DateTimeOffset NextRunAt { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "ACTIVE";
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset UpdatedAt { get; set; }
}

[Table("PA_TICKETS_REPORT_SCHEDULE_EXECUTION")]
public sealed class ReportScheduleExecutionRow
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("N_EXECUTION_ID")] public long Id { get; set; }
    [Column("N_SCHEDULE_ID")] public long ScheduleId { get; set; }
    [Column("N_EXPORT_ID")] public long? ExportId { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "QUEUED";
    [Column("D_STARTED_AT")] public DateTimeOffset? StartedAt { get; set; }
    [Column("D_COMPLETED_AT")] public DateTimeOffset? CompletedAt { get; set; }
    [Column("S_CORRELATION_ID")] public string CorrelationId { get; set; } = "";
    [Column("S_IDEMPOTENCY_KEY")] public string IdempotencyKey { get; set; } = "";
    [Column("S_FAILURE_MESSAGE")] public string? FailureMessage { get; set; }
}