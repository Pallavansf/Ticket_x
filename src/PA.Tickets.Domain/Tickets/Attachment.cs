namespace PA.Tickets.Domain.Tickets;

public sealed record Attachment(long Id,long TicketId,long? WorkItemId,string Type,string FileName,string? ContentType,long ContentLength,string StorageProvider,string StorageObjectKey,string Sha256Hash,int CurrentVersion,bool Active,DateTimeOffset UploadedAt,long UploadedBy);
public sealed record AttachmentVersion(long Id,long AttachmentId,int VersionNo,string FileName,string? ContentType,long ContentLength,string StorageProvider,string StorageObjectKey,string Sha256Hash,string Status,string? ChangeReason,DateTimeOffset UploadedAt,long UploadedBy);
public sealed record Evidence(long Id,long TicketId,long? WorkItemId,long? AttachmentId,string Code,string Type,string? Description,string? ControlReference,string Status,DateTimeOffset? ValidFrom,DateTimeOffset? ReviewDueAt,long? ReviewedBy,DateTimeOffset? ReviewedAt,string? ReviewComment,DateTimeOffset CreatedAt,long CreatedBy,DateTimeOffset? RetentionUntil,string? DisposalReason);

public static class EvidencePolicy
{
    private static readonly IReadOnlyDictionary<string,string[]> Allowed = new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["CREATED"] = ["UNDER_REVIEW", "RETIRED"],
        ["UNDER_REVIEW"] = ["ACCEPTED", "REJECTED", "EXPIRED", "RETIRED"],
        ["ACCEPTED"] = ["EXPIRED", "RETIRED"],
        ["REJECTED"] = ["UNDER_REVIEW", "RETIRED"],
        ["EXPIRED"] = ["RETIRED"],
        ["RETIRED"] = []
    };

    public static void ValidateTransition(string current, string next)
    {
        if (!Allowed.TryGetValue(current, out var transitions) || !transitions.Contains(next, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Evidence cannot transition from {current} to {next}.");
    }
}
