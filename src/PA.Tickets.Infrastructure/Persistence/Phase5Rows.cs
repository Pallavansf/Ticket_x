using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Persistence;

[Table("PA_TICKETS_NOTIFICATION")]
public sealed class NotificationRow
{
    [Key, Column("N_NOTIFICATION_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long? TicketId { get; set; }
    [Column("N_WORK_ITEM_ID")] public long? WorkItemId { get; set; }
    [Column("N_USER_ID")] public long? UserId { get; set; }
    [Column("S_NOTIFICATION_TYPE")] public string Type { get; set; } = "";
    [Column("S_CHANNEL")] public string Channel { get; set; } = "";
    [Column("S_SUBJECT")] public string? Subject { get; set; }
    [Column("S_MESSAGE")] public string? Message { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "PENDING";
    [Column("D_SCHEDULED_AT")] public DateTimeOffset? ScheduledAt { get; set; }
    [Column("D_SENT_AT")] public DateTimeOffset? SentAt { get; set; }
    [Column("D_READ_AT")] public DateTimeOffset? ReadAt { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("S_CORRELATION_ID")] public string? CorrelationId { get; set; }
    [Column("S_IDEMPOTENCY_KEY")] public string? IdempotencyKey { get; set; }
}

[Table("PA_TICKETS_NOTIFICATION_RULE")]
public sealed class NotificationRuleRow
{
    [Key, Column("N_NOTIFICATION_RULE_ID")] public long Id { get; set; }
    [Column("S_RULE_CODE")] public string Code { get; set; } = "";
    [Column("S_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("S_EVENT_TYPE")] public string EventType { get; set; } = "";
    [Column("N_ENABLED")] public bool Enabled { get; set; } = true;
    [Column("N_PRIORITY")] public int Priority { get; set; }
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_VERSION")] public int Version { get; set; } = 1;
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
    public ICollection<NotificationRuleVersionRow> Versions { get; set; } = new List<NotificationRuleVersionRow>();
}

[Table("PA_TICKETS_NOTIFICATION_RULE_VERSION")]
public sealed class NotificationRuleVersionRow
{
    [Key, Column("N_NOTIFICATION_RULE_VERSION_ID")] public long Id { get; set; }
    [Column("N_RULE_ID")] public long RuleId { get; set; }
    public NotificationRuleRow? Rule { get; set; }
    [Column("N_VERSION_NO")] public int VersionNo { get; set; }
    [Column("S_EVENT_TYPE")] public string EventType { get; set; } = "";
    [Column("N_PRIORITY")] public int Priority { get; set; }
    [Column("S_CONDITION")] public string? Condition { get; set; }
    [Column("S_RECIPIENT_DEFINITION")] public string? RecipientDefinition { get; set; }
    [Column("S_CHANNEL_DEFINITION")] public string? ChannelDefinition { get; set; }
    [Column("S_TEMPLATE_CODE")] public string? TemplateCode { get; set; }
    [Column("S_DEDUPLICATION_SCOPE")] public string DeduplicationScope { get; set; } = "PER_EVENT";
    [Column("N_ENABLED")] public bool Enabled { get; set; } = true;
    [Column("S_STATUS")] public string Status { get; set; } = "PUBLISHED";
    [Column("D_EFFECTIVE_FROM")] public DateTimeOffset EffectiveFrom { get; set; }
    [Column("D_EFFECTIVE_TO")] public DateTimeOffset? EffectiveTo { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_PUBLISHED_AT")] public DateTimeOffset? PublishedAt { get; set; }
    [Column("N_PUBLISHED_BY")] public long? PublishedBy { get; set; }
    [Column("D_RETIRED_AT")] public DateTimeOffset? RetiredAt { get; set; }
    [Column("N_RETIRED_BY")] public long? RetiredBy { get; set; }
    [ConcurrencyCheck, Column("N_ROW_VERSION")] public int RowVersion { get; set; }
}

[Table("PA_TICKETS_NOTIFICATION_EXECUTION")]
public sealed class NotificationExecutionRow
{
    [Key, Column("N_NOTIFICATION_EXECUTION_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("N_EVENT_ID")] public long EventId { get; set; }
    [Column("N_RULE_ID")] public long? RuleId { get; set; }
    [Column("N_RULE_VERSION_ID")] public long? RuleVersionId { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "PENDING";
    [Column("D_EVALUATED_AT")] public DateTimeOffset EvaluatedAt { get; set; }
    [Column("N_MATCHED")] public bool Matched { get; set; }
    [Column("S_SKIP_REASON")] public string? SkipReason { get; set; }
    [Column("N_CREATED_COMMUNICATION_COUNT")] public int CreatedCommunicationCount { get; set; }
    [Column("S_CORRELATION_ID")] public string? CorrelationId { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_COMPLETED_AT")] public DateTimeOffset? CompletedAt { get; set; }
    [Column("S_FAILURE_CODE")] public string? FailureCode { get; set; }
    [Column("S_FAILURE_REASON")] public string? FailureReason { get; set; }
    [Column("S_RULE_CODE")] public string? RuleCode { get; set; }
    [Column("S_TEMPLATE_CODE")] public string? TemplateCode { get; set; }
    [Column("S_SELECTED_CHANNEL")] public string? SelectedChannel { get; set; }
    [Column("S_DEDUPE_KEY")] public string? DedupeKey { get; set; }
    [Column("S_NOTIFICATION_CONTEXT")] public string? NotificationContext { get; set; }
}

[Table("PA_TICKETS_ATTACHMENT")]
public sealed class AttachmentRow
{
    [Key, Column("N_ATTACHMENT_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("N_WORK_ITEM_ID")] public long? WorkItemId { get; set; }
    [Column("S_ATTACHMENT_TYPE")] public string Type { get; set; } = "";
    [Column("S_FILE_NAME")] public string FileName { get; set; } = "";
    [Column("S_CONTENT_TYPE")] public string? ContentType { get; set; }
    [Column("N_FILE_SIZE")] public long? FileSize { get; set; }
    [Column("S_STORAGE_PROVIDER")] public string? StorageProvider { get; set; }
    [Column("S_STORAGE_REFERENCE")] public string? StorageReference { get; set; }
    [Column("S_SHA256_HASH")] public string Sha256Hash { get; set; } = "";
    [Column("S_CLASSIFICATION")] public string? Classification { get; set; }
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("N_CURRENT_VERSION")] public int CurrentVersion { get; set; } = 1;
    [Column("N_ACTIVE")] public bool Active { get; set; } = true;
    [Column("D_UPLOADED_AT")] public DateTimeOffset UploadedAt { get; set; }
    [Column("N_UPLOADED_BY")] public long UploadedBy { get; set; }
    [Column("D_RETENTION_UNTIL")] public DateTimeOffset? RetentionUntil { get; set; }
    [Column("S_DISPOSAL_REASON")] public string? DisposalReason { get; set; }
    [Column("S_IDEMPOTENCY_KEY")] public string? IdempotencyKey { get; set; }
}

[Table("PA_TICKETS_ATTACHMENT_VERSION")]
public sealed class AttachmentVersionRow
{
    [Key, Column("N_ATTACHMENT_VERSION_ID")] public long Id { get; set; }
    [Column("N_ATTACHMENT_ID")] public long AttachmentId { get; set; }
    [Column("N_VERSION_NO")] public int VersionNo { get; set; }
    [Column("S_FILE_NAME")] public string FileName { get; set; } = "";
    [Column("S_CONTENT_TYPE")] public string? ContentType { get; set; }
    [Column("N_FILE_SIZE")] public long? FileSize { get; set; }
    [Column("S_STORAGE_PROVIDER")] public string? StorageProvider { get; set; }
    [Column("S_STORAGE_REFERENCE")] public string? StorageReference { get; set; }
    [Column("S_SHA256_HASH")] public string Sha256Hash { get; set; } = "";
    [Column("S_VERSION_STATUS")] public string Status { get; set; } = "CURRENT";
    [Column("S_CHANGE_REASON")] public string? ChangeReason { get; set; }
    [Column("D_UPLOADED_AT")] public DateTimeOffset UploadedAt { get; set; }
    [Column("N_UPLOADED_BY")] public long UploadedBy { get; set; }
    [Column("S_IDEMPOTENCY_KEY")] public string? IdempotencyKey { get; set; }
}

[Table("PA_TICKETS_EVIDENCE")]
public sealed class EvidenceRow
{
    [Key, Column("N_EVIDENCE_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("N_WORK_ITEM_ID")] public long? WorkItemId { get; set; }
    [Column("N_ATTACHMENT_ID")] public long? AttachmentId { get; set; }
    [Column("S_EVIDENCE_CODE")] public string Code { get; set; } = "";
    [Column("S_EVIDENCE_TYPE")] public string Type { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("S_CONTROL_REFERENCE")] public string? ControlReference { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "VALID";
    [Column("D_VALID_FROM")] public DateTimeOffset? ValidFrom { get; set; }
    [Column("D_REVIEW_DUE_AT")] public DateTimeOffset? ReviewDueAt { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("N_REVIEWED_BY")] public long? ReviewedBy { get; set; }
    [Column("D_REVIEWED_AT")] public DateTimeOffset? ReviewedAt { get; set; }
    [Column("S_REVIEW_COMMENT")] public string? ReviewComment { get; set; }
    [Column("D_RETENTION_UNTIL")] public DateTimeOffset? RetentionUntil { get; set; }
    [Column("S_DISPOSAL_REASON")] public string? DisposalReason { get; set; }
}

[Table("PA_TICKETS_PROBLEM")]
public sealed class ProblemRow
{
    [Key, Column("N_PROBLEM_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("S_PROBLEM_STATEMENT")] public string? Statement { get; set; }
    [Column("S_ROOT_CAUSE")] public string? RootCause { get; set; }
    [Column("S_WORKAROUND")] public string? Workaround { get; set; }
    [Column("S_PERMANENT_FIX")] public string? PermanentFix { get; set; }
    [Column("S_PROBLEM_STATUS")] public string Status { get; set; } = "OPEN";
    [Column("D_IDENTIFIED_AT")] public DateTimeOffset? IdentifiedAt { get; set; }
    [Column("N_IDENTIFIED_BY")] public long? IdentifiedBy { get; set; }
    [Column("D_ROOT_CAUSE_IDENTIFIED_AT")] public DateTimeOffset? RootCauseIdentifiedAt { get; set; }
    [Column("N_ROOT_CAUSE_IDENTIFIED_BY")] public long? RootCauseIdentifiedBy { get; set; }
    [Column("N_KNOWN_ERROR")] public bool IsKnownError { get; set; }
    [Column("D_KNOWN_ERROR_DECLARED_AT")] public DateTimeOffset? KnownErrorDeclaredAt { get; set; }
    [Column("N_KNOWN_ERROR_DECLARED_BY")] public long? KnownErrorDeclaredBy { get; set; }
    [Column("D_RESOLVED_AT")] public DateTimeOffset? ResolvedAt { get; set; }
    [Column("N_RESOLVED_BY")] public long? ResolvedBy { get; set; }
    [Column("S_RESOLUTION_SUMMARY")] public string? ResolutionSummary { get; set; }
    [Column("D_CLOSED_AT")] public DateTimeOffset? ClosedAt { get; set; }
    [Column("N_CLOSED_BY")] public long? ClosedBy { get; set; }
    [Column("D_REOPENED_AT")] public DateTimeOffset? ReopenedAt { get; set; }
    [Column("N_REOPENED_BY")] public long? ReopenedBy { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset UpdatedAt { get; set; }
    [Column("S_IDEMPOTENCY_KEY")] public string? IdempotencyKey { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
}

[Table("PA_TICKETS_CHANGE")]
public sealed class ChangeRow
{
    [Key, Column("N_CHANGE_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("S_CHANGE_TYPE")] public string ChangeType { get; set; } = "NORMAL";
    [Column("S_IMPACT")] public string Impact { get; set; } = "MEDIUM";
    [Column("S_RISK")] public string Risk { get; set; } = "MEDIUM";
    [Column("N_RISK_SCORE")] public int RiskScore { get; set; }
    [Column("S_CHANGE_REASON")] public string? Reason { get; set; }
    [Column("S_BUSINESS_IMPACT")] public string? BusinessImpact { get; set; }
    [Column("S_RISK_ASSESSMENT")] public string? RiskAssessment { get; set; }
    [Column("S_IMPLEMENTATION_PLAN")] public string? ImplementationPlan { get; set; }
    [Column("S_TESTING_PLAN")] public string? TestingPlan { get; set; }
    [Column("S_ROLLBACK_PLAN")] public string? RollbackPlan { get; set; }
    [Column("S_VERIFICATION_PLAN")] public string? VerificationPlan { get; set; }
    [Column("S_SUCCESS_CRITERIA")] public string? SuccessCriteria { get; set; }
    [Column("S_IMPLEMENTATION_RESULT")] public string? ImplementationResult { get; set; }
    [Column("S_FAILURE_REASON")] public string? FailureReason { get; set; }
    [Column("S_ROLLBACK_RESULT")] public string? RollbackResult { get; set; }
    [Column("S_CHANGE_STATUS")] public string Status { get; set; } = "DRAFT";
    [Column("D_PLANNED_START_AT")] public DateTimeOffset? PlannedStartAt { get; set; }
    [Column("D_PLANNED_END_AT")] public DateTimeOffset? PlannedEndAt { get; set; }
    [Column("D_ACTUAL_START_AT")] public DateTimeOffset? ActualStartAt { get; set; }
    [Column("D_ACTUAL_END_AT")] public DateTimeOffset? ActualEndAt { get; set; }
    [Column("D_RESOLVED_AT")] public DateTimeOffset? ResolvedAt { get; set; }
    [Column("D_CLOSED_AT")] public DateTimeOffset? ClosedAt { get; set; }
    [Column("D_REOPENED_AT")] public DateTimeOffset? ReopenedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long? UpdatedBy { get; set; }
    [Column("S_IDEMPOTENCY_KEY")] public string? IdempotencyKey { get; set; }
}

[Table("PA_TICKETS_CHANGE_IMPLEMENTATION")]
public sealed class ChangeImplementationRow
{
    [Key, Column("N_CHANGE_IMPLEMENTATION_ID")] public long Id { get; set; }
    [Column("N_CHANGE_ID")] public long ChangeId { get; set; }
    [Column("N_STEP_NO")] public int StepNo { get; set; }
    [Column("S_STEP_NAME")] public string StepName { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "PENDING";
    [Column("N_EXECUTED_BY")] public long? ExecutedBy { get; set; }
    [Column("D_STARTED_AT")] public DateTimeOffset? StartedAt { get; set; }
    [Column("D_COMPLETED_AT")] public DateTimeOffset? CompletedAt { get; set; }
    [Column("S_RESULT")] public string? Result { get; set; }
}

[Table("PA_TICKETS_ESCALATION")]
public sealed class EscalationRow
{
    [Key, Column("N_ESCALATION_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("N_WORK_ITEM_ID")] public long? WorkItemId { get; set; }
    [Column("S_ESCALATION_TYPE")] public string Type { get; set; } = "";
    [Column("S_REASON")] public string? Reason { get; set; }
    [Column("N_ESCALATED_FROM_USER")] public long? FromUser { get; set; }
    [Column("N_ESCALATED_TO_USER")] public long? ToUser { get; set; }
    [Column("N_ESCALATED_TO_DEPARTMENT")] public long? ToDepartment { get; set; }
    [Column("D_ESCALATED_AT")] public DateTimeOffset EscalatedAt { get; set; }
    [Column("D_RESOLVED_AT")] public DateTimeOffset? ResolvedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
}
