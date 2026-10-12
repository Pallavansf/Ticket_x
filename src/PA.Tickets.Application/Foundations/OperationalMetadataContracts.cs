namespace PA.Tickets.Application.Foundations;

public sealed record StatusDefinitionView(
    long Id,
    string Code,
    string Name,
    string? Description,
    bool Active,
    bool IsInitial,
    bool IsOpen,
    bool IsClosed,
    bool IsCancelled,
    int DisplayOrder,
    int RowVersion);

public sealed record StatusTransitionDefinition(
    string FromCode,
    string ToCode,
    string? Condition,
    bool Allowed,
    int RowVersion = 0);

public sealed record StatusConfigurationAudit(string Action, long ActorId, DateTimeOffset At, string? OldValue, string? NewValue, string? Reason);

public sealed record PriorityDefinitionView(
    long Id,
    string Code,
    string Name,
    string? Description,
    int Level,
    int DisplayOrder,
    bool Active,
    bool IsDefault,
    int RowVersion);

public sealed record PriorityConfigurationAudit(string Action, long ActorId, DateTimeOffset At, string? OldValue, string? NewValue, string? Reason);

public interface ITicketStatusConfigurationRepository
{
    Task<IReadOnlyCollection<StatusDefinitionView>> ListStatusesAsync(long actor, string? search, CancellationToken ct);
    Task<StatusDefinitionView> GetStatusAsync(long actor, long statusId, CancellationToken ct);
    Task<bool> IsStatusActiveAsync(long actor, string code, CancellationToken ct);
    Task<StatusDefinitionView> CreateStatusAsync(long actor, string code, string name, string? description, bool isInitial, bool isOpen, bool isClosed, bool isCancelled, int displayOrder, string? reason, CancellationToken ct);
    Task<StatusDefinitionView> UpdateStatusAsync(long actor, long statusId, string code, string name, string? description, bool isInitial, bool isOpen, bool isClosed, bool isCancelled, int displayOrder, string? reason, CancellationToken ct, int? expectedRowVersion = null);
    Task<StatusDefinitionView> SetStatusActiveAsync(long actor, long statusId, bool active, string? reason, CancellationToken ct);
    Task<IReadOnlyCollection<StatusTransitionDefinition>> ListTransitionsAsync(long actor, CancellationToken ct);
    Task<StatusTransitionDefinition> SetTransitionAsync(long actor, string fromCode, string toCode, string? condition, bool allowed, string? reason, CancellationToken ct, int? expectedRowVersion = null);
    Task<bool> CanTransitionAsync(long actor, string fromCode, string toCode, CancellationToken ct);
    Task<IReadOnlyCollection<StatusConfigurationAudit>> GetStatusAuditAsync(long actor, long statusId, CancellationToken ct);
}

public interface ITicketPriorityConfigurationRepository
{
    Task<IReadOnlyCollection<PriorityDefinitionView>> ListPrioritiesAsync(long actor, string? search, CancellationToken ct);
    Task<PriorityDefinitionView> GetPriorityAsync(long actor, long priorityId, CancellationToken ct);
    Task<PriorityDefinitionView> CreatePriorityAsync(long actor, string code, string name, string? description, int level, int displayOrder, bool isDefault, string? reason, CancellationToken ct);
    Task<PriorityDefinitionView> UpdatePriorityAsync(long actor, long priorityId, string code, string name, string? description, int level, int displayOrder, bool isDefault, string? reason, CancellationToken ct, int? expectedRowVersion = null);
    Task<PriorityDefinitionView> SetPriorityActiveAsync(long actor, long priorityId, bool active, string? reason, CancellationToken ct);
    Task<PriorityDefinitionView> SetDefaultPriorityAsync(long actor, long priorityId, string? reason, CancellationToken ct);
    Task<PriorityDefinitionView> ResolvePriorityForSelectionAsync(long actor, long? priorityId, CancellationToken ct);
    Task<IReadOnlyCollection<PriorityConfigurationAudit>> GetPriorityAuditAsync(long actor, long priorityId, CancellationToken ct);
}
