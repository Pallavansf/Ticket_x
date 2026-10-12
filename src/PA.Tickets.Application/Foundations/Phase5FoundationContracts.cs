namespace PA.Tickets.Application.Foundations;

public sealed record NotificationFoundation(long Id,long? TicketId,long? WorkItemId,long? UserId,string Type,string Channel,string? Subject,string? Message,string Status,DateTimeOffset? ScheduledAt,DateTimeOffset? SentAt,DateTimeOffset? ReadAt,DateTimeOffset CreatedAt,string? CorrelationId,string? IdempotencyKey);
public sealed record AttachmentFoundation(long Id,long TicketId,long? WorkItemId,string Type,string FileName,string? ContentType,long? FileSize,string? StorageProvider,string? StorageReference,string Sha256Hash,string? Classification,string? Description,int CurrentVersion,bool Active,DateTimeOffset UploadedAt,long UploadedBy);
public sealed record AttachmentVersionFoundation(long Id,long AttachmentId,int VersionNo,string FileName,string? ContentType,long? FileSize,string? StorageProvider,string? StorageReference,string Sha256Hash,string Status,string? ChangeReason,DateTimeOffset UploadedAt,long UploadedBy);
public sealed record EvidenceFoundation(long Id,long TicketId,long? WorkItemId,long? AttachmentId,string Code,string Type,string? Description,string? ControlReference,string Status,DateTimeOffset? ValidFrom,DateTimeOffset? ReviewDueAt,DateTimeOffset CreatedAt,long CreatedBy);
public sealed record ProblemFoundation(long Id,long TicketId,string? Statement,string? RootCause,string? Workaround,string? PermanentFix,string Status,DateTimeOffset? IdentifiedAt,DateTimeOffset? RootCauseIdentifiedAt,DateTimeOffset? ResolvedAt,long CreatedBy,DateTimeOffset CreatedAt);
public sealed record ChangeFoundation(long Id,long TicketId,string? Reason,string? BusinessImpact,string? RiskAssessment,string? ImplementationPlan,string? TestingPlan,string? RollbackPlan,string? VerificationPlan,string Status,DateTimeOffset? PlannedStartAt,DateTimeOffset? PlannedEndAt,DateTimeOffset? ActualStartAt,DateTimeOffset? ActualEndAt,long CreatedBy,DateTimeOffset CreatedAt,DateTimeOffset? UpdatedAt,long? UpdatedBy);
public sealed record ChangeImplementationFoundation(long Id,long ChangeId,int StepNo,string StepName,string? Description,string Status,long? ExecutedBy,DateTimeOffset? StartedAt,DateTimeOffset? CompletedAt,string? Result);
public sealed record EscalationFoundation(long Id,long TicketId,long? WorkItemId,string Type,string? Reason,long? FromUser,long? ToUser,long? ToDepartment,DateTimeOffset EscalatedAt,DateTimeOffset? ResolvedAt,long CreatedBy);

public interface IPhase5FoundationStore
{
    Task AddNotificationAsync(NotificationFoundation value,CancellationToken ct);
    Task AddAttachmentAsync(AttachmentFoundation value,CancellationToken ct);
    Task AddAttachmentVersionAsync(AttachmentVersionFoundation value,CancellationToken ct);
    Task AddEvidenceAsync(EvidenceFoundation value,CancellationToken ct);
    Task AddProblemAsync(ProblemFoundation value,CancellationToken ct);
    Task AddChangeAsync(ChangeFoundation value,CancellationToken ct);
    Task AddChangeImplementationAsync(ChangeImplementationFoundation value,CancellationToken ct);
    Task AddEscalationAsync(EscalationFoundation value,CancellationToken ct);
}
