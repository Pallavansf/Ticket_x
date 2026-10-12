using PA.Tickets.Domain.Sla;

namespace PA.Tickets.Application.Sla;

public sealed record SlaPolicyDefinitionView(long Id, string Code, string Name, string? Description, bool Active, int RowVersion);
public sealed record SlaPolicyVersionSummary(long Id, long PolicyId, int VersionNo, string Status, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record SlaConfigurationAudit(string Action, long ActorId, DateTimeOffset At, string? OldValue, string? NewValue, string? Reason);
public sealed record SlaPolicyVersionView(long Id, SlaPolicyVersion Policy, string Status, int RowVersion, IReadOnlyCollection<SlaConfigurationAudit> Audit);
public sealed record SlaPolicyVersionComparison(IReadOnlyCollection<string> ChangedFields);
public sealed record SlaValidationResult(bool IsValid, IReadOnlyCollection<string> Errors);
public sealed record SlaCalendarDefinitionView(string Code, string TimeZoneId, string WorkingHoursJson, string HolidaysJson, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, bool Active, int RowVersion);

public interface ISlaAdministrationRepository
{
    Task<IReadOnlyCollection<SlaPolicyDefinitionView>> ListPoliciesAsync(long actor, string? search, CancellationToken ct);
    Task<SlaPolicyDefinitionView> GetPolicyAsync(long actor, long policyId, CancellationToken ct);
    Task<long> CreatePolicyAsync(long actor, string code, string name, string? description, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task<SlaPolicyDefinitionView> UpdatePolicyAsync(long actor, long policyId, int expectedRowVersion, string code, string name, string? description, string? reason, string? correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<SlaPolicyVersionSummary>> ListVersionsAsync(long actor, long policyId, CancellationToken ct);
    Task<SlaPolicyVersionView> GetVersionAsync(long actor, long versionId, CancellationToken ct);
    Task<SlaPolicyVersionComparison> CompareVersionsAsync(long actor, long firstVersionId, long secondVersionId, CancellationToken ct);
    Task<SlaValidationResult> ValidateAsync(long actor, long policyId, SlaPolicyVersion version, CancellationToken ct);
    Task<long> SaveDraftAsync(long policyId, SlaPolicyVersion version, long actor, string idempotencyKey, int? expectedRowVersion, string? correlationId, CancellationToken ct);
    Task PublishAsync(long versionId, long actor, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task RetireAsync(long versionId, long actor, string idempotencyKey, string? reason, string? correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<SlaCalendarDefinitionView>> ListCalendarsAsync(long actor, CancellationToken ct);
    Task<SlaCalendarDefinitionView> GetCalendarAsync(long actor, string code, CancellationToken ct);
    Task CreateCalendarAsync(long actor, string code, BusinessCalendar calendar, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, string reason, string? correlationId, CancellationToken ct);
    Task UpdateCalendarAsync(long actor, string code, int expectedRowVersion, BusinessCalendar calendar, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, bool active, string reason, string? correlationId, CancellationToken ct);
}
