using PA.Tickets.Domain.Communications;

namespace PA.Tickets.Application.Foundations;

public sealed record NotificationEventEnvelope(
    long EventId,
    long TicketId,
    long? WorkItemId,
    string EventType,
    string? EventCategory,
    string? Payload,
    DateTimeOffset EventAt,
    string? CorrelationId,
    string? ActorContext);

public sealed record NotificationProcessResult(
    bool Matched,
    string Status,
    string? SkipReason,
    int CreatedCommunicationCount,
    IReadOnlyList<NotificationCommunicationRecord> Communications,
    string? RuleCode = null,
    long? RuleVersionId = null,
    long? ExecutionId = null,
    string? CorrelationId = null);

public sealed record NotificationCommunicationRecord(
    long CommunicationId,
    string Channel,
    string Recipient,
    string TemplateCode,
    string? Subject,
    string? Body,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    string PublicId);

public interface INotificationOrchestrator
{
    Task<NotificationProcessResult> ProcessAsync(NotificationEventEnvelope envelope, long actor, CancellationToken ct);
}
