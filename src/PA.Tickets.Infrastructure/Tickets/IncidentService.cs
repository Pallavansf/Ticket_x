using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Tickets;

public sealed class IncidentService(
    TicketsDbContext db,
    AuditChainCoordinator auditLocks,
    ITicketAccessAuthorizer authorizer,
    ITicketLifecycleService tickets,
    TimeProvider clock) : IIncidentService
{
    public async Task<IncidentView> CreateAsync(CreateIncidentCommand command, long actor, CancellationToken ct)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (actor <= 0) throw new UnauthorizedAccessException("A server-authenticated actor is required.");
        if (string.IsNullOrWhiteSpace(command.Subject)) throw new ArgumentException("Incident subject is required.");
        if (command.SubCategoryId.HasValue && !command.RequestTypeId.HasValue) throw new ArgumentException("A Request Type is required when SubCategory is supplied.");
        if (command.IncidentCommanderUserId is long commanderUserId)
        {
            var commander = await db.AdministrationUsers.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == commanderUserId && x.Active, ct)
                ?? throw new ArgumentException("The incident commander is not active.");
            await authorizer.EnsureCanAssignAsync(
                actor,
                new AuthorizationResource(0, null, null, null),
                commander.DepartmentId,
                commander.TeamId,
                commanderUserId,
                ct);
        }
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey) && await db.Incidents.AnyAsync(x => x.IdempotencyKey == command.IdempotencyKey, ct))
            throw new InvalidOperationException("Incident request was already processed.");

        await using var creationTx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        TicketView? created = null;
        try
        {
            created = command.SubCategoryId.HasValue
                ? await tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, command.Subject, command.Description, command.RequesterUserId, actor, command.RequestTypeId, ServiceId: command.ServiceId, CategoryId: command.CategoryId, SubCategoryId: command.SubCategoryId, PriorityId: command.PriorityId), ct)
                : await tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, command.Subject, command.Description, command.RequesterUserId, actor, PriorityId: command.PriorityId), ct);

            var now = clock.GetUtcNow();
            var row = new IncidentRow
            {
                TicketId = created.Id,
                Impact = command.Impact.ToString(),
                Urgency = command.Urgency.ToString(),
                IsMajorIncident = false,
                IncidentCommanderUserId = command.IncidentCommanderUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IdempotencyKey = command.IdempotencyKey
            };

            db.Incidents.Add(row);
            db.Events.Add(new EventRow
            {
                TicketId = created.Id,
                Type = "INCIDENT_CREATED",
                Category = "INCIDENT",
                ActorId = actor,
                NewValue = $"{command.Impact}:{command.Urgency}",
                Reason = command.CorrelationId ?? command.IdempotencyKey,
                At = now
            });
            await AddAuditAsync("INCIDENT", created.Id.ToString(), "INCIDENT_CREATED", actor, command.CorrelationId ?? command.IdempotencyKey, ct);
            await db.SaveChangesAsync(ct);
            await creationTx.CommitIfOwnedAsync(ct);
            if (creationTx is not null)
                TicketEventCommitBoundary.ClearAfterSuccessfulCommit(created.Id);
            return await GetAsync(created.Id, actor, ct);
        }
        catch
        {
            if (created is not null)
                TicketEventCommitBoundary.DiscardPending(created.Id);
            throw;
        }
    }

    public async Task<IncidentView> AcknowledgeAsync(long ticketId, string? reason, long actor, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        if (row.AcknowledgedAt is not null) throw new InvalidOperationException("Incident was already acknowledged.");
        return await ExecuteMutationAsync(ticketId, actor, async now =>
        {
            row.AcknowledgedAt ??= now;
            row.AcknowledgedBy = actor;
            row.UpdatedAt = row.AcknowledgedAt.Value;
            db.Events.Add(new EventRow
            {
                TicketId = ticketId,
                Type = "INCIDENT_ACKNOWLEDGED",
                Category = "INCIDENT",
                ActorId = actor,
                NewValue = row.AcknowledgedAt.Value.ToString("O"),
                Reason = reason,
                At = row.AcknowledgedAt.Value
            });
            await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_ACKNOWLEDGED", actor, reason, ct);
        }, ct);
    }

    public async Task<IncidentView> AssignCommanderAsync(long ticketId, long commanderUserId, long actor, string? reason, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");
        var domainTicket = ToTicket(ticket);
        await authorizer.EnsureCanAssignAsync(actor, new AuthorizationResource(domainTicket.Id, domainTicket.CurrentDepartmentId, domainTicket.CurrentTeamId, domainTicket.ClientId, domainTicket.CurrentAssignee, RequesterUserId: domainTicket.RequestedBy), ticket.CurrentDepartmentId, ticket.CurrentTeamId, commanderUserId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        return await ExecuteMutationAsync(ticketId, actor, async now =>
        {
            row.IncidentCommanderUserId = commanderUserId;
            row.UpdatedAt = now;
            db.Events.Add(new EventRow
            {
                TicketId = ticketId,
                Type = "INCIDENT_COMMANDER_ASSIGNED",
                Category = "INCIDENT",
                ActorId = actor,
                NewValue = commanderUserId.ToString(),
                Reason = reason,
                At = now
            });
            await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_COMMANDER_ASSIGNED", actor, reason, ct);
        }, ct);
    }

    public async Task<IncidentView> DeclareMajorAsync(long ticketId, long actor, string? reason, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        if (row.IsMajorIncident) throw new InvalidOperationException("Major incident was already declared.");

        return await ExecuteMutationAsync(ticketId, actor, async now =>
        {
            row.IsMajorIncident = true;
            row.MajorIncidentDeclaredAt = now;
            row.MajorIncidentDeclaredBy = actor;
            row.UpdatedAt = now;
            db.Events.Add(new EventRow
            {
                TicketId = ticketId,
                Type = "INCIDENT_MAJOR_DECLARED",
                Category = "INCIDENT",
                ActorId = actor,
                NewValue = "true",
                Reason = reason,
                At = now
            });
            await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_MAJOR_DECLARED", actor, reason, ct);
        }, ct);
    }

    public async Task<IncidentView> RestoreAsync(long ticketId, long actor, string? reason, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        if (row.RestoredAt is not null) throw new InvalidOperationException("Incident was already restored.");
        return await ExecuteMutationAsync(ticketId, actor, async now =>
        {
            row.RestoredAt = now;
            row.RestoredBy = actor;
            row.UpdatedAt = now;
            db.Events.Add(new EventRow
            {
                TicketId = ticketId,
                Type = "INCIDENT_RESTORED",
                Category = "INCIDENT",
                ActorId = actor,
                NewValue = "true",
                Reason = reason,
                At = now
            });
            await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_RESTORED", actor, reason, ct);
        }, ct);
    }

    public async Task<IncidentView> ResolveAsync(long ticketId, string? rootCauseSummary, string? workaroundSummary, string? resolutionSummary, long actor, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        if (row.ResolvedAt is not null) throw new InvalidOperationException("Incident was already resolved.");
        await tickets.EnsureSpecializedStatusTransitionAllowedAsync(ticketId, TicketStatus.Resolved, actor, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var now = clock.GetUtcNow();
        row.RootCauseSummary = rootCauseSummary;
        row.WorkaroundSummary = workaroundSummary;
        row.ResolutionSummary = resolutionSummary;
        row.ResolvedAt = now;
        row.ResolvedBy = actor;
        row.UpdatedAt = now;
        await UpdateTicketStatus(ticketId, TicketStatus.Resolved, actor, now, null, ct);
        db.Events.Add(new EventRow
        {
            TicketId = ticketId,
            Type = "INCIDENT_RESOLVED",
            Category = "INCIDENT",
            ActorId = actor,
            NewValue = resolutionSummary ?? "resolved",
            Reason = rootCauseSummary ?? workaroundSummary,
            At = now
        });
        await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_RESOLVED", actor, resolutionSummary, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(ticketId, actor, ct);
    }

    public async Task<IncidentView> ReopenAsync(long ticketId, string reason, long actor, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reopen reason is required.");
        await tickets.EnsureSpecializedStatusTransitionAllowedAsync(ticketId, TicketStatus.Reopened, actor, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var now = clock.GetUtcNow();
        row.ResolvedAt = null;
        row.ResolvedBy = null;
        row.UpdatedAt = now;
        await UpdateTicketStatus(ticketId, TicketStatus.Reopened, actor, now, reason, ct);
        db.Events.Add(new EventRow
        {
            TicketId = ticketId,
            Type = "INCIDENT_REOPENED",
            Category = "INCIDENT",
            ActorId = actor,
            NewValue = TicketStatus.Reopened.ToString(),
            Reason = reason,
            At = now
        });
        await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_REOPENED", actor, reason, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(ticketId, actor, ct);
    }

    public async Task<IncidentView> CloseAsync(long ticketId, long actor, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        if (row.ClosedAt is not null) throw new InvalidOperationException("Incident was already closed.");
        await tickets.EnsureSpecializedStatusTransitionAllowedAsync(ticketId, TicketStatus.Closed, actor, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var now = clock.GetUtcNow();
        row.ClosedAt = now;
        row.ClosedBy = actor;
        row.UpdatedAt = now;
        await UpdateTicketStatus(ticketId, TicketStatus.Closed, actor, now, null, ct);
        db.Events.Add(new EventRow
        {
            TicketId = ticketId,
            Type = "INCIDENT_CLOSED",
            Category = "INCIDENT",
            ActorId = actor,
            NewValue = TicketStatus.Closed.ToString(),
            At = now
        });
        await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_CLOSED", actor, null, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(ticketId, actor, ct);
    }

    public async Task<IncidentView> UpdateImpactAsync(long ticketId, IncidentImpact impact, long actor, string? reason, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        return await ExecuteMutationAsync(ticketId, actor, async now =>
        {
            row.Impact = impact.ToString();
            row.UpdatedAt = now;
            db.Events.Add(new EventRow { TicketId = ticketId, Type = "INCIDENT_IMPACT_CHANGED", Category = "INCIDENT", ActorId = actor, NewValue = impact.ToString(), Reason = reason, At = now });
            await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_IMPACT_CHANGED", actor, reason, ct);
        }, ct);
    }

    public async Task<IncidentView> UpdateUrgencyAsync(long ticketId, IncidentUrgency urgency, long actor, string? reason, CancellationToken ct)
    {
        var row = await LoadRowAsync(ticketId, ct);
        await EnsureMutateAsync(ticketId, actor, ct);
        return await ExecuteMutationAsync(ticketId, actor, async now =>
        {
            row.Urgency = urgency.ToString();
            row.UpdatedAt = now;
            db.Events.Add(new EventRow { TicketId = ticketId, Type = "INCIDENT_URGENCY_CHANGED", Category = "INCIDENT", ActorId = actor, NewValue = urgency.ToString(), Reason = reason, At = now });
            await AddAuditAsync("INCIDENT", ticketId.ToString(), "INCIDENT_URGENCY_CHANGED", actor, reason, ct);
        }, ct);
    }

    public async Task<IncidentView> GetAsync(long ticketId, long actor, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");
        var incident = await db.Incidents.AsNoTracking().SingleOrDefaultAsync(x => x.TicketId == ticketId, ct)
            ?? throw new KeyNotFoundException($"Incident for ticket {ticketId} was not found.");

        await authorizer.EnsureCanAccessAsync(actor, ToTicket(ticket), ct);
        return ToView(ticket, incident);
    }

    private async Task<IncidentView> ExecuteMutationAsync(long ticketId, long actor, Func<DateTimeOffset, Task> stage, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await stage(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
        return await GetAsync(ticketId, actor, ct);
    }

    private async Task<IncidentRow> LoadRowAsync(long ticketId, CancellationToken ct)
    {
        return await db.Incidents.SingleOrDefaultAsync(x => x.TicketId == ticketId, ct)
            ?? throw new KeyNotFoundException($"Incident for ticket {ticketId} was not found.");
    }

    private async Task EnsureMutateAsync(long ticketId, long actor, CancellationToken ct)
    {
        var row = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId, ct)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");
        await authorizer.EnsureCanMutateAsync(actor, ToTicket(row), ct);
    }

    private async Task UpdateTicketStatus(long ticketId, TicketStatus status, long actor, DateTimeOffset now, string? reason, CancellationToken ct)
        => await tickets.TransitionSpecializedStatusAsync(ticketId, status, actor, reason, ct);

    private static IncidentView ToView(TicketRow ticket, IncidentRow incident)
    {
        return new IncidentView(
            ticket.Id,
            ticket.Number,
            (TicketType)ticket.TypeId,
            (TicketStatus)ticket.StatusId,
            ticket.Subject,
            Enum.Parse<IncidentImpact>(incident.Impact, ignoreCase: true),
            Enum.Parse<IncidentUrgency>(incident.Urgency, ignoreCase: true),
            incident.IsMajorIncident,
            incident.IncidentCommanderUserId,
            incident.AcknowledgedAt,
            incident.RestoredAt,
            incident.MajorIncidentDeclaredAt,
            incident.MajorIncidentResolvedAt,
            incident.ResolvedAt,
            incident.ClosedAt,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.RootCauseSummary,
            incident.WorkaroundSummary,
            incident.ResolutionSummary);
    }

    private async Task AddAuditAsync(string entityType, string entityId, string action, long actor, string? reason, CancellationToken ct)
    {
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, entityType, entityId, ct);
        var now = clock.GetUtcNow();
        var previous = await db.Audits
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.Id)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(ct);

        var canonical = System.Text.Json.JsonSerializer.Serialize(new
        {
            entityType,
            entityId,
            action,
            actorUserId = actor,
            auditAtUtc = now.UtcDateTime,
            oldValue = (string?)null,
            newValue = (string?)null,
            reason,
            previousHash = previous
        });

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorId = actor,
            At = now,
            Reason = reason,
            Hash = hash,
            PreviousHash = previous
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }

    private static Ticket ToTicket(TicketRow ticket) => Ticket.Restore(
        ticket.Id,
        ticket.Number,
        (TicketType)ticket.TypeId,
        (TicketStatus)ticket.StatusId,
        ticket.RequestedBy,
        ticket.CreatedBy,
        ticket.Subject,
        ticket.Description,
        ticket.ServiceId,
        ticket.CategoryId,
        ticket.RequestTypeId,
        ticket.PriorityId,
        ticket.CurrentDepartmentId,
        ticket.CurrentTeamId,
        ticket.CurrentAssignee,
        ticket.CreatedAt,
        ticket.ResolvedAt,
        ticket.ClosedAt,
        ticket.ApprovalRequired == 1,
        ticket.Approved == 1,
        ticket.EvidenceCount,
        ticket.LastExecutor,
        []);
}
