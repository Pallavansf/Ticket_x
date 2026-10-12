using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public class CommunicationStore(
    TicketsDbContext db,
    IRecipientResolver recipients,
    CommunicationTemplateRenderer renderer,
    AuditChainCoordinator auditLocks,
    ITicketAccessAuthorizer authorizer, IConfiguration configuration) : ICommunicationStore
{
    public async Task<CommunicationMessage> QueueAsync(QueueCommunicationCommand command,long actor,CancellationToken ct)
    {
        if (actor <= 0 || string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new ArgumentException("Actor and communication idempotency key are required.");
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == command.TicketId, ct) ?? throw new KeyNotFoundException("Ticket was not found.");
        await authorizer.EnsureCanMutateAsync(actor, ToTicket(ticket), ct);
        var recipient = await recipients.ResolveAsync(command, actor, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (await db.Communications.AnyAsync(x => x.IdempotencyKey == command.IdempotencyKey, ct)) throw new InvalidOperationException("Communication request was already queued.");
        var now = DateTimeOffset.UtcNow;
        var subject = command.Subject;
        var body = command.Body;
        long? templateId = null;
        if (!string.IsNullOrWhiteSpace(command.TemplateCode))
        {
            var template = await db.CommunicationTemplates.AsNoTracking().Where(x => x.Code == command.TemplateCode && x.Channel == command.Channel.ToString().ToUpperInvariant() && (x.Status == "ACTIVE" || x.Status == "PUBLISHED") && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now)).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (template is null)
            {
                if (command.Body is null) throw new ArgumentException("The requested communication template is not active.");
            }
            else
            {
                templateId = template.Id;
                subject = renderer.Render(template.Subject ?? "", command.TemplateValues);
                body = renderer.Render(template.Body, command.TemplateValues);
            }
        }
        var publicId = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var row = new CommunicationRow { TicketId = command.TicketId, WorkItemId = command.WorkItemId, PublicId = publicId, CommunicationType = "SYSTEM", Channel = command.Channel.ToString().ToUpperInvariant(), ProviderCode = command.ProviderCode, TemplateCode = command.TemplateCode, TemplateVersionId = templateId, Status = "QUEUED", Recipient = recipient, Subject = subject, Body = body, StructuredPayload = command.StructuredPayload, RetryCount = 0, NextRetryAt = command.ScheduledAt ?? now, ScheduledAt = command.ScheduledAt, CommunicationAt = now, RecordedAt = now, RecordedBy = actor, CorrelationId = command.CorrelationId, IdempotencyKey = command.IdempotencyKey };
        db.Communications.Add(row);
        await AddAudit("COMMUNICATION", publicId, "COMMUNICATION_CREATED", actor, command.CorrelationId, ct);
        db.Events.Add(new EventRow { TicketId = command.TicketId, Type = "COMMUNICATION_QUEUED", Category = "COMMUNICATION", ActorId = actor, NewValue = "QUEUED", Reason = command.CorrelationId, At = now });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new InvalidOperationException("Communication request was already queued."); }
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<CommunicationView> GetAsync(string publicId,long actor,CancellationToken ct)
    {
        var row = await db.Communications.AsNoTracking().SingleOrDefaultAsync(x => x.PublicId == publicId, ct) ?? throw new KeyNotFoundException("Communication was not found.");
        await EnsureCanAccessAsync(actor, row.TicketId, ct);
        var attempt = await db.CommunicationAttempts.AsNoTracking().Where(x => x.CommunicationId == row.Id).OrderByDescending(x => x.AttemptNumber).FirstOrDefaultAsync(ct);
        return MapView(row, attempt);
    }

    public async Task<IReadOnlyCollection<CommunicationView>> ForTicketAsync(long ticketId,long actor,CancellationToken ct)
    {
        await EnsureCanAccessAsync(actor, ticketId, ct);
        var rows = await db.Communications.AsNoTracking().Where(x => x.TicketId == ticketId).OrderByDescending(x => x.CommunicationAt).ThenByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var attempts = new List<CommunicationAttemptRow>();
        if (ids.Length > 0)
        {
            var latestNumbers = db.CommunicationAttempts.AsNoTracking().Where(x => ids.Contains(x.CommunicationId))
                .GroupBy(x => x.CommunicationId)
                .Select(group => new { CommunicationId = group.Key, AttemptNumber = group.Max(attempt => attempt.AttemptNumber) });
            attempts = await (from attempt in db.CommunicationAttempts.AsNoTracking()
                join latestNumber in latestNumbers on new { attempt.CommunicationId, attempt.AttemptNumber } equals new { latestNumber.CommunicationId, latestNumber.AttemptNumber }
                select attempt).ToListAsync(ct);
        }
        var latest = attempts.ToDictionary(x => x.CommunicationId);
        return rows.Select(row => MapView(row, latest.GetValueOrDefault(row.Id))).ToArray();
    }
    public async Task<bool> RetryAsync(string publicId,long actor,string? correlationId,CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var row = await db.Communications.SingleOrDefaultAsync(x => x.PublicId == publicId, ct) ?? throw new KeyNotFoundException("Communication was not found.");
        await EnsureCanMutateAsync(actor, row.TicketId, ct);
        var maxAttempts = Math.Clamp(configuration.GetValue("Communication:Retry:MaxAttempts", 3), 1, 20);
        if (row.Status != "FAILED_TRANSIENT" || row.RetryCount >= maxAttempts) throw new InvalidOperationException("Only retryable communication below its maximum attempt count can be retried.");
        row.Status = "QUEUED"; row.NextRetryAt = DateTimeOffset.UtcNow; row.LeaseOwner = null; row.LeaseUntil = null;
        await AddAudit("COMMUNICATION", row.PublicId, "COMMUNICATION_RETRY_SCHEDULED", actor, correlationId, ct);
        db.Events.Add(new EventRow { TicketId = row.TicketId, Type = "COMMUNICATION_RETRY_SCHEDULED", Category = "COMMUNICATION", ActorId = actor, NewValue = "QUEUED", Reason = correlationId, At = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> CancelAsync(string publicId,long actor,string? correlationId,CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var row = await db.Communications.SingleOrDefaultAsync(x => x.PublicId == publicId, ct) ?? throw new KeyNotFoundException("Communication was not found.");
        await EnsureCanMutateAsync(actor, row.TicketId, ct);
        if (row.Status is "SENT" or "DELIVERED" or "CANCELLED") throw new InvalidOperationException("Communication cannot be cancelled in its current state.");
        row.Status = "CANCELLED"; row.LeaseOwner = null; row.LeaseUntil = null;
        await AddAudit("COMMUNICATION", row.PublicId, "COMMUNICATION_CANCELLED", actor, correlationId, ct);
        db.Events.Add(new EventRow { TicketId = row.TicketId, Type = "COMMUNICATION_CANCELLED", Category = "COMMUNICATION", ActorId = actor, NewValue = "CANCELLED", Reason = correlationId, At = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyCollection<CommunicationMessage>> LeaseAsync(string workerId,int limit,TimeSpan leaseDuration,CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentException("Worker identity is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Communications.FromSqlInterpolated($@"select c.* from ""PA_TICKETS_COMMUNICATION"" c where ((""S_STATUS"" in ('QUEUED','FAILED_TRANSIENT') and coalesce(""D_NEXT_RETRY_AT"",now())<=now()) or (""S_STATUS""='PROCESSING' and ""D_LEASE_UNTIL""<now())) and not exists (select 1 from ""PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG"" ch where ch.""S_CHANNEL""=c.""S_CHANNEL"" and (not ch.""N_ENABLED"" or ch.""D_EFFECTIVE_FROM"">now() or (ch.""D_EFFECTIVE_TO"" is not null and ch.""D_EFFECTIVE_TO""<=now()))) and not exists (select 1 from ""PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG"" p where lower(p.""S_PROVIDER_CODE"")=lower(coalesce(c.""S_PROVIDER_CODE"",'deterministic')) and p.""S_CHANNEL""=c.""S_CHANNEL"" and (not p.""N_ENABLED"" or p.""D_EFFECTIVE_FROM"">now() or (p.""D_EFFECTIVE_TO"" is not null and p.""D_EFFECTIVE_TO""<=now()))) order by c.""D_COMMUNICATION_AT"" for update of c skip locked limit {limit}").ToListAsync(ct);
        foreach (var row in rows) { row.Status = "PROCESSING"; row.LeaseOwner = workerId; row.LeaseUntil = now.Add(leaseDuration); row.RetryCount++; }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return rows.Select(Map).ToArray();
    }

    public async Task ReleaseLeaseAsync(long communicationId,string workerId,CancellationToken ct)
    {
        var row = await db.Communications.SingleOrDefaultAsync(x => x.Id == communicationId, ct)
            ?? throw new KeyNotFoundException("Communication was not found.");
        if (row.Status != "PROCESSING" || row.LeaseOwner != workerId)
            throw new InvalidOperationException("Communication lease is no longer owned by this worker.");
        row.Status = "QUEUED";
        row.LeaseOwner = null;
        row.LeaseUntil = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(long communicationId,string workerId,CommunicationDeliveryResult result,int maxAttempts,TimeSpan retryDelay,string? correlationId,CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Communications.SingleOrDefaultAsync(x => x.Id == communicationId, ct) ?? throw new KeyNotFoundException("Communication was not found.");
        if (row.Status != "PROCESSING" || row.LeaseOwner != workerId) throw new InvalidOperationException("Communication lease is no longer owned by this worker.");
        var now = DateTimeOffset.UtcNow;
        OperationalMetrics.CommunicationAttempts.Add(1, new KeyValuePair<string, object?>("channel", row.Channel ?? "unknown"), new KeyValuePair<string, object?>("outcome", result.Accepted ? "accepted" : "failed"));
        if (!result.Accepted) OperationalMetrics.CommunicationFailed.Add(1, new KeyValuePair<string, object?>("channel", row.Channel ?? "unknown"), new KeyValuePair<string, object?>("terminal", result.FailureClassification != CommunicationFailureClassification.Transient || row.RetryCount >= maxAttempts ? "true" : "false"));
        db.CommunicationAttempts.Add(new CommunicationAttemptRow { CommunicationId = row.Id, AttemptNumber = row.RetryCount, StartedAt = now, CompletedAt = now, Provider = row.ProviderCode ?? "deterministic", ProviderMessageId = result.ProviderMessageId, Result = result.Accepted ? (result.Delivered ? "DELIVERED" : "ACCEPTED") : "FAILED", FailureClassification = result.FailureClassification?.ToString().ToUpperInvariant(), FailureCode = result.FailureCode, FailureReason = result.FailureReason, CorrelationId = correlationId ?? row.CorrelationId });
        row.LeaseOwner = null; row.LeaseUntil = null; row.FailureCode = result.FailureCode; row.FailureReason = result.FailureReason; row.FailureClassification = result.FailureClassification?.ToString().ToUpperInvariant(); row.ExternalReference = result.ProviderMessageId;
        var eventType = "COMMUNICATION_FAILED";
        if (result.Accepted) { OperationalMetrics.CommunicationDelivered.Add(1, new KeyValuePair<string, object?>("channel", row.Channel ?? "unknown")); row.Status = result.Delivered ? "DELIVERED" : "SENT"; row.SentAt = now; if (result.Delivered) row.DeliveredAt = now; eventType = result.Delivered ? "COMMUNICATION_DELIVERED" : "COMMUNICATION_SENT"; }
        else if (result.FailureClassification == CommunicationFailureClassification.Transient && row.RetryCount < maxAttempts) { OperationalMetrics.CommunicationRetries.Add(1, new KeyValuePair<string, object?>("channel", row.Channel ?? "unknown")); row.Status = "FAILED_TRANSIENT"; row.NextRetryAt = now + TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * Math.Pow(2, Math.Max(0, row.RetryCount - 1)), TimeSpan.FromHours(1).TotalMilliseconds)); row.FailedAt = now; eventType = "COMMUNICATION_RETRY_SCHEDULED"; }
        else { row.Status = "FAILED_PERMANENT"; row.FailedAt = now; }
        await AddAudit("COMMUNICATION", row.PublicId, eventType, 0, correlationId, ct);
        db.Events.Add(new EventRow { TicketId = row.TicketId, Type = eventType, Category = "COMMUNICATION", ActorId = 0, OldValue = "PROCESSING", NewValue = row.Status, Reason = result.FailureReason ?? correlationId, At = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static CommunicationView MapView(CommunicationRow row, CommunicationAttemptRow? attempt) => new(row.PublicId,row.TicketId,ParseChannel(row.Channel),row.ProviderCode,row.TemplateCode,row.Recipient ?? "",row.Subject,row.Body,ParseStatus(row.Status),row.RetryCount,attempt?.ProviderMessageId,row.CorrelationId,row.CommunicationAt,row.SentAt,row.DeliveredAt,row.FailureCode,row.FailureReason);

    private static CommunicationMessage Map(CommunicationRow row)=>new(row.Id,row.TicketId,row.WorkItemId,ParseChannel(row.Channel),row.ProviderCode,row.TemplateCode,row.Recipient ?? "",row.Subject,row.Body,row.StructuredPayload,ParseStatus(row.Status),row.RetryCount,row.CorrelationId,row.CommunicationAt,row.ScheduledAt,row.PublicId);
    private static CommunicationChannel ParseChannel(string? value)=>Enum.TryParse<CommunicationChannel>(value,true,out var result)?result:throw new InvalidOperationException("Communication channel is invalid.");
    private static CommunicationStatus ParseStatus(string? value)=>value?.ToUpperInvariant() switch { "PENDING" or "QUEUED"=>CommunicationStatus.Queued,"PROCESSING"=>CommunicationStatus.Processing,"SENT"=>CommunicationStatus.Sent,"DELIVERED"=>CommunicationStatus.Delivered,"FAILED_TRANSIENT"=>CommunicationStatus.FailedTransient,"FAILED_PERMANENT" or "FAILED"=>CommunicationStatus.FailedPermanent,"CANCELLED"=>CommunicationStatus.Cancelled,_=>throw new InvalidOperationException("Communication status is invalid.") };
    private async Task EnsureCanAccessAsync(long actor,long ticketId,CancellationToken ct){var ticket=await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new KeyNotFoundException("Ticket was not found.");await authorizer.EnsureCanAccessAsync(actor,ToTicket(ticket),ct);}
    private async Task EnsureCanMutateAsync(long actor,long ticketId,CancellationToken ct){var ticket=await db.Tickets.SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new KeyNotFoundException("Ticket was not found.");await authorizer.EnsureCanMutateAsync(actor,ToTicket(ticket),ct);}
    private static Ticket ToTicket(TicketRow ticket)=>Ticket.Restore(ticket.Id,ticket.Number,(TicketType)ticket.TypeId,(TicketStatus)ticket.StatusId,ticket.RequestedBy,ticket.CreatedBy,ticket.Subject,ticket.Description,ticket.ServiceId,ticket.CategoryId,ticket.RequestTypeId,ticket.PriorityId,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.CurrentAssignee,ticket.CreatedAt,ticket.ResolvedAt,ticket.ClosedAt,ticket.ApprovalRequired==1,ticket.Approved==1,ticket.EvidenceCount,ticket.LastExecutor,[]);
    private async Task AddAudit(string type,string id,string action,long actor,string? correlation,CancellationToken ct){await using var tx=await AuditTransaction.BeginIfNeededAsync(db,ct);await auditLocks.AcquireAsync(db,type,id,ct);var now=DateTimeOffset.UtcNow;var previous=await db.Audits.Where(x=>x.EntityType==type&&x.EntityId==id).OrderByDescending(x=>x.Id).Select(x=>x.Hash).FirstOrDefaultAsync(ct);var canonical=JsonSerializer.Serialize(new{entityType=type,entityId=id,action,actorUserId=actor,auditAtUtc=now.UtcDateTime,previousHash=previous,reason=correlation});var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();db.Audits.Add(new AuditRow{EntityType=type,EntityId=id,Action=action,ActorId=actor,At=now,Reason=correlation,Hash=hash,PreviousHash=previous});await db.SaveChangesAsync(ct);await tx.CommitIfOwnedAsync(ct);}
}

public sealed class OracleCommunicationStore(TicketsDbContext db,IRecipientResolver recipients,CommunicationTemplateRenderer renderer,AuditChainCoordinator auditLocks,ITicketAccessAuthorizer authorizer,IConfiguration configuration):CommunicationStore(db,recipients,renderer,auditLocks,authorizer,configuration) { }
