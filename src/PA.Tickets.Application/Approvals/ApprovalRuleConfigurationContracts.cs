using PA.Tickets.Domain.Approvals;

namespace PA.Tickets.Application.Approvals;

public sealed record ApprovalRuleDefinitionView(long Id, string Code, string Name, string? Description, bool Active);
public sealed record ApprovalRuleVersionSummary(long Id, long RuleId, int VersionNo, string Status, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record ApprovalRuleConfigurationAudit(string Action, long ActorId, DateTimeOffset At, string? OldValue, string? NewValue, string? Reason);
public sealed record ApprovalRuleVersionView(long Id, long RuleId, int VersionNo, string Status, ApprovalMode Mode, string RejectionBehavior, int? ExpiresAfterMinutes, bool DelegationAllowed, bool RequireRequesterSeparation, bool RequireCreatorSeparation, bool RequireExecutorSeparation, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, IReadOnlyCollection<ApprovalRuleStepDefinition> Steps, IReadOnlyCollection<ApprovalRuleConfigurationAudit> Audit);
public sealed record ApprovalRuleVersionComparison(IReadOnlyCollection<int> AddedSteps, IReadOnlyCollection<int> RemovedSteps, IReadOnlyCollection<int> ChangedSteps, bool ConfigurationChanged);
public sealed record ApprovalDecisionHistoryView(long Id,long ApprovalId,string Action,string? Decision,long ActorId,string? Comment,DateTimeOffset At,string? CorrelationId,string? IdempotencyKey);
public sealed record ApprovalExecutionHistoryView(long Id,int Level,bool Required,bool Active,string Status,long? ApproverUserId,DateTimeOffset? ExpiresAt,DateTimeOffset? DecidedAt,string? Decision,string? Comments,IReadOnlyCollection<ApprovalDecisionHistoryView> History);
public sealed record ApprovalInstanceHistoryView(long InstanceId,long TicketId,long? WorkflowStepInstanceId,long? RuleVersionId,int? RuleVersionNo,string Mode,string Status,long RequesterUserId,long CreatorUserId,long? ExecutorUserId,string RejectionBehavior,bool DelegationAllowed,DateTimeOffset CreatedAt,DateTimeOffset? CompletedAt,IReadOnlyCollection<ApprovalExecutionHistoryView> Approvals);
public sealed record SelectedApprovalRuleVersion(long RuleVersionId, long RuleId, int VersionNo, ApprovalMode Mode, IReadOnlyCollection<DurableApproverSelection> Approvers, string RejectionBehavior, int? ExpiresAfterMinutes, bool DelegationAllowed);
public sealed record DurableApproverSelection(long UserId, int Level, bool Required);

public interface IApprovalRuleConfigurationRepository
{
    Task<long> CreateRuleAsync(long actor, string code, string name, string? description, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<ApprovalRuleDefinitionView>> ListRulesAsync(long actor, string? search, CancellationToken ct);
    Task<ApprovalRuleDefinitionView> GetRuleAsync(long actor, long ruleId, CancellationToken ct);
    Task<ApprovalRuleDefinitionView> UpdateRuleAsync(long actor, long ruleId, string code, string name, string? description, string? reason, string? correlationId, CancellationToken ct);
    Task<ApprovalRuleDefinitionView> SetRuleActiveAsync(long actor,long ruleId,bool active,string? reason,string? correlationId,CancellationToken ct);
    Task<IReadOnlyCollection<ApprovalRuleVersionSummary>> ListVersionsAsync(long actor, long ruleId, CancellationToken ct);
    Task<ApprovalRuleVersionView> GetVersionAsync(long actor, long versionId, CancellationToken ct);
    Task<ApprovalRuleVersionComparison> CompareVersionsAsync(long actor, long firstVersionId, long secondVersionId, CancellationToken ct);
    Task<IReadOnlyCollection<ApprovalInstanceHistoryView>> GetTicketRuntimeHistoryAsync(long actor,long ticketId,CancellationToken ct);
    Task<long> SaveDraftAsync(long ruleId, ApprovalRuleVersion version, long actor, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task PublishAsync(long versionId, long actor, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task RetireAsync(long versionId, long actor, string idempotencyKey, string? reason, string? correlationId, CancellationToken ct);
}

public interface IApprovalRuleVersionResolver
{
    Task<SelectedApprovalRuleVersion> ResolveAsync(long versionId, DateTimeOffset at, CancellationToken ct);
    Task ValidateRangeAsync(long versionId, DateTimeOffset from, DateTimeOffset? to, CancellationToken ct);
}
