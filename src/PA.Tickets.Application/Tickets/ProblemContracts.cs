using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Application.Tickets;

public sealed record CreateProblemCommand(
    string Subject,
    string? Description,
    string ProblemStatement,
    long RequesterUserId,
    long? ServiceId = null,
    long? CategoryId = null,
    long? RequestTypeId = null,
    long? PriorityId = null,
    string? CorrelationId = null,
    string? IdempotencyKey = null,
    long? SubCategoryId = null,
    bool ValidateCataloguePath = false);

public sealed record ProblemView(
    long TicketId,
    string Number,
    TicketType Type,
    TicketStatus TicketStatus,
    ProblemStatus Status,
    string Subject,
    string ProblemStatement,
    string? RootCause,
    string? Workaround,
    string? PermanentFix,
    bool IsKnownError,
    DateTimeOffset? InvestigationStartedAt,
    DateTimeOffset? RootCauseIdentifiedAt,
    DateTimeOffset? KnownErrorDeclaredAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ReopenedAt,
    long? CurrentAssignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IProblemService
{
    Task<ProblemView> CreateAsync(CreateProblemCommand command, long actor, CancellationToken ct);
    Task<ProblemView> GetAsync(long ticketId, long actor, CancellationToken ct);
    Task<ProblemView> GetByNumberAsync(string number, long actor, CancellationToken ct);
    Task<ProblemView> AssignAsync(long ticketId, long assignee, string? reason, long actor, CancellationToken ct);
    Task<ProblemView> StartInvestigationAsync(long ticketId, long actor, CancellationToken ct);
    Task<ProblemView> UpdateRootCauseAsync(long ticketId, string rootCause, long actor, CancellationToken ct);
    Task<ProblemView> UpdateWorkaroundAsync(long ticketId, string workaround, long actor, CancellationToken ct);
    Task<ProblemView> DeclareKnownErrorAsync(long ticketId, long actor, CancellationToken ct);
    Task<ProblemView> UpdatePermanentFixAsync(long ticketId, string permanentFix, long actor, CancellationToken ct);
    Task<ProblemView> ResolveAsync(long ticketId, string resolutionSummary, long actor, CancellationToken ct);
    Task<ProblemView> CloseAsync(long ticketId, long actor, CancellationToken ct);
    Task<ProblemView> ReopenAsync(long ticketId, string reason, long actor, CancellationToken ct);
    Task<TicketRelationship> RelateIncidentAsync(long problemTicketId, long incidentTicketId, string? description, long actor, CancellationToken ct);
    Task<TicketRelationship> RelateIncidentByNumberAsync(string problemNumber, string incidentNumber, string? description, long actor, CancellationToken ct);
    Task<TicketRelationship> RelateChangeAsync(long problemTicketId, long changeTicketId, string relationType, string? description, long actor, CancellationToken ct);
    Task<IReadOnlyCollection<TicketRelationship>> GetIncidentRelationshipsAsync(long problemTicketId, long actor, CancellationToken ct);
}
