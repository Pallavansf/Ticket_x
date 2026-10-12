using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Application.Tickets;

public sealed record CreateChangeCommand(
    string Subject,
    string? Description,
    ChangeType ChangeType,
    ChangeImpact Impact,
    ChangeRisk Risk,
    string Reason,
    string? BusinessImpact,
    string ImplementationPlan,
    string ValidationPlan,
    string? RollbackPlan,
    string? SuccessCriteria,
    long RequesterUserId,
    long? ServiceId = null,
    long? CategoryId = null,
    long? RequestTypeId = null,
    long? PriorityId = null,
    string? CorrelationId = null,
    string? IdempotencyKey = null,
    long? SubCategoryId = null,
    bool ValidateCataloguePath = false);

public sealed record ChangeView(
    long TicketId,
    string Number,
    TicketType TicketType,
    TicketStatus TicketStatus,
    ChangeType ChangeType,
    ChangeImpact Impact,
    ChangeRisk Risk,
    int RiskScore,
    ChangeStatus Status,
    string Subject,
    string? Description,
    string Reason,
    string? BusinessImpact,
    string ImplementationPlan,
    string ValidationPlan,
    string? RollbackPlan,
    string? SuccessCriteria,
    DateTimeOffset? PlannedStartAt,
    DateTimeOffset? PlannedEndAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    string? ImplementationResult,
    string? FailureReason,
    string? RollbackResult,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ReopenedAt,
    long CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<ChangeImplementationView> Implementations);

public sealed record ChangeImplementationView(
    long Id,
    int StepNo,
    string StepName,
    string? Description,
    string Status,
    long? ExecutedBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Result);

public sealed record ChangeScheduleCommand(DateTimeOffset PlannedStartAt, DateTimeOffset PlannedEndAt, string? Reason = null);
public sealed record ChangeImplementationCommand(string? Result = null, string? Reason = null);
public sealed record ChangeFailureCommand(string FailureReason);
public sealed record ChangeRollbackCommand(string RollbackResult);
public sealed record ChangeValidationCommand(string Result, bool Successful = true);
public sealed record ChangeRelationshipResult(long TicketId, long RelatedTicketId, string RelationType);

public interface IChangeService
{
    Task<ChangeView> CreateAsync(CreateChangeCommand command, long actor, CancellationToken ct);
    Task<ChangeView> GetAsync(long ticketId, long actor, CancellationToken ct);
    Task<ChangeView> GetByNumberAsync(string number, long actor, CancellationToken ct);
    Task<ChangeView> AssessAsync(long ticketId, ChangeImpact impact, ChangeRisk risk, long actor, string? reason, CancellationToken ct);
    Task<ChangeView> ScheduleAsync(long ticketId, ChangeScheduleCommand command, long actor, CancellationToken ct);
    Task<ChangeView> StartImplementationAsync(long ticketId, long actor, CancellationToken ct);
    Task<ChangeView> CompleteImplementationAsync(long ticketId, ChangeImplementationCommand command, long actor, CancellationToken ct);
    Task<ChangeView> FailImplementationAsync(long ticketId, ChangeFailureCommand command, long actor, CancellationToken ct);
    Task<ChangeView> RollbackAsync(long ticketId, ChangeRollbackCommand command, long actor, CancellationToken ct);
    Task<ChangeView> ValidateAsync(long ticketId, ChangeValidationCommand command, long actor, CancellationToken ct);
    Task<ChangeView> ResolveAsync(long ticketId, string resolution, long actor, CancellationToken ct);
    Task<ChangeView> CloseAsync(long ticketId, long actor, CancellationToken ct);
    Task<ChangeView> ReopenAsync(long ticketId, string reason, long actor, CancellationToken ct);
    Task<ChangeRelationshipResult> RelateIncidentAsync(long changeTicketId, long incidentTicketId, string? description, long actor, CancellationToken ct);
    Task<ChangeRelationshipResult> RelateProblemAsync(long changeTicketId, long problemTicketId, string? description, long actor, CancellationToken ct);
    Task<ChangeRelationshipResult> RelateIncidentByNumberAsync(string changeNumber, string incidentNumber, string? description, long actor, CancellationToken ct);
    Task<ChangeRelationshipResult> RelateProblemByNumberAsync(string changeNumber, string problemNumber, string? description, long actor, CancellationToken ct);
    Task<IReadOnlyCollection<TicketRelationship>> GetRelationshipsAsync(long changeTicketId, long actor, CancellationToken ct);
}
