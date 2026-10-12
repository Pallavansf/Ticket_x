using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Application.Foundations;

public sealed record CreateAttachmentCommand(long TicketId,long? WorkItemId,string AttachmentType,string FileName,string? ContentType,string? Classification,string? Description,Stream Content,string IdempotencyKey,string? CorrelationId);
public sealed record AttachmentResult(Attachment Attachment,AttachmentVersion Version);
public sealed record CreateEvidenceCommand(long TicketId,long? WorkItemId,long? AttachmentId,string Code,string Type,string? Description,string? ControlReference,DateTimeOffset? ReviewDueAt,DateTimeOffset? RetentionUntil,string IdempotencyKey,string? CorrelationId);
public sealed record ReviewEvidenceCommand(string Status,string? Comment);

public interface IAttachmentService
{
    Task<AttachmentResult> CreateAsync(CreateAttachmentCommand command,long actor,CancellationToken ct);
    Task<AttachmentResult> CreateVersionAsync(long attachmentId,string fileName,string? contentType,Stream content,string changeReason,string idempotencyKey,string? correlationId,long actor,CancellationToken ct);
    Task<Attachment> GetAsync(long id,long actor,CancellationToken ct);
    Task<IReadOnlyCollection<AttachmentVersion>> VersionsAsync(long id,long actor,CancellationToken ct);
    Task<(AttachmentVersion Version,Stream Content)> DownloadAsync(long id,int version,long actor,CancellationToken ct);
    Task<Evidence> CreateEvidenceAsync(CreateEvidenceCommand command,long actor,CancellationToken ct);
    Task<Evidence> ReviewEvidenceAsync(long id,ReviewEvidenceCommand command,long actor,CancellationToken ct);
}
