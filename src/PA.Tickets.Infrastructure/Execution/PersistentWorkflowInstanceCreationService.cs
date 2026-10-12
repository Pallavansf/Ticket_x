using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class PersistentWorkflowInstanceCreationService(
    ExecutionDbContext db,
    TicketsDbContext ticketDb,
    IWorkflowVersionSelector versions,
    IAuthorizationService authorization,
    ProviderTransactionRetry retry) : IWorkflowInstanceCreationService
{
    public Task<WorkflowInstanceCreationResult> CreateAsync(long ticketId, long workflowId, long actor, string idempotencyKey, string? correlationId, CancellationToken ct, long? selectedWorkflowVersionId = null)
        => retry.ExecuteAsync<WorkflowInstanceCreationResult>(db, async () =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var ticket = await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct)
                ?? throw new KeyNotFoundException("Ticket was not found.");
            await authorization.EnsurePermissionAsync(actor, "TICKET_UPDATE", Resource(ticket), ct);
            var fingerprint = $"{ticketId}:{workflowId}:{selectedWorkflowVersionId?.ToString() ?? "EFFECTIVE"}";
            var prior = await db.Idempotency.SingleOrDefaultAsync(x => x.Scope == "WORKFLOW_INSTANCE_CREATE" && x.Key == idempotencyKey, ct);
            if (prior is not null)
            {
                if (prior.Fingerprint != fingerprint) throw new InvalidOperationException("Idempotency key was reused for a different workflow instance request.");
                var existing = await db.WorkflowInstances.Where(x => x.TicketId == ticketId).OrderByDescending(x => x.Id).FirstAsync(ct);
                return new(existing.Id, existing.VersionId, true);
            }
            var selected = selectedWorkflowVersionId.HasValue
                ? await LoadSelectedVersionAsync(workflowId, selectedWorkflowVersionId.Value, ct)
                : await versions.ResolveAsync(workflowId, DateTimeOffset.UtcNow, ct);
            if (ticket.RequestTypeId is long requestTypeId)
            {
                var requestType = await ticketDb.CatalogueRequestTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestTypeId, ct);
                if (requestType is not null &&
                    (requestType.WorkflowVersionId.HasValue && requestType.WorkflowId is null ||
                    requestType.WorkflowId.HasValue && (requestType.WorkflowId.Value != workflowId ||
                    requestType.WorkflowVersionId.HasValue && requestType.WorkflowVersionId.Value != selected.VersionId)))
                    throw new UnauthorizedAccessException("The workflow does not match the ticket's configured request type.");
            }
            var instance = new WorkflowInstanceRow { TicketId = ticketId, VersionId = selected.VersionId, Status = "RUNNING", RowVersion = 0 };
            db.WorkflowInstances.Add(instance);
            var now = DateTimeOffset.UtcNow;
            db.Idempotency.Add(new IdempotencyRow { Scope = "WORKFLOW_INSTANCE_CREATE", Key = idempotencyKey, Fingerprint = fingerprint, Status = "COMPLETED", CreatedAt = now, CompletedAt = now });
            db.Events.Add(new PA.Tickets.Infrastructure.Persistence.EventRow { TicketId = ticketId, Type = "WORKFLOW_INSTANCE_CREATED", Category = "WORKFLOW", ActorId = actor, NewValue = selected.VersionId.ToString(), At = now });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(instance.Id, selected.VersionId, false);
        }, ct);

    private async Task<EffectiveWorkflowVersion> LoadSelectedVersionAsync(long workflowId, long versionId, CancellationToken ct)
    {
        var row = await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.WorkflowId == workflowId && x.Status == "PUBLISHED", ct)
            ?? throw new InvalidOperationException("The selected workflow version is no longer published.");
        return new(row.Id, row.WorkflowId, row.VersionNo);
    }

    private static AuthorizationResource Resource(TicketRow ticket) =>
        new(ticket.Id, ticket.CurrentDepartmentId, ticket.CurrentTeamId, ticket.ClientId, ticket.CurrentAssignee);
}