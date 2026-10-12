using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Application.Tickets;

public sealed record WaitingReasonDefinition(
    long Id,
    string Code,
    bool Active,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool PauseSla,
    bool RequireComment,
    bool RequireEvidence,
    bool RequireExpectedDate,
    IReadOnlySet<TicketStatus> AllowedFromStatuses);

public sealed record WaitingRequest(
    string Reason,
    string? Comment,
    string? EvidenceReference,
    DateTimeOffset? ExpectedDate);

public interface IWaitingReasonCatalog
{
    Task<WaitingReasonDefinition> ResolveAsync(string code, DateTimeOffset at, CancellationToken ct);
}
