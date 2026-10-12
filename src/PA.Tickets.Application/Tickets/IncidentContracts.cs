using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Application.Tickets;

public sealed record CreateIncidentCommand(
    string Subject,
    string? Description,
    long RequesterUserId,
    long? ServiceId,
    long? CategoryId,
    long? RequestTypeId,
    long? PriorityId,
    IncidentImpact Impact,
    IncidentUrgency Urgency,
    long? IncidentCommanderUserId,
    string? CorrelationId = null,
    string? IdempotencyKey = null,
    long? SubCategoryId = null);

public sealed record IncidentView(
    long TicketId,
    string Number,
    TicketType Type,
    TicketStatus Status,
    string Subject,
    IncidentImpact Impact,
    IncidentUrgency Urgency,
    bool IsMajorIncident,
    long? IncidentCommanderUserId,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? RestoredAt,
    DateTimeOffset? MajorIncidentDeclaredAt,
    DateTimeOffset? MajorIncidentResolvedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? RootCauseSummary,
    string? WorkaroundSummary,
    string? ResolutionSummary);

public interface IIncidentService
{
    Task<IncidentView> CreateAsync(CreateIncidentCommand command, long actor, CancellationToken ct);
    Task<IncidentView> AcknowledgeAsync(long ticketId, string? reason, long actor, CancellationToken ct);
    Task<IncidentView> AssignCommanderAsync(long ticketId, long commanderUserId, long actor, string? reason, CancellationToken ct);
    Task<IncidentView> DeclareMajorAsync(long ticketId, long actor, string? reason, CancellationToken ct);
    Task<IncidentView> RestoreAsync(long ticketId, long actor, string? reason, CancellationToken ct);
    Task<IncidentView> ResolveAsync(long ticketId, string? rootCauseSummary, string? workaroundSummary, string? resolutionSummary, long actor, CancellationToken ct);
    Task<IncidentView> ReopenAsync(long ticketId, string reason, long actor, CancellationToken ct);
    Task<IncidentView> CloseAsync(long ticketId, long actor, CancellationToken ct);
    Task<IncidentView> UpdateImpactAsync(long ticketId, IncidentImpact impact, long actor, string? reason, CancellationToken ct);
    Task<IncidentView> UpdateUrgencyAsync(long ticketId, IncidentUrgency urgency, long actor, string? reason, CancellationToken ct);
    Task<IncidentView> GetAsync(long ticketId, long actor, CancellationToken ct);
}
