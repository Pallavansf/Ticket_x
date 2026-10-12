using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Tickets;

public sealed class ChangeService(
    TicketsDbContext db,
    AuditChainCoordinator auditLocks,
    ITicketAccessAuthorizer authorizer,
    IAuthorizationService authorization,
    ITicketLifecycleService tickets,
    ITicketRelationshipPolicy relationshipPolicy,
    TimeProvider clock) : IChangeService
{
    public async Task<ChangeView> CreateAsync(CreateChangeCommand command, long actor, CancellationToken ct)
    {
        EnsureActor(actor);
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (command.RequesterUserId != actor && !await authorization.HasPermissionAsync(actor, "USER_ADMIN", null, ct))
            throw new UnauthorizedAccessException("Only an administrator may create a Change on behalf of another user.");
        if (string.IsNullOrWhiteSpace(command.Subject)) throw new ArgumentException("Change subject is required.");
        if (string.IsNullOrWhiteSpace(command.Reason)) throw new ArgumentException("Change reason is required.");
        if (string.IsNullOrWhiteSpace(command.ImplementationPlan)) throw new ArgumentException("Implementation plan is required.");
        if (string.IsNullOrWhiteSpace(command.ValidationPlan)) throw new ArgumentException("Validation plan is required.");
        if (command.ChangeType != ChangeType.Standard && string.IsNullOrWhiteSpace(command.RollbackPlan))
            throw new ArgumentException("Normal and emergency Changes require a rollback plan.");
        if (command.SubCategoryId.HasValue && !command.RequestTypeId.HasValue) throw new ArgumentException("A Request Type is required when SubCategory is supplied.");
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey) && await db.Changes.AnyAsync(x => x.IdempotencyKey == command.IdempotencyKey, ct))
            throw new InvalidOperationException("Change request was already processed.");

        var validateCataloguePath = command.ValidateCataloguePath || command.SubCategoryId.HasValue;
        var created = validateCataloguePath
            ? await tickets.CreateAsync(new CreateTicketCommand(TicketType.Change, command.Subject, command.Description, command.RequesterUserId, actor, command.RequestTypeId, ServiceId: command.ServiceId, CategoryId: command.CategoryId, SubCategoryId: command.SubCategoryId, PriorityId: command.PriorityId), ct)
            : await tickets.CreateAsync(new CreateTicketCommand(TicketType.Change, command.Subject, command.Description, command.RequesterUserId, actor, PriorityId: command.PriorityId), ct);
        var ticket = await db.Tickets.SingleAsync(x => x.Id == created.Id, ct);
        if (!validateCataloguePath)
        {
            ticket.ServiceId = command.ServiceId;
            ticket.CategoryId = command.CategoryId;
            ticket.RequestTypeId = command.RequestTypeId;
        }
        var now = clock.GetUtcNow();
        var riskScore = CalculateRiskScore(command.Impact, command.Risk);
        var row = new ChangeRow
        {
            TicketId = created.Id,
            ChangeType = command.ChangeType.ToString().ToUpperInvariant(),
            Impact = command.Impact.ToString().ToUpperInvariant(),
            Risk = command.Risk.ToString().ToUpperInvariant(),
            RiskScore = riskScore,
            Reason = command.Reason.Trim(),
            BusinessImpact = command.BusinessImpact,
            ImplementationPlan = command.ImplementationPlan.Trim(),
            TestingPlan = command.ValidationPlan.Trim(),
            RollbackPlan = command.RollbackPlan,
            VerificationPlan = command.ValidationPlan.Trim(),
            SuccessCriteria = command.SuccessCriteria,
            Status = "DRAFT",
            CreatedBy = actor,
            CreatedAt = now,
            UpdatedAt = now
            ,IdempotencyKey = command.IdempotencyKey
        };
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        db.Changes.Add(row);
        db.Events.Add(new EventRow { TicketId = created.Id, Type = "CHANGE_CREATED", Category = "CHANGE", ActorId = actor, NewValue = row.ChangeType, Reason = command.CorrelationId ?? command.IdempotencyKey, At = now });
        await AddAuditAsync(created.Id, "CHANGE_CREATED", actor, command.CorrelationId ?? command.IdempotencyKey, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(created.Id, actor, ct);
    }

    public async Task<ChangeView> GetAsync(long ticketId, long actor, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct) ?? throw new KeyNotFoundException("Ticket was not found.");
        var row = await db.Changes.AsNoTracking().SingleOrDefaultAsync(x => x.TicketId == ticketId, ct) ?? throw new KeyNotFoundException("Change was not found.");
        if (ticket.TypeId != (int)TicketType.Change) throw new KeyNotFoundException("Change was not found.");
        await authorizer.EnsureCanAccessAsync(actor, ToTicket(ticket), ct);
        var implementations = await db.ChangeImplementations.AsNoTracking().Where(x => x.ChangeId == row.Id).OrderBy(x => x.StepNo).Select(x => new ChangeImplementationView(x.Id, x.StepNo, x.StepName, x.Description, x.Status, x.ExecutedBy, x.StartedAt, x.CompletedAt, x.Result)).ToListAsync(ct);
        return ToView(ticket, row, implementations);
    }

    public async Task<ChangeView> GetByNumberAsync(string number, long actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Change public number is required.");
        var ticketId = await db.Tickets.AsNoTracking().Where(x => x.Number == number && x.TypeId == (int)TicketType.Change).Select(x => (long?)x.Id).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Change was not found.");
        return await GetAsync(ticketId, actor, ct);
    }

    public Task<ChangeView> AssessAsync(long ticketId, ChangeImpact impact, ChangeRisk risk, long actor, string? reason, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_ASSESSED", (ticket, row, now) =>
        {
            EnsureMutable(row);
            row.Impact = impact.ToString().ToUpperInvariant();
            row.Risk = risk.ToString().ToUpperInvariant();
            row.RiskScore = CalculateRiskScore(impact, risk);
            row.Status = "ASSESSMENT";
            return new EventData($"{row.Impact}:{row.Risk}:{row.RiskScore}", reason);
        }, ct);

    public Task<ChangeView> ScheduleAsync(long ticketId, ChangeScheduleCommand command, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_SCHEDULED", (ticket, row, now) =>
        {
            EnsureMutable(row);
            if (command.PlannedEndAt <= command.PlannedStartAt) throw new ArgumentException("Change window end must be after start.");
            if (row.Status is not ("APPROVED" or "ASSESSMENT" or "DRAFT" or "REOPENED")) throw new InvalidOperationException("Change is not schedulable in its current state.");
            var conflict = db.Changes.Join(db.Tickets, change => change.TicketId, target => target.Id, (change, target) => new { change, target })
                .Any(x => x.change.TicketId != ticketId && x.target.ServiceId == ticket.ServiceId && x.change.PlannedStartAt < command.PlannedEndAt && x.change.PlannedEndAt > command.PlannedStartAt && (x.change.Status == "SCHEDULED" || x.change.Status == "IMPLEMENTING"));
            if (conflict) throw new InvalidOperationException("A conflicting Change is already scheduled for this service.");
            row.PlannedStartAt = command.PlannedStartAt;
            row.PlannedEndAt = command.PlannedEndAt;
            row.Status = "SCHEDULED";
            return new EventData($"{command.PlannedStartAt:O}/{command.PlannedEndAt:O}", command.Reason);
        }, ct);

    public Task<ChangeView> StartImplementationAsync(long ticketId, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_IMPLEMENTATION_STARTED", (ticket, row, now) =>
        {
            if (row.Status != "SCHEDULED") throw new InvalidOperationException("Only scheduled Changes can start implementation.");
            if (row.PlannedStartAt is null || row.PlannedEndAt is null) throw new InvalidOperationException("An implementation window is required.");
            row.Status = "IMPLEMENTING";
            row.ActualStartAt ??= now;
            if (!db.ChangeImplementations.Any(x => x.ChangeId == row.Id && x.Status == "IN_PROGRESS"))
                db.ChangeImplementations.Add(new ChangeImplementationRow { ChangeId = row.Id, StepNo = 1, StepName = "Implementation", Status = "IN_PROGRESS", ExecutedBy = actor, StartedAt = now });
            return new EventData("IMPLEMENTING", null);
        }, ct);

    public Task<ChangeView> CompleteImplementationAsync(long ticketId, ChangeImplementationCommand command, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_IMPLEMENTATION_COMPLETED", (ticket, row, now) =>
        {
            if (row.Status != "IMPLEMENTING") throw new InvalidOperationException("Change is not being implemented.");
            row.Status = "VALIDATING";
            row.ActualEndAt ??= now;
            row.ImplementationResult = command.Result;
            var step = db.ChangeImplementations.SingleOrDefault(x => x.ChangeId == row.Id && x.Status == "IN_PROGRESS");
            if (step is not null) { step.Status = "COMPLETED"; step.CompletedAt = now; step.Result = command.Result; }
            return new EventData(command.Result ?? "VALIDATING", command.Reason);
        }, ct);

    public Task<ChangeView> FailImplementationAsync(long ticketId, ChangeFailureCommand command, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_IMPLEMENTATION_FAILED", (ticket, row, now) =>
        {
            if (row.Status != "IMPLEMENTING") throw new InvalidOperationException("Change is not being implemented.");
            if (string.IsNullOrWhiteSpace(command.FailureReason)) throw new ArgumentException("Failure reason is required.");
            row.Status = "FAILED";
            row.FailureReason = command.FailureReason.Trim();
            row.ActualEndAt ??= now;
            var step = db.ChangeImplementations.SingleOrDefault(x => x.ChangeId == row.Id && x.Status == "IN_PROGRESS");
            if (step is not null) { step.Status = "FAILED"; step.CompletedAt = now; step.Result = row.FailureReason; }
            return new EventData(row.FailureReason, row.FailureReason);
        }, ct);

    public Task<ChangeView> RollbackAsync(long ticketId, ChangeRollbackCommand command, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_ROLLBACK_COMPLETED", (ticket, row, now) =>
        {
            if (row.Status is not ("FAILED" or "ROLLING_BACK")) throw new InvalidOperationException("Only failed Changes can be rolled back.");
            if (string.IsNullOrWhiteSpace(row.RollbackPlan)) throw new InvalidOperationException("This Change has no configured rollback plan.");
            if (string.IsNullOrWhiteSpace(command.RollbackResult)) throw new ArgumentException("Rollback result is required.");
            row.Status = "VALIDATING";
            row.RollbackResult = command.RollbackResult.Trim();
            return new EventData(row.RollbackResult, row.RollbackResult);
        }, ct);

    public Task<ChangeView> ValidateAsync(long ticketId, ChangeValidationCommand command, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_VALIDATED", (ticket, row, now) =>
        {
            if (row.Status != "VALIDATING") throw new InvalidOperationException("Change is not ready for validation.");
            if (!command.Successful) { row.Status = "FAILED"; row.FailureReason = command.Result; return new EventData(command.Result, command.Result); }
            if (string.IsNullOrWhiteSpace(row.TestingPlan) || string.IsNullOrWhiteSpace(row.SuccessCriteria)) throw new InvalidOperationException("Validation plan and success criteria are required.");
            row.Status = "RESOLVED";
            row.ResolvedAt = now;
            row.ImplementationResult = command.Result;
            return new EventData(command.Result, command.Result);
        }, ct);

    public Task<ChangeView> ResolveAsync(long ticketId, string resolution, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_RESOLVED", (ticket, row, now) =>
        {
            if (row.Status != "RESOLVED") throw new InvalidOperationException("Change must pass validation before resolution.");
            if (string.IsNullOrWhiteSpace(resolution)) throw new ArgumentException("Resolution is required.");
            row.ImplementationResult = resolution.Trim();
            return new EventData(row.ImplementationResult, resolution);
        }, ct);

    public Task<ChangeView> CloseAsync(long ticketId, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_CLOSED", (ticket, row, now) =>
        {
            if (row.Status != "RESOLVED") throw new InvalidOperationException("Only resolved Changes can close.");
            row.Status = "CLOSED";
            row.ClosedAt = now;
            return new EventData("CLOSED", null);
        }, ct);

    public Task<ChangeView> ReopenAsync(long ticketId, string reason, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "CHANGE_REOPENED", (ticket, row, now) =>
        {
            if (row.Status != "CLOSED" && row.Status != "RESOLVED") throw new InvalidOperationException("Only completed Changes can reopen.");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reopen reason is required.");
            row.Status = "REOPENED";
            row.ReopenedAt = now;
            row.ClosedAt = null;
            return new EventData("REOPENED", reason);
        }, ct);

    public async Task<ChangeRelationshipResult> RelateIncidentAsync(long changeTicketId, long incidentTicketId, string? description, long actor, CancellationToken ct)
        => await RelateAsync(changeTicketId, incidentTicketId, TicketType.Incident, "CHANGE_INCIDENT", description, actor, ct);

    public async Task<ChangeRelationshipResult> RelateProblemAsync(long changeTicketId, long problemTicketId, string? description, long actor, CancellationToken ct)
        => await RelateAsync(changeTicketId, problemTicketId, TicketType.Problem, "CHANGE_PROBLEM", description, actor, ct);

    public async Task<ChangeRelationshipResult> RelateIncidentByNumberAsync(string changeNumber, string incidentNumber, string? description, long actor, CancellationToken ct)
    {
        var changeId = await ResolveNumberAsync(changeNumber, TicketType.Change, ct);
        var incidentId = await ResolveNumberAsync(incidentNumber, TicketType.Incident, ct);
        return await RelateIncidentAsync(changeId, incidentId, description, actor, ct);
    }

    public async Task<ChangeRelationshipResult> RelateProblemByNumberAsync(string changeNumber, string problemNumber, string? description, long actor, CancellationToken ct)
    {
        var changeId = await ResolveNumberAsync(changeNumber, TicketType.Change, ct);
        var problemId = await ResolveNumberAsync(problemNumber, TicketType.Problem, ct);
        return await RelateProblemAsync(changeId, problemId, description, actor, ct);
    }

    public async Task<IReadOnlyCollection<TicketRelationship>> GetRelationshipsAsync(long changeTicketId, long actor, CancellationToken ct)
    {
        var change = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == changeTicketId && x.TypeId == (int)TicketType.Change, ct) ?? throw new KeyNotFoundException("Change was not found.");
        await authorizer.EnsureCanAccessAsync(actor, ToTicket(change), ct);
        var relationships = await db.Relations.AsNoTracking().Where(x => x.TicketId == changeTicketId || x.RelatedTicketId == changeTicketId).Select(x => new TicketRelationship(x.TicketId, x.RelatedTicketId, x.Type, x.Description, x.CreatedBy, x.CreatedAt)).ToListAsync(ct);
        foreach (var relationship in relationships)
        {
            var relatedTicketId = relationship.TicketId == changeTicketId ? relationship.RelatedTicketId : relationship.TicketId;
            await tickets.GetAsync(relatedTicketId, actor, ct);
        }
        return relationships;
    }

    private async Task<ChangeRelationshipResult> RelateAsync(long changeId, long relatedId, TicketType relatedType, string relationType, string? description, long actor, CancellationToken ct)
    {
        var change = await db.Tickets.SingleOrDefaultAsync(x => x.Id == changeId && x.TypeId == (int)TicketType.Change, ct) ?? throw new KeyNotFoundException("Change was not found.");
        var related = await db.Tickets.SingleOrDefaultAsync(x => x.Id == relatedId && x.TypeId == (int)relatedType, ct) ?? throw new KeyNotFoundException("Related ticket was not found.");
        await authorizer.EnsureCanMutateAsync(actor, ToTicket(change), ct);
        await authorizer.EnsureCanAccessAsync(actor, ToTicket(related), ct);
        if (!relationshipPolicy.IsAllowed(TicketType.Change, relatedType, relationType)) throw new InvalidOperationException("The configured relationship policy does not allow this relationship.");
        await tickets.AddRelationshipAsync(changeId, relatedId, relationType, description, actor, ct);
        return new ChangeRelationshipResult(changeId, relatedId, relationType);
    }

    private async Task<ChangeView> MutateAsync(long ticketId, long actor, string eventType, Func<TicketRow, ChangeRow, DateTimeOffset, EventData> mutation, CancellationToken ct)
    {
        EnsureActor(actor);
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId, ct) ?? throw new KeyNotFoundException("Ticket was not found.");
        var row = await db.Changes.SingleOrDefaultAsync(x => x.TicketId == ticketId, ct) ?? throw new KeyNotFoundException("Change was not found.");
        if (ticket.TypeId != (int)TicketType.Change) throw new KeyNotFoundException("Change was not found.");
        await authorizer.EnsureCanMutateAsync(actor, ToTicket(ticket), ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var now = clock.GetUtcNow();
        var data = mutation(ticket, row, now);
        row.UpdatedAt = now;
        db.Events.Add(new EventRow { TicketId = ticketId, Type = eventType, Category = "CHANGE", ActorId = actor, NewValue = data.NewValue, Reason = data.Reason, At = now });
        await AddAuditAsync(ticketId, eventType, actor, data.Reason ?? data.NewValue, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(ticketId, actor, ct);
    }

    private async Task AddAuditAsync(long ticketId, string action, long actor, string? reason, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        var entityId = ticketId.ToString();
        await auditLocks.AcquireAsync(db, "CHANGE", entityId, ct);
        var now = clock.GetUtcNow();
        var previous = await db.Audits.Where(x => x.EntityType == "CHANGE" && x.EntityId == entityId).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var canonical = JsonSerializer.Serialize(new { entityType = "CHANGE", entityId, action, actorUserId = actor, auditAtUtc = now.UtcDateTime, reason, previousHash = previous });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow { EntityType = "CHANGE", EntityId = entityId, Action = action, ActorId = actor, At = now, Reason = reason, Hash = hash, PreviousHash = previous });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }

    private static int CalculateRiskScore(ChangeImpact impact, ChangeRisk risk) => ((int)impact + (int)risk) * 2;
    private async Task<long> ResolveNumberAsync(string number, TicketType type, CancellationToken ct)
        => await db.Tickets.AsNoTracking().Where(x => x.Number == number && x.TypeId == (int)type).Select(x => (long?)x.Id).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Ticket was not found.");
    private static void EnsureActor(long actor) { if (actor <= 0) throw new UnauthorizedAccessException("A server-authenticated actor is required."); }
    private static void EnsureMutable(ChangeRow row) { if (row.Status is "CLOSED" or "RESOLVED" or "CANCELLED") throw new InvalidOperationException("The Change is not mutable in its current state."); }
    private static Ticket ToTicket(TicketRow x) => Ticket.Restore(x.Id, x.Number, (TicketType)x.TypeId, (TicketStatus)x.StatusId, x.RequestedBy, x.CreatedBy, x.Subject, x.Description, x.ServiceId, x.CategoryId, x.RequestTypeId, x.PriorityId, x.CurrentDepartmentId, x.CurrentTeamId, x.CurrentAssignee, x.CreatedAt, x.ResolvedAt, x.ClosedAt, x.ApprovalRequired == 1, x.Approved == 1, x.EvidenceCount, x.LastExecutor, []);
    private static ChangeView ToView(TicketRow ticket, ChangeRow row, IReadOnlyCollection<ChangeImplementationView> implementations) => new(ticket.Id, ticket.Number, (TicketType)ticket.TypeId, (TicketStatus)ticket.StatusId, ParseEnum<ChangeType>(row.ChangeType), ParseEnum<ChangeImpact>(row.Impact), ParseEnum<ChangeRisk>(row.Risk), row.RiskScore, ParseEnum<ChangeStatus>(row.Status), ticket.Subject, ticket.Description, row.Reason ?? "", row.BusinessImpact, row.ImplementationPlan ?? "", row.TestingPlan ?? "", row.RollbackPlan, row.SuccessCriteria, row.PlannedStartAt, row.PlannedEndAt, row.ActualStartAt, row.ActualEndAt, row.ImplementationResult, row.FailureReason, row.RollbackResult, row.ResolvedAt, row.ClosedAt, row.ReopenedAt, row.CreatedBy, row.CreatedAt, row.UpdatedAt, implementations);
    private static T ParseEnum<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var result) ? result : throw new InvalidOperationException($"Change value '{value}' is invalid.");
    private sealed record EventData(string NewValue, string? Reason);
}
