using PA.Tickets.Domain.Workflow;
namespace PA.Tickets.Application.Workflow;
public sealed record WorkflowDefinitionView(long Id,string Code,string Name,string? Description,bool Active);
public sealed record WorkflowVersionSummary(long Id,long WorkflowId,int VersionNo,string Status,DateTimeOffset? EffectiveFrom,DateTimeOffset? EffectiveTo);
public sealed record WorkflowConfigurationAudit(string Action,long ActorId,DateTimeOffset At,string? OldValue,string? NewValue,string? Reason);
public sealed record WorkflowVersionView(long Id,long WorkflowId,int VersionNo,string Status,DateTimeOffset? EffectiveFrom,DateTimeOffset? EffectiveTo,IReadOnlyCollection<WorkflowStepDefinition> Steps,IReadOnlyCollection<WorkflowTransitionDefinition> Transitions,IReadOnlyCollection<WorkflowConfigurationAudit> Audit);
public sealed record WorkflowVersionComparison(IReadOnlyCollection<string> AddedSteps,IReadOnlyCollection<string> RemovedSteps,IReadOnlyCollection<string> ChangedSteps,IReadOnlyCollection<string> AddedTransitions,IReadOnlyCollection<string> RemovedTransitions,IReadOnlyCollection<string> ChangedTransitions);
public sealed record WorkflowRuntimeEventView(string Action,string? OldStatus,string? NewStatus,long? ActorId,DateTimeOffset At,string? CorrelationId,string? IdempotencyKey);
public sealed record WorkflowStepHistoryView(long StepInstanceId,string StepCode,string Status,long? WorkItemId,long? ApprovalId,IReadOnlyCollection<WorkflowRuntimeEventView> Events);
public sealed record WorkflowInstanceHistoryView(long InstanceId,long TicketId,long WorkflowVersionId,int VersionNo,string Status,long? CurrentStepId,IReadOnlyCollection<WorkflowStepHistoryView> Steps);

public interface IWorkflowConfigurationRepository
{
	Task<long> CreateWorkflowAsync(long actor,string code,string name,string? description,string idempotencyKey,string? correlationId,CancellationToken ct);
	Task<IReadOnlyCollection<WorkflowDefinitionView>> ListWorkflowsAsync(long actor,string? search,CancellationToken ct);
	Task<WorkflowDefinitionView> GetWorkflowAsync(long actor,long workflowId,CancellationToken ct);
	Task<WorkflowDefinitionView> UpdateWorkflowAsync(long actor,long workflowId,string code,string name,string? description,string? reason,string? correlationId,CancellationToken ct);
	Task<WorkflowDefinitionView> SetWorkflowActiveAsync(long actor,long workflowId,bool active,string? reason,string? correlationId,CancellationToken ct);
	Task<IReadOnlyCollection<WorkflowVersionSummary>> ListVersionsAsync(long actor,long workflowId,CancellationToken ct);
	Task<WorkflowVersionView> GetVersionAsync(long actor,long versionId,CancellationToken ct);
	Task<WorkflowVersionComparison> CompareVersionsAsync(long actor,long firstVersionId,long secondVersionId,CancellationToken ct);
	Task<WorkflowValidationResult> ValidateAsync(long actor,long workflowId,WorkflowVersion version,CancellationToken ct);
	Task<IReadOnlyCollection<WorkflowInstanceHistoryView>> GetTicketRuntimeHistoryAsync(long actor,long ticketId,CancellationToken ct);
	Task<long> SaveDraftAsync(long workflowId,WorkflowVersion version,long actor,string idempotencyKey,CancellationToken ct,string? correlationId=null);
	Task PublishAsync(long versionId,long actor,string idempotencyKey,CancellationToken ct,string? correlationId=null);
	Task RetireAsync(long versionId,long actor,string idempotencyKey,string? reason,string? correlationId,CancellationToken ct);
}
