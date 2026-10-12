using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class Phase5FoundationStore(TicketsDbContext db) : IPhase5FoundationStore
{
    public async Task AddNotificationAsync(NotificationFoundation value, CancellationToken ct)
    {
        db.Notifications.Add(new NotificationRow { Id = value.Id, TicketId = value.TicketId, WorkItemId = value.WorkItemId, UserId = value.UserId, Type = value.Type, Channel = value.Channel, Subject = value.Subject, Message = value.Message, Status = value.Status, ScheduledAt = value.ScheduledAt, SentAt = value.SentAt, ReadAt = value.ReadAt, CreatedAt = value.CreatedAt, CorrelationId = value.CorrelationId, IdempotencyKey = value.IdempotencyKey });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddAttachmentAsync(AttachmentFoundation value, CancellationToken ct)
    {
        db.Attachments.Add(new AttachmentRow { Id = value.Id, TicketId = value.TicketId, WorkItemId = value.WorkItemId, Type = value.Type, FileName = value.FileName, ContentType = value.ContentType, FileSize = value.FileSize, StorageProvider = value.StorageProvider, StorageReference = value.StorageReference, Sha256Hash = value.Sha256Hash, Classification = value.Classification, Description = value.Description, CurrentVersion = value.CurrentVersion, Active = value.Active, UploadedAt = value.UploadedAt, UploadedBy = value.UploadedBy });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddAttachmentVersionAsync(AttachmentVersionFoundation value, CancellationToken ct)
    {
        AttachmentVersionPolicy.ValidateTransition("CURRENT", value.Status);
        db.AttachmentVersions.Add(new AttachmentVersionRow { Id = value.Id, AttachmentId = value.AttachmentId, VersionNo = value.VersionNo, FileName = value.FileName, ContentType = value.ContentType, FileSize = value.FileSize, StorageProvider = value.StorageProvider, StorageReference = value.StorageReference, Sha256Hash = value.Sha256Hash, Status = value.Status, ChangeReason = value.ChangeReason, UploadedAt = value.UploadedAt, UploadedBy = value.UploadedBy });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddEvidenceAsync(EvidenceFoundation value, CancellationToken ct)
    {
        db.Evidence.Add(new EvidenceRow { Id = value.Id, TicketId = value.TicketId, WorkItemId = value.WorkItemId, AttachmentId = value.AttachmentId, Code = value.Code, Type = value.Type, Description = value.Description, ControlReference = value.ControlReference, Status = value.Status, ValidFrom = value.ValidFrom, ReviewDueAt = value.ReviewDueAt, CreatedAt = value.CreatedAt, CreatedBy = value.CreatedBy });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddProblemAsync(ProblemFoundation value, CancellationToken ct)
    {
        db.Problems.Add(new ProblemRow { Id = value.Id, TicketId = value.TicketId, Statement = value.Statement, RootCause = value.RootCause, Workaround = value.Workaround, PermanentFix = value.PermanentFix, Status = value.Status, IdentifiedAt = value.IdentifiedAt, RootCauseIdentifiedAt = value.RootCauseIdentifiedAt, ResolvedAt = value.ResolvedAt, CreatedBy = value.CreatedBy, CreatedAt = value.CreatedAt });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddChangeAsync(ChangeFoundation value, CancellationToken ct)
    {
        db.Changes.Add(new ChangeRow { Id = value.Id, TicketId = value.TicketId, Reason = value.Reason, BusinessImpact = value.BusinessImpact, RiskAssessment = value.RiskAssessment, ImplementationPlan = value.ImplementationPlan, TestingPlan = value.TestingPlan, RollbackPlan = value.RollbackPlan, VerificationPlan = value.VerificationPlan, Status = value.Status, PlannedStartAt = value.PlannedStartAt, PlannedEndAt = value.PlannedEndAt, ActualStartAt = value.ActualStartAt, ActualEndAt = value.ActualEndAt, CreatedBy = value.CreatedBy, CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt, UpdatedBy = value.UpdatedBy });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddChangeImplementationAsync(ChangeImplementationFoundation value, CancellationToken ct)
    {
        db.ChangeImplementations.Add(new ChangeImplementationRow { Id = value.Id, ChangeId = value.ChangeId, StepNo = value.StepNo, StepName = value.StepName, Description = value.Description, Status = value.Status, ExecutedBy = value.ExecutedBy, StartedAt = value.StartedAt, CompletedAt = value.CompletedAt, Result = value.Result });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddEscalationAsync(EscalationFoundation value, CancellationToken ct)
    {
        db.Escalations.Add(new EscalationRow { Id = value.Id, TicketId = value.TicketId, WorkItemId = value.WorkItemId, Type = value.Type, Reason = value.Reason, FromUser = value.FromUser, ToUser = value.ToUser, ToDepartment = value.ToDepartment, EscalatedAt = value.EscalatedAt, ResolvedAt = value.ResolvedAt, CreatedBy = value.CreatedBy });
        await db.SaveChangesAsync(ct);
    }
}
