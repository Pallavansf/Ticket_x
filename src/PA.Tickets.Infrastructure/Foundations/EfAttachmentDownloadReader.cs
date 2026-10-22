using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class EfAttachmentDownloadReader(TicketsDbContext db) : IAttachmentDownloadReader
{
    public async Task<AttachmentDownloadOwnerFact> ReadOwnerAsync(long attachmentId, CancellationToken ct)
    {
        var attachment = await db.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attachmentId, ct)
            ?? throw new KeyNotFoundException("Attachment was not found.");
        var ticket = await db.Tickets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attachment.TicketId, ct)
            ?? throw new KeyNotFoundException("Ticket was not found.");
        return new AttachmentDownloadOwnerFact(attachment.Id, attachment.TicketId, ToTicket(ticket));
    }

    public async Task<AttachmentDownloadVersionFact> ReadVersionAsync(long attachmentId, int version, CancellationToken ct)
    {
        var row = await db.AttachmentVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AttachmentId == attachmentId && x.VersionNo == version, ct)
            ?? throw new KeyNotFoundException("Attachment version was not found.");
        if (string.IsNullOrWhiteSpace(row.StorageReference) || string.IsNullOrWhiteSpace(row.Sha256Hash))
            throw new KeyNotFoundException("Attachment version was not found.");
        return new AttachmentDownloadVersionFact(
            row.Id,
            row.AttachmentId,
            row.VersionNo,
            row.FileName,
            row.ContentType,
            row.FileSize ?? 0,
            row.StorageProvider ?? "",
            row.StorageReference,
            row.Sha256Hash,
            row.Status,
            row.ChangeReason,
            row.UploadedAt,
            row.UploadedBy);
    }

    private static Ticket ToTicket(TicketRow row) => Ticket.Restore(
        row.Id,
        row.Number,
        (TicketType)row.TypeId,
        (TicketStatus)row.StatusId,
        row.RequestedBy,
        row.CreatedBy,
        row.Subject,
        row.Description,
        row.ServiceId,
        row.CategoryId,
        row.RequestTypeId,
        row.PriorityId,
        row.CurrentDepartmentId,
        row.CurrentTeamId,
        row.CurrentAssignee,
        row.CreatedAt,
        row.ResolvedAt,
        row.ClosedAt,
        row.ApprovalRequired == 1,
        row.Approved == 1,
        row.EvidenceCount,
        row.LastExecutor,
        [],
        row.ClientId,
        null,
        null,
        row.ApprovalApproverUserId,
        row.RowVersion);
}
