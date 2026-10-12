using PA.Tickets.Domain.Sla;

namespace PA.Tickets.Application.Sla;

public sealed record SlaStartResult(
    long SlaId,
    long PolicyId,
    int PolicyVersion,
    string CalendarCode,
    DateTimeOffset? ResponseDueAt,
    DateTimeOffset? ResolutionDueAt);

public interface ISlaOrchestrationService
{
    Task<SlaStartResult> StartAsync(long slaId, long ticketId, SlaEvaluationContext context, long actor, DateTimeOffset startedAt, CancellationToken ct);
}
