namespace PA.Tickets.Application.Foundations;

public sealed record RealtimeTicketEvent(
    long Id,
    string Type,
    string ChangeKind,
    long Version,
    DateTimeOffset OccurredAt);

public interface IRealtimePublisher
{
    Task PublishTicketEventAsync(long ticketId, long? departmentId, RealtimeTicketEvent message, CancellationToken ct);
}
