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

public sealed class ProblemService(
    TicketsDbContext db,
    AuditChainCoordinator auditLocks,
    ITicketAccessAuthorizer authorizer,
    ITicketLifecycleService tickets,
    TimeProvider clock) : IProblemService
{
    public async Task<ProblemView> CreateAsync(CreateProblemCommand command, long actor, CancellationToken ct)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        EnsureActor(actor);
        if (string.IsNullOrWhiteSpace(command.Subject)) throw new ArgumentException("Problem subject is required.");
        if (string.IsNullOrWhiteSpace(command.ProblemStatement)) throw new ArgumentException("Problem statement is required.");
        if (command.SubCategoryId.HasValue && !command.RequestTypeId.HasValue) throw new ArgumentException("A Request Type is required when SubCategory is supplied.");
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey) && await db.Problems.AnyAsync(x => x.IdempotencyKey == command.IdempotencyKey, ct))
            throw new InvalidOperationException("Problem request was already processed.");

        var validateCataloguePath = command.ValidateCataloguePath || command.SubCategoryId.HasValue;
        var created = validateCataloguePath
            ? await tickets.CreateAsync(new CreateTicketCommand(TicketType.Problem, command.Subject, command.Description, command.RequesterUserId, actor, command.RequestTypeId, ServiceId: command.ServiceId, CategoryId: command.CategoryId, SubCategoryId: command.SubCategoryId, PriorityId: command.PriorityId), ct)
            : await tickets.CreateAsync(new CreateTicketCommand(TicketType.Problem, command.Subject, command.Description, command.RequesterUserId, actor, PriorityId: command.PriorityId), ct);
        var ticket = await db.Tickets.SingleAsync(x => x.Id == created.Id, ct);
        if (!validateCataloguePath)
        {
            ticket.ServiceId = command.ServiceId;
            ticket.CategoryId = command.CategoryId;
            ticket.RequestTypeId = command.RequestTypeId;
        }
        var now = clock.GetUtcNow();
        var row = new ProblemRow
        {
            TicketId = created.Id,
            Statement = command.ProblemStatement.Trim(),
            Status = "OPEN",
            CreatedBy = actor,
            CreatedAt = now,
            UpdatedAt = now,
            IdempotencyKey = command.IdempotencyKey
        };
        db.Problems.Add(row);
        db.Events.Add(new EventRow { TicketId = created.Id, Type = "PROBLEM_CREATED", Category = "PROBLEM", ActorId = actor, NewValue = row.Statement, Reason = command.CorrelationId ?? command.IdempotencyKey, At = now });
        await AddAuditAsync(created.Id, "PROBLEM_CREATED", actor, command.CorrelationId ?? command.IdempotencyKey, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(created.Id, actor, ct);
    }

    public Task<ProblemView> GetByNumberAsync(string number, long actor, CancellationToken ct)
        => GetAsync(ResolveNumber(number), actor, ct);

    public async Task<ProblemView> GetAsync(long ticketId, long actor, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct) ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");
        if (ticket.TypeId != (int)TicketType.Problem) throw new KeyNotFoundException($"Problem for ticket {ticketId} was not found.");
        var row = await db.Problems.AsNoTracking().SingleOrDefaultAsync(x => x.TicketId == ticketId, ct) ?? throw new KeyNotFoundException($"Problem for ticket {ticketId} was not found.");
        await authorizer.EnsureCanAccessAsync(actor, ToTicket(ticket), ct);
        return ToView(ticket, row);
    }

    public async Task<ProblemView> AssignAsync(long ticketId, long assignee, string? reason, long actor, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");
        var domainTicket = ToTicket(ticket);
        await authorizer.EnsureCanAssignAsync(actor, new AuthorizationResource(domainTicket.Id, domainTicket.CurrentDepartmentId, domainTicket.CurrentTeamId, domainTicket.ClientId, domainTicket.CurrentAssignee), ticket.CurrentDepartmentId, ticket.CurrentTeamId, assignee, ct);
        return await MutateAsync(ticketId, actor, "PROBLEM_ASSIGNED", (ticket, row, now) =>
        {
            if (assignee <= 0) throw new ArgumentException("A valid assignee is required.");
            EnsureOpen(row);
            ticket.CurrentAssignee = assignee;
            db.Assignments.Add(new AssignmentRow { TicketId = ticket.Id, AssignedTo = assignee, Type = "PROBLEM_OWNER", AssignedAt = now, AssignedBy = actor, Reason = reason });
            return new EventData(assignee.ToString(), reason);
        }, ct);
    }

    public Task<ProblemView> StartInvestigationAsync(long ticketId, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_INVESTIGATION_STARTED", (ticket, row, now) =>
        {
            EnsureOpen(row);
            if (row.IdentifiedAt is not null) throw new InvalidOperationException("Problem investigation was already started.");
            row.IdentifiedAt = now;
            row.IdentifiedBy = actor;
            row.Status = "INVESTIGATING";
            return new EventData("INVESTIGATING", null);
        }, ct);

    public Task<ProblemView> UpdateRootCauseAsync(long ticketId, string rootCause, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_ROOT_CAUSE_UPDATED", (ticket, row, now) =>
        {
            EnsureOpen(row);
            if (string.IsNullOrWhiteSpace(rootCause)) throw new ArgumentException("Root cause is required.");
            row.RootCause = rootCause.Trim();
            row.RootCauseIdentifiedAt ??= now;
            row.RootCauseIdentifiedBy = actor;
            row.Status = "INVESTIGATING";
            return new EventData(row.RootCause, null);
        }, ct);

    public Task<ProblemView> UpdateWorkaroundAsync(long ticketId, string workaround, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_WORKAROUND_UPDATED", (ticket, row, now) =>
        {
            EnsureOpen(row);
            if (string.IsNullOrWhiteSpace(workaround)) throw new ArgumentException("Workaround is required.");
            row.Workaround = workaround.Trim();
            return new EventData(row.Workaround, null);
        }, ct);

    public Task<ProblemView> DeclareKnownErrorAsync(long ticketId, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_DECLARED_KNOWN_ERROR", (ticket, row, now) =>
        {
            EnsureOpen(row);
            if (row.IsKnownError) throw new InvalidOperationException("Problem was already declared a Known Error.");
            if (string.IsNullOrWhiteSpace(row.RootCause)) throw new InvalidOperationException("Root cause is required before declaring a Known Error.");
            row.IsKnownError = true;
            row.KnownErrorDeclaredAt = now;
            row.KnownErrorDeclaredBy = actor;
            row.Status = "KNOWN_ERROR";
            return new EventData("true", null);
        }, ct);

    public Task<ProblemView> UpdatePermanentFixAsync(long ticketId, string permanentFix, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_PERMANENT_FIX_UPDATED", (ticket, row, now) =>
        {
            EnsureOpen(row);
            if (string.IsNullOrWhiteSpace(permanentFix)) throw new ArgumentException("Permanent fix is required.");
            row.PermanentFix = permanentFix.Trim();
            return new EventData(row.PermanentFix, null);
        }, ct);

    public Task<ProblemView> ResolveAsync(long ticketId, string resolutionSummary, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_RESOLVED", (ticket, row, now) =>
        {
            EnsureOpen(row);
            if (string.IsNullOrWhiteSpace(row.RootCause) || string.IsNullOrWhiteSpace(row.PermanentFix)) throw new InvalidOperationException("Root cause and permanent fix are required before resolution.");
            if (string.IsNullOrWhiteSpace(resolutionSummary)) throw new ArgumentException("Resolution summary is required.");
            row.ResolutionSummary = resolutionSummary.Trim();
            row.ResolvedAt = now;
            row.ResolvedBy = actor;
            row.Status = "RESOLVED";
            return new EventData(row.ResolutionSummary, null);
        }, ct, TicketStatus.Resolved);

    public Task<ProblemView> CloseAsync(long ticketId, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_CLOSED", (ticket, row, now) =>
        {
            if (row.Status != "RESOLVED") throw new InvalidOperationException("Only resolved Problems can be closed.");
            if (row.ClosedAt is not null) throw new InvalidOperationException("Problem was already closed.");
            row.ClosedAt = now;
            row.ClosedBy = actor;
            row.Status = "CLOSED";
            return new EventData("CLOSED", null);
        }, ct, TicketStatus.Closed);

    public Task<ProblemView> ReopenAsync(long ticketId, string reason, long actor, CancellationToken ct)
        => MutateAsync(ticketId, actor, "PROBLEM_REOPENED", (ticket, row, now) =>
        {
            if (row.Status is not ("RESOLVED" or "CLOSED")) throw new InvalidOperationException("Only resolved or closed Problems can be reopened.");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reopen reason is required.");
            row.Status = "REOPENED";
            row.ReopenedAt = now;
            row.ReopenedBy = actor;
            row.ResolvedAt = null;
            row.ResolvedBy = null;
            row.ClosedAt = null;
            row.ClosedBy = null;
            return new EventData("REOPENED", reason);
        }, ct, TicketStatus.Reopened);

    public async Task<TicketRelationship> RelateIncidentAsync(long problemTicketId, long incidentTicketId, string? description, long actor, CancellationToken ct)
    {
        await GetAsync(problemTicketId, actor, ct);
        await tickets.GetAsync(incidentTicketId, actor, ct);
        return await tickets.AddRelationshipAsync(incidentTicketId, problemTicketId, "INCIDENT_PROBLEM", description, actor, ct);
    }

    public async Task<TicketRelationship> RelateIncidentByNumberAsync(string problemNumber, string incidentNumber, string? description, long actor, CancellationToken ct)
    {
        var problemId = ResolveNumber(problemNumber);
        var incident = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Number == incidentNumber && x.TypeId == (int)TicketType.Incident, ct) ?? throw new KeyNotFoundException("Incident was not found.");
        return await RelateIncidentAsync(problemId, incident.Id, description, actor, ct);
    }

    public async Task<TicketRelationship> RelateChangeAsync(long problemTicketId, long changeTicketId, string relationType, string? description, long actor, CancellationToken ct)
    {
        await GetAsync(problemTicketId, actor, ct);
        await tickets.GetAsync(changeTicketId, actor, ct);
        return await tickets.AddRelationshipAsync(problemTicketId, changeTicketId, relationType, description, actor, ct);
    }

    public async Task<IReadOnlyCollection<TicketRelationship>> GetIncidentRelationshipsAsync(long problemTicketId, long actor, CancellationToken ct)
    {
        await GetAsync(problemTicketId, actor, ct);
        var relationships = await db.Relations.AsNoTracking().Where(x => x.RelatedTicketId == problemTicketId && x.Type == "INCIDENT_PROBLEM")
            .Select(x => new TicketRelationship(x.TicketId, x.RelatedTicketId, x.Type, x.Description, x.CreatedBy, x.CreatedAt)).ToListAsync(ct);
        foreach (var relationship in relationships)
            await tickets.GetAsync(relationship.TicketId, actor, ct);
        return relationships;
    }

    private async Task<ProblemView> MutateAsync(long ticketId, long actor, string action, Func<TicketRow, ProblemRow, DateTimeOffset, EventData> mutation, CancellationToken ct, TicketStatus? lifecycleStatus = null)
    {
        EnsureActor(actor);
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId, ct) ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");
        var row = await db.Problems.SingleOrDefaultAsync(x => x.TicketId == ticketId, ct) ?? throw new KeyNotFoundException($"Problem for ticket {ticketId} was not found.");
        if (ticket.TypeId != (int)TicketType.Problem) throw new KeyNotFoundException($"Problem for ticket {ticketId} was not found.");
        await authorizer.EnsureCanMutateAsync(actor, ToTicket(ticket), ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var now = clock.GetUtcNow();
        var data = mutation(ticket, row, now);
        if (lifecycleStatus.HasValue)
            await tickets.TransitionSpecializedStatusAsync(ticketId, lifecycleStatus.Value, actor, data.Reason ?? data.NewValue, ct);
        row.UpdatedAt = now;
        db.Events.Add(new EventRow { TicketId = ticketId, Type = action, Category = "PROBLEM", ActorId = actor, NewValue = data.NewValue, Reason = data.Reason, At = now });
        await AddAuditAsync(ticketId, action, actor, data.Reason ?? data.NewValue, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToView(ticket, row);
    }

    private async Task AddAuditAsync(long ticketId, string action, long actor, string? reason, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        var entityId = ticketId.ToString();
        await auditLocks.AcquireAsync(db, "PROBLEM", entityId, ct);
        var now = clock.GetUtcNow();
        var previous = await db.Audits.Where(x => x.EntityType == "PROBLEM" && x.EntityId == entityId).OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var canonical = JsonSerializer.Serialize(new { entityType = "PROBLEM", entityId, action, actorUserId = actor, auditAtUtc = now.UtcDateTime, reason, previousHash = previous });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow { EntityType = "PROBLEM", EntityId = entityId, Action = action, ActorId = actor, At = now, Reason = reason, Hash = hash, PreviousHash = previous });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }

    private long ResolveNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Problem public number is required.");
        var row = db.Tickets.AsNoTracking().SingleOrDefault(x => x.Number == number && x.TypeId == (int)TicketType.Problem) ?? throw new KeyNotFoundException("Problem was not found.");
        return row.Id;
    }

    private static void EnsureActor(long actor) { if (actor <= 0) throw new UnauthorizedAccessException("A server-authenticated actor is required."); }
    private static void EnsureOpen(ProblemRow row) { if (row.Status is "CLOSED" or "RESOLVED") throw new InvalidOperationException("The Problem is not mutable in its current state."); }
    private static Ticket ToTicket(TicketRow x) => Ticket.Restore(x.Id, x.Number, (TicketType)x.TypeId, (TicketStatus)x.StatusId, x.RequestedBy, x.CreatedBy, x.Subject, x.Description, x.ServiceId, x.CategoryId, x.RequestTypeId, x.PriorityId, x.CurrentDepartmentId, x.CurrentTeamId, x.CurrentAssignee, x.CreatedAt, x.ResolvedAt, x.ClosedAt, x.ApprovalRequired == 1, x.Approved == 1, x.EvidenceCount, x.LastExecutor, []);
    private static ProblemView ToView(TicketRow ticket, ProblemRow row) => new(ticket.Id, ticket.Number, (TicketType)ticket.TypeId, (TicketStatus)ticket.StatusId, ParseStatus(row.Status), ticket.Subject, row.Statement ?? "", row.RootCause, row.Workaround, row.PermanentFix, row.IsKnownError, row.IdentifiedAt, row.RootCauseIdentifiedAt, row.KnownErrorDeclaredAt, row.ResolvedAt, row.ClosedAt, row.ReopenedAt, ticket.CurrentAssignee, row.CreatedAt, row.UpdatedAt);
    private static ProblemStatus ParseStatus(string status) => status.ToUpperInvariant() switch
    {
        "OPEN" => ProblemStatus.Open,
        "INVESTIGATING" => ProblemStatus.Investigating,
        "KNOWN_ERROR" => ProblemStatus.KnownError,
        "RESOLVED" => ProblemStatus.Resolved,
        "CLOSED" => ProblemStatus.Closed,
        "REOPENED" => ProblemStatus.Reopened,
        _ => throw new InvalidOperationException($"Problem status '{status}' is invalid.")
    };
    private sealed record EventData(string? NewValue, string? Reason);
}
