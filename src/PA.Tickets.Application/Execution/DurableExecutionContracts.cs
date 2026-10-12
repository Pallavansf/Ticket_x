namespace PA.Tickets.Application.Execution;
public sealed record DurableMutationResult(bool Applied,bool AlreadyProcessed,string? Message);
public sealed record WorkflowInstanceCreationResult(long InstanceId,long WorkflowVersionId,bool AlreadyProcessed);
public interface IWorkflowInstanceCreationService
{ Task<WorkflowInstanceCreationResult> CreateAsync(long ticketId,long workflowId,long actor,string idempotencyKey,string? correlationId,CancellationToken ct,long? selectedWorkflowVersionId=null); }
public interface IWorkflowExecutionRepository
{ Task<DurableMutationResult> CompleteStepAsync(long workflowInstanceId,long stepInstanceId,long actor,string idempotencyKey,string? correlationId,CancellationToken ct); Task<DurableMutationResult> CompleteWorkItemAsync(string publicWorkItemId,long actor,string idempotencyKey,string? correlationId,CancellationToken ct); Task<DurableMutationResult> ActivateInitialStepAsync(long workflowInstanceId,long actor,string idempotencyKey,string? correlationId,CancellationToken ct); Task<DurableMutationResult> ReassignWorkItemAsync(string publicWorkItemId,long actor,long? departmentId,long? teamId,long? userId,string reason,string idempotencyKey,string? correlationId,CancellationToken ct); }
public enum DurableApprovalDecision { Approved, Rejected, Cancelled, Expired }
public enum DurableApprovalMode { AnyOne, AllRequired, Sequential }
public sealed record DurableApprovalApprover(long UserId,int Level=1,bool Required=true);
public sealed record DurableApprovalInstanceRequest(long TicketId,long? WorkflowStepInstanceId,DurableApprovalMode Mode,IReadOnlyCollection<DurableApprovalApprover> Approvers,long RequesterUserId,long CreatorUserId,long? ExecutorUserId,string RejectionBehavior="REJECT",long? ApprovalRuleVersionId=null,int? ExpiresAfterMinutes=null,bool DelegationAllowed=true);
public sealed record DurableApprovalInstanceResult(long InstanceId,IReadOnlyCollection<long> ApprovalIds,string Status);
public interface IApprovalExecutionRepository
{ Task<DurableApprovalInstanceResult> CreateInstanceAsync(DurableApprovalInstanceRequest request,long actor,string idempotencyKey,string? correlationId,CancellationToken ct); Task<DurableMutationResult> DecideAsync(long approvalId,long approver,DurableApprovalDecision decision,string? comments,string idempotencyKey,string? correlationId,CancellationToken ct); Task<DurableMutationResult> DelegateAsync(long approvalId,long originalApprover,long delegateUser,DateTimeOffset effectiveFrom,DateTimeOffset? effectiveTo,string reason,long actor,string idempotencyKey,string? correlationId,CancellationToken ct); }
public enum DurableSlaOperation { Pause, Resume, Breach, Complete }
public interface ISlaExecutionRepository
{ Task<DurableMutationResult> ApplyAsync(long slaId,DurableSlaOperation operation,string? reason,long actor,string idempotencyKey,string? correlationId,CancellationToken ct,DateTimeOffset? effectiveAt=null); }
public sealed record BackgroundJobEnvelope(long Id,string Type,string DedupeKey,string Payload,DateTimeOffset ScheduledAt,string? CorrelationId=null);
public interface IBackgroundJobRepository
{ Task<bool> EnqueueAsync(string type,string dedupeKey,string payload,DateTimeOffset scheduledAt,CancellationToken ct); Task<IReadOnlyCollection<BackgroundJobEnvelope>> LeaseReadyAsync(string workerId,int limit,CancellationToken ct); Task CompleteAsync(long id,CancellationToken ct); Task FailAsync(long id,string error,bool exhausted,CancellationToken ct); }
