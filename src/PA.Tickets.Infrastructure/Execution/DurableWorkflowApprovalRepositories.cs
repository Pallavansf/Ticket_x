using System.Data;using System.Security.Cryptography;using Microsoft.Extensions.Configuration;using System.Text;using System.Text.Json;using Microsoft.EntityFrameworkCore;using PA.Tickets.Application.Execution;using PA.Tickets.Application.Foundations;using PA.Tickets.Application.Tickets;using PA.Tickets.Domain.Tickets;using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Application.Approvals;
namespace PA.Tickets.Infrastructure.Execution;
public sealed class DurableWorkflowExecutionRepository(ExecutionDbContext db,TicketsDbContext ticketDb,IWorkflowRouter router,Microsoft.Extensions.Configuration.IConfiguration configuration,AuditChainCoordinator auditLocks,ProviderTransactionRetry retry,ITicketStatusConfigurationRepository statusConfiguration,IAuthorizationService authorization):IWorkflowExecutionRepository
{
 public Task<DurableMutationResult> CompleteStepAsync(long instanceId,long stepId,long actor,string key,string? correlationId,CancellationToken ct)=>CompleteStepInternalAsync(instanceId,stepId,null,actor,key,correlationId,ct);
 public async Task<DurableMutationResult> CompleteWorkItemAsync(string publicWorkItemId,long actor,string key,string? correlationId,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(publicWorkItemId)||publicWorkItemId.Length!=16||publicWorkItemId.Any(ch=>!"ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(ch)))throw new KeyNotFoundException("Work item was not found.");
  var work=await db.WorkItems.SingleOrDefaultAsync(x=>x.PublicId==publicWorkItemId,ct)??throw new KeyNotFoundException("Work item was not found.");
  var ticket=await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==work.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
  await authorization.EnsurePermissionAsync(actor,"TICKET_UPDATE",new AuthorizationResource(ticket.Id,work.ToDepartmentId,work.ToTeamId,ticket.ClientId,work.AssignedToUserId),ct);
  return await CompleteWorkItemInternalAsync(work.Id,actor,key,correlationId,ct);
 }
 private async Task<DurableMutationResult> CompleteWorkItemInternalAsync(long workItemId,long actor,string key,string? correlationId,CancellationToken ct){var work=await db.WorkItems.SingleOrDefaultAsync(x=>x.Id==workItemId,ct)??throw new KeyNotFoundException("Work item was not found.");if(work.WorkflowStepInstanceId is null)throw new InvalidOperationException("Work item is not linked to a workflow step.");var fingerprint=$"WORK_ITEM:{workItemId}";var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORKFLOW_STEP_COMPLETE"&&x.Key==key,ct);if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");return new(false,true,"Already processed.");}if(work.AssignedToUserId is null||work.AssignedToUserId!=actor)throw new UnauthorizedAccessException("Only the assigned user can complete this work item.");if(work.Status is not ("PENDING" or "IN_PROGRESS"))throw new InvalidOperationException("Work item is not executable.");var step=await db.WorkflowSteps.SingleOrDefaultAsync(x=>x.Id==work.WorkflowStepInstanceId.Value,ct)??throw new KeyNotFoundException("Workflow step instance was not found.");var result=await CompleteStepInternalAsync(step.WorkflowInstanceId,step.Id,workItemId,actor,key,correlationId,ct);return result;}
 public Task<DurableMutationResult> ActivateInitialStepAsync(long instanceId,long actor,string key,string? correlationId,CancellationToken ct)=>retry.ExecuteAsync(db,()=>ActivateInitialStepOnceAsync(instanceId,actor,key,correlationId,ct),ct); private async Task<DurableMutationResult> ActivateInitialStepOnceAsync(long instanceId,long actor,string key,string? correlationId,CancellationToken ct)
{
 await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
 if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency key is required.");
 var instance=await db.WorkflowInstances.SingleOrDefaultAsync(x=>x.Id==instanceId,ct)??throw new KeyNotFoundException("Workflow instance was not found.");
 var authorizationTicket=await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==instance.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
 await authorization.EnsurePermissionAsync(actor,"TICKET_UPDATE",new AuthorizationResource(authorizationTicket.Id,authorizationTicket.CurrentDepartmentId,authorizationTicket.CurrentTeamId,authorizationTicket.ClientId,authorizationTicket.CurrentAssignee),ct);
 var initialFingerprint=$"INSTANCE:{instanceId}";var priorInitial=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORKFLOW_INITIAL_ACTIVATE"&&x.Key==key,ct);if(priorInitial is not null){if(priorInitial.Fingerprint!=initialFingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");return new(false,true,"Already processed.");}
 if(instance.Status!="RUNNING")throw new InvalidOperationException("Workflow instance is not active.");
 if(await db.WorkflowSteps.AnyAsync(x=>x.WorkflowInstanceId==instanceId,ct))throw new InvalidOperationException("Workflow instance is already activated.");
 var defs=await db.WorkflowStepDefinitions.Where(x=>x.VersionId==instance.VersionId).ToListAsync(ct);var incomingIds=await db.WorkflowTransitions.Where(t=>t.VersionId==instance.VersionId).Select(t=>t.ToStepId).Distinct().ToListAsync(ct);var starts=defs.Where(d=>!incomingIds.Contains(d.Id)).ToList();if(starts.Count==0)throw new InvalidOperationException("Workflow has no initial step.");
 var ticket=await db.Tickets.SingleAsync(x=>x.Id==instance.TicketId,ct);var now=DateTimeOffset.UtcNow;foreach(var target in starts){var step=new WorkflowStepInstanceRow{WorkflowInstanceId=instanceId,WorkflowStepId=target.Id,Status="ACTIVE",StartedAt=now,RowVersion=0,JoinRule=target.ExecutionMode};db.WorkflowSteps.Add(step);await db.SaveChangesAsync(ct);if(string.Equals(target.Type,"WORK",StringComparison.OrdinalIgnoreCase)){var work=new WorkItemExecutionRow{TicketId=instance.TicketId,Number=$"WI-{step.Id:D10}",PublicId=await NextPublicIdAsync(ct),SequenceNo=target.StepNo,WorkflowStepId=target.Id,WorkflowStepInstanceId=step.Id,Status="PENDING",PriorityId=ticket.PriorityId,CreatedAt=now,CreatedBy=actor};db.WorkItems.Add(work);await db.SaveChangesAsync(ct);await RouteAndAssignAsync(work,ticket,target.Code,actor,now,ct);step.WorkItemId=work.Id;db.Events.Add(new EventRow{TicketId=instance.TicketId,WorkItemId=work.Id,Type="WORK_ITEM_CREATED",Category="WORKFLOW",ActorId=actor,NewValue=work.Number,At=now});await AddAudit("WORK_ITEM",work.Id.ToString(),"WORK_ITEM_CREATED",actor,now,null,work.Number,null,ct);}db.WorkflowStepHistory.Add(new WorkflowStepHistoryRow{StepInstanceId=step.Id,Action="STEP_ACTIVATED",OldStatus="PENDING",NewStatus="ACTIVE",ActorId=actor,EventAt=now,CorrelationId=correlationId,IdempotencyKey=key});db.Events.Add(new EventRow{TicketId=instance.TicketId,Type="WORKFLOW_STEP_ACTIVATED",Category="WORKFLOW",ActorId=actor,NewValue=target.Code,At=now});}
 instance.CurrentStepId=starts.Count==1?(await db.WorkflowSteps.Where(x=>x.WorkflowInstanceId==instanceId).Select(x=>(long?)x.Id).FirstAsync(ct)):instance.CurrentStepId;db.Idempotency.Add(new IdempotencyRow{Scope="WORKFLOW_INITIAL_ACTIVATE",Key=key,Fingerprint=initialFingerprint,Status="COMPLETED",CreatedAt=now,CompletedAt=now});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(true,false,"Initial workflow step activated.");
}
private Task<DurableMutationResult> CompleteStepInternalAsync(long instanceId,long stepId,long? workItemId,long actor,string key,string? correlationId,CancellationToken ct)=>retry.ExecuteAsync(db,()=>CompleteStepInternalOnceAsync(instanceId,stepId,workItemId,actor,key,correlationId,ct),ct); private async Task<DurableMutationResult> CompleteStepInternalOnceAsync(long instanceId,long stepId,long? workItemId,long actor,string key,string? correlationId,CancellationToken ct){if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency key is required.");await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var completionFingerprint=workItemId is null?$"STEP:{instanceId}:{stepId}":$"WORK_ITEM:{workItemId}";var priorCompletion=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORKFLOW_STEP_COMPLETE"&&x.Key==key,ct);if(priorCompletion is not null){if(priorCompletion.Fingerprint!=completionFingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");return new(false,true,"Already processed.");}var step=await db.WorkflowSteps.SingleOrDefaultAsync(x=>x.Id==stepId&&x.WorkflowInstanceId==instanceId,ct)??throw new KeyNotFoundException("Workflow step instance was not found."); if(workItemId is not null){var work=await db.WorkItems.SingleOrDefaultAsync(x=>x.Id==workItemId.Value,ct)??throw new KeyNotFoundException("Work item was not found.");if(work.WorkflowStepInstanceId!=stepId)throw new InvalidOperationException("Work item does not belong to the workflow step.");if(work.AssignedToUserId is null||work.AssignedToUserId!=actor)throw new UnauthorizedAccessException("Only the assigned user can complete this work item.");if(work.Status is not ("PENDING" or "IN_PROGRESS"))throw new InvalidOperationException("Work item is not executable.");work.Status="COMPLETED";work.CompletedAt=DateTimeOffset.UtcNow;db.Events.Add(new EventRow{TicketId=work.TicketId,WorkItemId=work.Id,Type="WORK_ITEM_COMPLETED",Category="WORKFLOW",ActorId=actor,NewValue=work.Status,At=work.CompletedAt.Value});await AddAudit("WORK_ITEM",work.Id.ToString(),"WORK_ITEM_COMPLETED",actor,work.CompletedAt.Value,null,work.Status,null,ct);await db.SaveChangesAsync(ct);}if(step.Status is not("ACTIVE" or "WAITING"))throw new InvalidOperationException("Only active or waiting steps can complete.");var now=DateTimeOffset.UtcNow;var old=step.Status;step.Status="COMPLETED";step.CompletedAt=now;step.RowVersion++;db.Idempotency.Add(new IdempotencyRow{Scope="WORKFLOW_STEP_COMPLETE",Key=key,Fingerprint=completionFingerprint,Status="COMPLETED",CreatedAt=now,CompletedAt=now});db.WorkflowStepHistory.Add(new WorkflowStepHistoryRow{StepInstanceId=stepId,Action="STEP_COMPLETED",OldStatus=old,NewStatus=step.Status,ActorId=actor,EventAt=now,CorrelationId=correlationId,IdempotencyKey=key});var instance=await db.WorkflowInstances.SingleAsync(x=>x.Id==instanceId,ct);
  if(workItemId is long linkedWorkItemId&&!await db.WorkItems.AnyAsync(x=>x.Id==linkedWorkItemId&&x.TicketId==instance.TicketId,ct))throw new InvalidOperationException("Work item does not belong to the workflow instance ticket.");
  var definitions=await db.WorkflowStepDefinitions.Where(x=>x.VersionId==instance.VersionId).ToListAsync(ct); var ticket=await db.Tickets.SingleAsync(x=>x.Id==instance.TicketId,ct); var context=new Dictionary<string,string?>{{"TicketType",ticket.TypeId.ToString()},{"Priority",ticket.PriorityId?.ToString()},{"ClientId",ticket.ClientId?.ToString()},{"ServiceId",ticket.ServiceId?.ToString()}};
  var transitions=await db.WorkflowTransitions.Where(x=>x.VersionId==instance.VersionId&&x.FromStepId==step.WorkflowStepId).ToListAsync(ct);
  var existingInstances=await db.WorkflowSteps.Where(x=>x.WorkflowInstanceId==instanceId).ToListAsync(ct);
  var activated=0;
  foreach(var transition in transitions){
   if(transition.Condition is not null){var condition=JsonSerializer.Deserialize<PA.Tickets.Domain.Workflow.WorkflowCondition>(transition.Condition);if(condition is null||!condition.Evaluate(context))continue;}
   var target=definitions.Single(x=>x.Id==transition.ToStepId); if(existingInstances.Any(x=>x.WorkflowStepId==target.Id))continue;
   var join=target.ExecutionMode.ToUpperInvariant(); var incoming=await db.WorkflowTransitions.Where(x=>x.VersionId==instance.VersionId&&x.ToStepId==target.Id).Select(x=>x.FromStepId).ToListAsync(ct); var completedIncoming=existingInstances.Count(x=>incoming.Contains(x.WorkflowStepId)&&x.Status=="COMPLETED"); var ready=join is "ANYONE" or "ANY_ONE" ? completedIncoming>=1 : incoming.Count==0 || incoming.All(x=>existingInstances.Any(i=>i.WorkflowStepId==x&&i.Status=="COMPLETED")); if(!ready)continue;
   var next=new WorkflowStepInstanceRow{WorkflowInstanceId=instanceId,WorkflowStepId=target.Id,Status="ACTIVE",StartedAt=now,RowVersion=0,JoinRule=join}; db.WorkflowSteps.Add(next); await db.SaveChangesAsync(ct); if(string.Equals(target.Type,"WORK",StringComparison.OrdinalIgnoreCase)){var work=new WorkItemExecutionRow{TicketId=instance.TicketId,Number=$"WI-{next.Id:D10}",PublicId=await NextPublicIdAsync(ct),SequenceNo=target.StepNo,WorkflowStepId=target.Id,WorkflowStepInstanceId=next.Id,Status="PENDING",CreatedAt=now,CreatedBy=actor};db.WorkItems.Add(work);await db.SaveChangesAsync(ct);await RouteAndAssignAsync(work,ticket,target.Code,actor,now,ct);next.WorkItemId=work.Id;db.Events.Add(new EventRow{TicketId=instance.TicketId,WorkItemId=work.Id,Type="WORK_ITEM_CREATED",Category="WORKFLOW",ActorId=actor,NewValue=work.Number,At=now});await AddAudit("WORK_ITEM",work.Id.ToString(),"WORK_ITEM_CREATED",actor,now,null,work.Number,null,ct);await db.SaveChangesAsync(ct);} existingInstances.Add(next); activated++; db.WorkflowStepHistory.Add(new WorkflowStepHistoryRow{StepInstanceId=next.Id,Action="STEP_ACTIVATED",OldStatus="PENDING",NewStatus="ACTIVE",ActorId=actor,EventAt=now,CorrelationId=correlationId,IdempotencyKey=key}); db.Events.Add(new EventRow{TicketId=instance.TicketId,Type="WORKFLOW_STEP_ACTIVATED",Category="WORKFLOW",ActorId=actor,NewValue=target.Code,At=now});
  }
  if(activated==1)instance.CurrentStepId=existingInstances.Last().Id; if(activated==0&&!await db.WorkflowTransitions.AnyAsync(x=>x.VersionId==instance.VersionId&&x.FromStepId==step.WorkflowStepId,ct))instance.Status="COMPLETED"; var projection=await db.Tickets.SingleAsync(x=>x.Id==instance.TicketId,ct);var targetStatus=instance.Status=="COMPLETED"?TicketStatus.Resolved:TicketStatus.InProgress;var currentStatus=(TicketStatus)projection.StatusId;if(currentStatus!=targetStatus){await TicketStatusTransitionPolicy.EnsureAllowedAsync(statusConfiguration,actor,currentStatus,targetStatus,ct);projection.StatusId=(int)targetStatus;await AddAudit("TICKET",projection.Id.ToString(),"TICKET_STATUS_CHANGED",actor,now,TicketStatusCodes.ToConfigurationCode(currentStatus),TicketStatusCodes.ToConfigurationCode(targetStatus),null,ct);db.Events.Add(new EventRow{TicketId=projection.Id,Type="TICKET_STATUS_CHANGED",Category="LIFECYCLE",ActorId=actor,OldValue=TicketStatusCodes.ToConfigurationCode(currentStatus),NewValue=TicketStatusCodes.ToConfigurationCode(targetStatus),At=now});}db.Events.Add(new EventRow{TicketId=instance.TicketId,Type="WORKFLOW_STEP_COMPLETED",Category="WORKFLOW",ActorId=actor,NewValue=stepId.ToString(),At=now});await AddAudit("WORKFLOW_STEP_INSTANCE",stepId.ToString(),"STEP_COMPLETED",actor,now,old,step.Status,null,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(true,false,"Workflow step completed.");}
 public async Task<DurableMutationResult> ReassignWorkItemAsync(string publicWorkItemId,long actor,long? departmentId,long? teamId,long? userId,string reason,string key,string? correlationId,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(publicWorkItemId))throw new ArgumentException("Public work-item ID is required.");
  if(departmentId is null&&teamId is null&&userId is null)throw new InvalidOperationException("At least one assignment target is required.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  var assignmentFingerprint=$"{publicWorkItemId}:{departmentId}:{teamId}:{userId}:{reason}";
  var work=await db.WorkItems.SingleOrDefaultAsync(x=>x.PublicId==publicWorkItemId,ct)??throw new KeyNotFoundException("Work item was not found.");
  var ticket=await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==work.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
  await authorization.EnsurePermissionAsync(actor,"TICKET_UPDATE",new AuthorizationResource(ticket.Id,work.ToDepartmentId,work.ToTeamId,ticket.ClientId,work.AssignedToUserId),ct);
  EnsureAssignmentAuthorized(actor,departmentId,teamId,userId);
  var targetTeam=teamId.HasValue?await ticketDb.AdministrationTeams.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==teamId&&x.Active,ct)??throw new ArgumentException("The assignment team is not active."):null;
  var targetUser=userId.HasValue?await ticketDb.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==userId&&x.Active,ct)??throw new ArgumentException("The assignment user is not active."):null;
  var effectiveDepartmentId=departmentId??targetTeam?.DepartmentId??targetUser?.DepartmentId;
  var effectiveTeamId=teamId??targetUser?.TeamId;
  if(targetTeam is not null&&targetTeam.DepartmentId!=effectiveDepartmentId)throw new ArgumentException("The assignment team does not belong to the selected department.");
  if(targetUser is not null&&(targetUser.DepartmentId!=effectiveDepartmentId||targetUser.TeamId!=effectiveTeamId))throw new ArgumentException("The assignment user does not belong to the selected department and team.");
  await authorization.EnsurePermissionAsync(actor,"TICKET_UPDATE",new AuthorizationResource(ticket.Id,effectiveDepartmentId,effectiveTeamId,ticket.ClientId),ct);
  var priorAssignment=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="WORK_ITEM_ASSIGN"&&x.Key==key,ct);
  if(priorAssignment is not null){if(priorAssignment.Fingerprint!=assignmentFingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");return new(false,true,"Already processed.");}
  if(work.Status is "COMPLETED" or "CANCELLED")throw new InvalidOperationException("Completed or cancelled work items cannot be reassigned.");
  var now=DateTimeOffset.UtcNow;
  var active=await db.Assignments.Where(x=>x.WorkItemId==work.Id&&x.UnassignedAt==null).ToListAsync(ct);
  foreach(var old in active)old.UnassignedAt=now;
  var type=userId.HasValue?"USER":teamId.HasValue?"TEAM":"DEPARTMENT";
  work.ToDepartmentId=effectiveDepartmentId;work.ToTeamId=effectiveTeamId;work.AssignedToUserId=userId;
  db.Assignments.Add(new AssignmentExecutionRow{TicketId=work.TicketId,WorkItemId=work.Id,DepartmentId=effectiveDepartmentId,TeamId=effectiveTeamId,AssignedTo=userId,Type=type,AssignedAt=now,UnassignedAt=null,AssignedBy=actor,Reason=reason});
  db.Events.Add(new EventRow{TicketId=work.TicketId,WorkItemId=work.Id,Type="WORK_ITEM_REASSIGNED",Category="WORKFLOW",ActorId=actor,NewValue=publicWorkItemId,Reason=reason,At=now});
  await AddAudit("WORK_ITEM",work.Id.ToString(),"WORK_ITEM_REASSIGNED",actor,now,null,publicWorkItemId,reason,ct);
  db.Idempotency.Add(new IdempotencyRow{Scope="WORK_ITEM_ASSIGN",Key=key,Fingerprint=assignmentFingerprint,Status="COMPLETED",CreatedAt=now,CompletedAt=now});
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(true,false,"Work item reassigned.");
 }
 private async Task<string> NextPublicIdAsync(CancellationToken ct){for(var attempt=0;attempt<8;attempt++){var candidate=CreatePublicId();if(!await db.WorkItems.AnyAsync(x=>x.PublicId==candidate,ct))return candidate;}throw new InvalidOperationException("Unable to allocate a unique public work-item identifier.");} private static string CreatePublicId(){const string chars="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";Span<byte> bytes=stackalloc byte[16];RandomNumberGenerator.Fill(bytes);var result=new char[16];for(var i=0;i<result.Length;i++)result[i]=chars[bytes[i]%chars.Length];return new string(result);} private async Task AddAudit(string type,string id,string action,long actor,DateTimeOffset at,string? oldValue,string? newValue,string? reason,CancellationToken ct){await auditLocks.AcquireAsync(db,type,id,ct);var prev=await db.Audits.Where(x=>x.EntityType==type&&x.EntityId==id).OrderByDescending(x=>x.Id).Select(x=>x.Hash).FirstOrDefaultAsync(ct);var canonical=JsonSerializer.Serialize(new{entityType=type,entityId=id,action,actorUserId=actor,auditAtUtc=at.UtcDateTime,oldValue,newValue,reason,previousHash=prev});var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();db.Audits.Add(new AuditRow{EntityType=type,EntityId=id,Action=action,ActorId=actor,At=at,OldValue=oldValue,NewValue=newValue,Reason=reason,Hash=hash,PreviousHash=prev});}
 private void EnsureAssignmentAuthorized(long actor,long? departmentId,long? teamId,long? userId){var actors=configuration.GetSection("Security:AssignmentActors").Get<long[]>()??[];if(actors.Length>0&&!actors.Contains(actor))throw new UnauthorizedAccessException("Actor is not permitted to assign work items.");var departments=configuration.GetSection("Security:AllowedAssignmentDepartments").Get<long[]>()??[];if(departmentId.HasValue&&departments.Length>0&&!departments.Contains(departmentId.Value))throw new UnauthorizedAccessException("Department is outside the actor assignment scope.");var teams=configuration.GetSection("Security:AllowedAssignmentTeams").Get<long[]>()??[];if(teamId.HasValue&&teams.Length>0&&!teams.Contains(teamId.Value))throw new UnauthorizedAccessException("Team is outside the actor assignment scope.");}
 private async Task RouteAndAssignAsync(WorkItemExecutionRow work,TicketExecutionRow ticket,string stepCode,long actor,DateTimeOffset at,CancellationToken ct){var step=work.WorkflowStepId is long stepId?await db.WorkflowStepDefinitions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==stepId,ct):null;var decision=router.Resolve(new WorkflowRoutingContext(ticket.Id,ticket.TypeId,ticket.PriorityId,ticket.ClientId,ticket.ServiceId,stepCode,step?.DepartmentId??ticket.CurrentDepartmentId,step?.TeamId??ticket.CurrentTeamId,ticket.CurrentAssignee));EnsureAssignmentAuthorized(actor,decision.DepartmentId,decision.TeamId,decision.UserId);if(ticket.StatusId!=(int)TicketStatus.Routed){var previousStatus=(TicketStatus)ticket.StatusId;await TicketStatusTransitionPolicy.EnsureAllowedAsync(statusConfiguration,actor,previousStatus,TicketStatus.Routed,ct);ticket.StatusId=(int)TicketStatus.Routed;db.Events.Add(new EventRow{TicketId=ticket.Id,Type="TICKET_STATUS_CHANGED",Category="LIFECYCLE",ActorId=actor,OldValue=TicketStatusCodes.ToConfigurationCode(previousStatus),NewValue=TicketStatusCodes.ToConfigurationCode(TicketStatus.Routed),Reason=decision.RuleCode,At=at});await AddAudit("TICKET",ticket.Id.ToString(),"TICKET_STATUS_CHANGED",actor,at,TicketStatusCodes.ToConfigurationCode(previousStatus),TicketStatusCodes.ToConfigurationCode(TicketStatus.Routed),decision.RuleCode,ct);}work.ToDepartmentId=decision.DepartmentId;work.ToTeamId=decision.TeamId;work.AssignedToUserId=decision.UserId;ticket.CurrentDepartmentId=decision.DepartmentId;ticket.CurrentTeamId=decision.TeamId;ticket.CurrentAssignee=decision.UserId;db.Assignments.Add(new AssignmentExecutionRow{TicketId=work.TicketId,WorkItemId=work.Id,DepartmentId=decision.DepartmentId,TeamId=decision.TeamId,AssignedTo=decision.UserId,Type=decision.TargetType,AssignedAt=at,UnassignedAt=null,AssignedBy=actor,Reason=decision.RuleCode});db.Events.Add(new EventRow{TicketId=work.TicketId,WorkItemId=work.Id,Type="WORK_ITEM_ASSIGNED",Category="WORKFLOW",ActorId=actor,NewValue=decision.RuleCode,At=at});await AddAudit("WORK_ITEM",work.Id.ToString(),"WORK_ITEM_ASSIGNED",actor,at,null,decision.RuleCode,null,ct);await db.SaveChangesAsync(ct);}
}
public sealed class DurableApprovalExecutionRepository(ExecutionDbContext db,TicketsDbContext ticketDb,IAuthorizationService authorization,IApprovalRuleVersionResolver approvalRules,IDatabaseProviderCapabilities capabilities,ProviderTransactionRetry retry,AuditChainCoordinator auditLocks):IApprovalExecutionRepository
{
 public async Task<DurableApprovalInstanceResult> CreateInstanceAsync(DurableApprovalInstanceRequest request,long actor,string key,string? correlationId,CancellationToken ct)
 {
  try{return await retry.ExecuteAsync(db,()=>CreateInstanceOnceAsync(request,actor,key,correlationId,ct),ct);}
  catch(DbUpdateException ex) when(capabilities.IsUniqueConstraintViolation(ex)){db.ChangeTracker.Clear();var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="APPROVAL_INSTANCE_CREATE"&&x.Key==key,ct);if(prior is null)throw;var normalizedMode=NormalizeMode(request.Mode);var normalizedApprovers=NormalizeApprovers(request.Approvers);if(prior.Fingerprint!=CreateInstanceFingerprint(request,normalizedMode,normalizedApprovers))throw new InvalidOperationException("Idempotency key was reused for a different request.");var existing=await db.ApprovalInstances.Where(x=>x.TicketId==request.TicketId&&x.Mode==normalizedMode).OrderByDescending(x=>x.Id).FirstAsync(ct);var ids=await db.Approvals.Where(x=>x.ApprovalInstanceId==existing.Id).OrderBy(x=>x.Level).ThenBy(x=>x.Id).Select(x=>x.Id).ToListAsync(ct);return new(existing.Id,ids,existing.Status);}
 }
 private async Task ValidateCreationContextAsync(DurableApprovalInstanceRequest request,long actor,string normalizedMode,IReadOnlyCollection<DurableApprovalApprover> approvers,CancellationToken ct)
 {
  if(request.CreatorUserId!=actor)throw new UnauthorizedAccessException("The approval creator must be the authenticated actor.");
  var ticket=await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==request.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
  await authorization.EnsurePermissionAsync(actor,"TICKET_UPDATE",new AuthorizationResource(ticket.Id,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.ClientId,ticket.CurrentAssignee),ct);
  if(request.RequesterUserId!=ticket.RequestedBy)throw new UnauthorizedAccessException("The approval requester must match the ticket requester.");
  if(request.ExecutorUserId.HasValue&&request.ExecutorUserId!=ticket.CurrentAssignee)throw new UnauthorizedAccessException("The approval executor must match the ticket's current assignee.");

  if(request.WorkflowStepInstanceId is long stepInstanceId)
  {
   var step=await db.WorkflowSteps.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==stepInstanceId,ct)??throw new KeyNotFoundException("Workflow step instance was not found.");
   var instance=await db.WorkflowInstances.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==step.WorkflowInstanceId&&x.TicketId==ticket.Id,ct);
   if(instance is null)throw new UnauthorizedAccessException("The workflow step does not belong to the ticket.");
   if(!await db.WorkflowStepDefinitions.AsNoTracking().AnyAsync(x=>x.Id==step.WorkflowStepId&&x.VersionId==instance.VersionId&&x.Type=="APPROVAL",ct))
    throw new InvalidOperationException("The workflow step is not an approval step in the ticket's workflow version.");
  }

  long? configuredRuleVersionId=null;
  if(ticket.RequestTypeId is long requestTypeId)
  {
   var requestType=await ticketDb.CatalogueRequestTypes.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==requestTypeId,ct);
   configuredRuleVersionId=requestType?.ApprovalRuleVersionId;
  }
  if(configuredRuleVersionId!=request.ApprovalRuleVersionId)
   throw new UnauthorizedAccessException("The approval rule version does not match the ticket's configured request type.");
  if(request.ApprovalRuleVersionId is not long ruleVersionId)return;

  var rule=await approvalRules.ResolveAsync(ruleVersionId,DateTimeOffset.UtcNow,ct);
  var ruleMode=rule.Mode switch
  {
   PA.Tickets.Domain.Approvals.ApprovalMode.AnyOne=>"ANY_ONE",
   PA.Tickets.Domain.Approvals.ApprovalMode.AllRequired=>"ALL_REQUIRED",
   PA.Tickets.Domain.Approvals.ApprovalMode.Sequential=>"SEQUENTIAL",
   _=>throw new InvalidOperationException("The configured approval mode is unsupported.")
  };
  var ruleApprovers=NormalizeApprovers(rule.Approvers.Select(x=>new DurableApprovalApprover(x.UserId,x.Level,x.Required)).ToArray());
  if(normalizedMode!=ruleMode||!approvers.SequenceEqual(ruleApprovers)||
     !string.Equals(request.RejectionBehavior,rule.RejectionBehavior,StringComparison.OrdinalIgnoreCase)||
     request.ExpiresAfterMinutes!=rule.ExpiresAfterMinutes||request.DelegationAllowed!=rule.DelegationAllowed)
   throw new UnauthorizedAccessException("The approval request does not match the configured approval rule.");
 }

 private async Task<DurableApprovalInstanceResult> CreateInstanceOnceAsync(DurableApprovalInstanceRequest request,long actor,string key,string? correlationId,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency key is required.");
  if(request.Approvers.Count==0)throw new InvalidOperationException("At least one approver is required.");
  var normalizedMode=NormalizeMode(request.Mode);
  var approvers=NormalizeApprovers(request.Approvers);
  if(approvers.Any(x=>x.UserId<=0))throw new InvalidOperationException("Approver users must be valid.");
  if(request.ExpiresAfterMinutes is <=0)throw new ArgumentException("Approval expiry must be positive when configured.");
  var sodUsers=new[]{request.RequesterUserId,request.CreatorUserId,request.ExecutorUserId??0}.Where(x=>x>0).ToHashSet();
  if(approvers.Any(x=>sodUsers.Contains(x.UserId)))throw new InvalidOperationException("Requester, creator, or executor cannot approve their own work.");
  var fingerprint=CreateInstanceFingerprint(request,normalizedMode,approvers);
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await ValidateCreationContextAsync(request,actor,normalizedMode,approvers,ct);
  foreach(var approver in approvers)await EnsureApprovalParticipantAsync(approver.UserId,request.TicketId,ct);
  var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="APPROVAL_INSTANCE_CREATE"&&x.Key==key,ct);
  if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");var existing=await db.ApprovalInstances.Where(x=>x.TicketId==request.TicketId&&x.Mode==normalizedMode).OrderByDescending(x=>x.Id).FirstOrDefaultAsync(ct);IReadOnlyCollection<long> ids=existing is null?Array.Empty<long>():await db.Approvals.Where(x=>x.ApprovalInstanceId==existing.Id).OrderBy(x=>x.Level).ThenBy(x=>x.Id).Select(x=>x.Id).ToListAsync(ct);return new(existing?.Id??0,ids,existing?.Status??"PENDING");}
  var now=DateTimeOffset.UtcNow;var firstLevel=approvers.Min(x=>x.Level);
  if(request.ApprovalRuleVersionId is long ruleVersionId)
  {
   var ruleVersion=await db.ApprovalRuleVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ruleVersionId&&x.Status=="PUBLISHED",ct)??throw new InvalidOperationException("The selected approval rule version is no longer published.");
   if(ruleVersion.EffectiveFrom>now||ruleVersion.EffectiveTo is not null&&ruleVersion.EffectiveTo<=now)throw new InvalidOperationException("The selected approval rule version is not effective.");
  }
  var instance=new ApprovalInstanceExecutionRow{TicketId=request.TicketId,WorkflowStepInstanceId=request.WorkflowStepInstanceId,Mode=normalizedMode,Status="PENDING",RequesterUserId=request.RequesterUserId,CreatorUserId=request.CreatorUserId,ExecutorUserId=request.ExecutorUserId,RejectionBehavior=string.IsNullOrWhiteSpace(request.RejectionBehavior)?"REJECT":request.RejectionBehavior.ToUpperInvariant(),CurrentLevel=firstLevel,CreatedAt=now};
  db.ApprovalInstances.Add(instance);db.Entry(instance).Property<long?>("ApprovalRuleVersionId").CurrentValue=request.ApprovalRuleVersionId;db.Entry(instance).Property<bool>("DelegationAllowed").CurrentValue=request.DelegationAllowed;await db.SaveChangesAsync(ct);
  foreach(var approver in approvers){var active=normalizedMode!="SEQUENTIAL"||approver.Level==firstLevel;var row=new ApprovalExecutionRow{ApprovalInstanceId=instance.Id,TicketId=request.TicketId,Level=approver.Level,Required=approver.Required,Active=active,Status="PENDING",ApproverId=approver.UserId,ExpiresAt=request.ExpiresAfterMinutes.HasValue?now.AddMinutes(request.ExpiresAfterMinutes.Value):null};db.Approvals.Add(row);}
  db.Idempotency.Add(new IdempotencyRow{Scope="APPROVAL_INSTANCE_CREATE",Key=key,Fingerprint=fingerprint,Status="COMPLETED",CreatedAt=now,CompletedAt=now});
  db.Events.Add(new EventRow{TicketId=request.TicketId,Type="APPROVAL_INSTANCE_CREATED",Category="APPROVAL",ActorId=actor,NewValue=normalizedMode,At=now});
  await AddAudit("APPROVAL_INSTANCE",instance.Id.ToString(),"APPROVAL_INSTANCE_CREATED",actor,now,null,normalizedMode,null,ct);
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  var createdIds=await db.Approvals.Where(x=>x.ApprovalInstanceId==instance.Id).OrderBy(x=>x.Level).ThenBy(x=>x.Id).Select(x=>x.Id).ToListAsync(ct);
  return new(instance.Id,createdIds,instance.Status);
 }
 public Task<DurableMutationResult> DecideAsync(long approvalId,long approver,DurableApprovalDecision decision,string? comments,string key,string? correlationId,CancellationToken ct)=>retry.ExecuteAsync(db,()=>DecideOnceAsync(approvalId,approver,decision,comments,key,correlationId,ct),ct);
 private async Task<DurableMutationResult> DecideOnceAsync(long approvalId,long approver,DurableApprovalDecision decision,string? comments,string key,string? correlationId,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency key is required.");
  var decisionValue=decision.ToString().ToUpperInvariant();var fingerprint=$"{approvalId}:{approver}:{decisionValue}:{comments}";
  try
  {
   await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
   var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="APPROVAL_DECISION"&&x.Key==key,ct);
   if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");return new(false,true,"Already processed.");}
  var row=await db.Approvals.SingleOrDefaultAsync(x=>x.Id==approvalId,ct)??throw new KeyNotFoundException("Approval was not found.");var now=DateTimeOffset.UtcNow;
   var authorized=row.ApproverId==approver||await db.ApprovalDelegations.AnyAsync(x=>x.ApprovalId==approvalId&&x.DelegateUser==approver&&x.EffectiveFrom<=now&&(x.EffectiveTo==null||x.EffectiveTo>=now),ct);if(!authorized)throw new UnauthorizedAccessException("Approver is not authorized or delegation is expired.");
   await EnsureApprovalParticipantAsync(approver,row.TicketId,ct);
   var approvalInstance=row.ApprovalInstanceId is long instanceId?await db.ApprovalInstances.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==instanceId,ct):null;
   var ticket=await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==row.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
   if(approver==ticket.RequestedBy||approver==ticket.CreatedBy||approver==ticket.CurrentAssignee||approvalInstance is not null&&(approver==approvalInstance.RequesterUserId||approver==approvalInstance.CreatorUserId||approver==approvalInstance.ExecutorUserId))
    throw new UnauthorizedAccessException("Requester, creator, or executor cannot approve their own work.");
  if(row.ExpiresAt is not null&&row.ExpiresAt<=now)decisionValue="EXPIRED";
  if(row.Status!="PENDING")throw new InvalidOperationException("Approval is no longer pending.");
   var old=row.Status;row.Status=decisionValue;row.Decision=decisionValue;row.Comments=comments;row.DecidedAt=now;row.RowVersion++;
   db.Idempotency.Add(new IdempotencyRow{Scope="APPROVAL_DECISION",Key=key,Fingerprint=fingerprint,Status="COMPLETED",CreatedAt=now,CompletedAt=now});
   db.ApprovalHistory.Add(new ApprovalHistoryExecutionRow{ApprovalId=approvalId,Action="DECISION",Decision=row.Decision,ActorId=approver,Comment=comments,CreatedAt=now,CorrelationId=correlationId,IdempotencyKey=key});
   db.Events.Add(new EventRow{TicketId=row.TicketId,Type="APPROVAL_DECIDED",Category="APPROVAL",ActorId=approver,OldValue=old,NewValue=row.Decision,At=now});
   await AddAudit("APPROVAL",approvalId.ToString(),"APPROVAL_DECIDED",approver,now,old,row.Decision,comments,ct);
   if(row.ApprovalInstanceId is not null)await AdvanceInstanceAsync(row.ApprovalInstanceId.Value,decisionValue,approver,now,correlationId,key,ct);
   await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(true,false,row.Decision);
  }
  catch(DbUpdateConcurrencyException ex){throw new InvalidOperationException("Approval decision conflicted with a concurrent update.",ex);}
  catch(DbUpdateException ex) when (capabilities.IsUniqueConstraintViolation(ex)){db.ChangeTracker.Clear();var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="APPROVAL_DECISION"&&x.Key==key,ct);if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");return new(false,true,"Already processed.");}throw;}
  catch(DbUpdateException ex) when (ex.InnerException?.Message.Contains("40001",StringComparison.OrdinalIgnoreCase)==true||ex.InnerException?.Message.Contains("could not serialize",StringComparison.OrdinalIgnoreCase)==true){throw new InvalidOperationException("Approval decision conflicted with a concurrent update.",ex);}
 }
 public async Task<DurableMutationResult> DelegateAsync(long approvalId,long original,long delegateUser,DateTimeOffset from,DateTimeOffset? to,string reason,long actor,string key,string? correlationId,CancellationToken ct)
 {
  try{return await retry.ExecuteAsync(db,()=>DelegateOnceAsync(approvalId,original,delegateUser,from,to,reason,actor,key,correlationId,ct),ct);}
  catch(DbUpdateException ex) when(capabilities.IsUniqueConstraintViolation(ex)){db.ChangeTracker.Clear();var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="APPROVAL_DELEGATION"&&x.Key==key,ct);if(prior is not null)return new(false,true,"Already processed.");throw;}
 }
 private async Task<DurableMutationResult> DelegateOnceAsync(long approvalId,long original,long delegateUser,DateTimeOffset from,DateTimeOffset? to,string reason,long actor,string key,string? correlationId,CancellationToken ct)
 {
  if(actor!=original)throw new UnauthorizedAccessException("Only the current approver may create a delegation.");
  if(original==delegateUser)throw new InvalidOperationException("Self-delegation is not allowed.");if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Idempotency key is required.");var fingerprint=$"{approvalId}:{original}:{delegateUser}:{from.UtcDateTime:O}:{to?.UtcDateTime:O}:{reason}";
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var prior=await db.Idempotency.SingleOrDefaultAsync(x=>x.Scope=="APPROVAL_DELEGATION"&&x.Key==key,ct);if(prior is not null){if(prior.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key was reused for a different request.");return new(false,true,"Already processed.");}
  var approval=await db.Approvals.SingleOrDefaultAsync(x=>x.Id==approvalId,ct)??throw new KeyNotFoundException("Approval was not found.");if(approval.Status!="PENDING"||approval.ApproverId!=original)throw new InvalidOperationException("Only the current pending approver can delegate.");if(!approval.Active)throw new InvalidOperationException("Only an active approval can be delegated.");if(approval.ApprovalInstanceId is long approvalInstanceId){var instance=await db.ApprovalInstances.SingleAsync(x=>x.Id==approvalInstanceId,ct);if(!db.Entry(instance).Property<bool>("DelegationAllowed").CurrentValue)throw new InvalidOperationException("Delegation is disabled by the selected approval rule version.");}var now=DateTimeOffset.UtcNow;if(to is not null&&to<=from)throw new ArgumentException("Delegation end must be after its start.");
  await EnsureApprovalParticipantAsync(actor,approval.TicketId,ct);await EnsureApprovalParticipantAsync(delegateUser,approval.TicketId,ct);
  var relatedTicket=await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==approval.TicketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
  if(delegateUser==relatedTicket.RequestedBy||delegateUser==relatedTicket.CreatedBy||delegateUser==relatedTicket.CurrentAssignee)
   throw new UnauthorizedAccessException("Requester, creator, or executor cannot approve their own work.");
  if(approval.ApprovalInstanceId is long approvalInstanceIdForSod)
  {
   var instance=await db.ApprovalInstances.AsNoTracking().SingleAsync(x=>x.Id==approvalInstanceIdForSod,ct);
   if(delegateUser==instance.RequesterUserId||delegateUser==instance.CreatorUserId||delegateUser==instance.ExecutorUserId)
    throw new UnauthorizedAccessException("Requester, creator, or executor cannot approve their own work.");
  }
  db.ApprovalDelegations.Add(new ApprovalDelegationRow{ApprovalId=approvalId,OriginalApprover=original,DelegateUser=delegateUser,EffectiveFrom=from,EffectiveTo=to,Reason=reason,CreatedBy=actor,CreatedAt=now});db.Idempotency.Add(new IdempotencyRow{Scope="APPROVAL_DELEGATION",Key=key,Fingerprint=fingerprint,Status="COMPLETED",CreatedAt=now,CompletedAt=now});db.ApprovalHistory.Add(new ApprovalHistoryExecutionRow{ApprovalId=approvalId,Action="DELEGATED",Decision=null,ActorId=actor,Comment=reason,CreatedAt=now,CorrelationId=correlationId,IdempotencyKey=key});db.Events.Add(new EventRow{TicketId=approval.TicketId,Type="APPROVAL_DELEGATED",Category="APPROVAL",ActorId=actor,OldValue=original.ToString(),NewValue=delegateUser.ToString(),Reason=reason,At=now});await AddAudit("APPROVAL",approvalId.ToString(),"APPROVAL_DELEGATED",actor,now,original.ToString(),delegateUser.ToString(),reason,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(true,false,"Delegation created.");
 }
 private async Task AdvanceInstanceAsync(long instanceId,string decision,long actor,DateTimeOffset now,string? correlationId,string key,CancellationToken ct)
 {
  var instance=await db.ApprovalInstances.SingleAsync(x=>x.Id==instanceId,ct);if(instance.Status!="PENDING")throw new InvalidOperationException("Approval instance is already terminal.");
  var rows=await db.Approvals.Where(x=>x.ApprovalInstanceId==instanceId).OrderBy(x=>x.Level).ThenBy(x=>x.Id).ToListAsync(ct);
  if(decision=="REJECTED"||decision=="CANCELLED"||decision=="EXPIRED"){await CompleteInstanceAsync(instance,rows,decision,actor,now,correlationId,key,ct);return;}
  if(decision!="APPROVED")throw new InvalidOperationException("Unsupported approval decision.");
  var mode=instance.Mode.ToUpperInvariant();
  if(mode=="ANY_ONE"){await CompleteInstanceAsync(instance,rows,"APPROVED",actor,now,correlationId,key,ct);return;}
  if(mode=="ALL_REQUIRED"){if(rows.Where(x=>x.Required).All(x=>x.Status=="APPROVED"))await CompleteInstanceAsync(instance,rows,"APPROVED",actor,now,correlationId,key,ct);return;}
  if(mode=="SEQUENTIAL")
  {
   var activeLevel=instance.CurrentLevel;var activeRows=rows.Where(x=>x.Level==activeLevel&&x.Required).ToList();if(activeRows.All(x=>x.Status=="APPROVED"))
   {
    var nextLevel=rows.Where(x=>x.Required&&x.Status=="PENDING"&&x.Level>activeLevel).Select(x=>x.Level).DefaultIfEmpty(0).Min();
    if(nextLevel==0){await CompleteInstanceAsync(instance,rows,"APPROVED",actor,now,correlationId,key,ct);return;}
    instance.CurrentLevel=nextLevel;foreach(var next in rows.Where(x=>x.Level==nextLevel&&x.Status=="PENDING")){next.Active=true;next.RowVersion++;db.ApprovalHistory.Add(new ApprovalHistoryExecutionRow{ApprovalId=next.Id,Action="ACTIVATED",ActorId=actor,CreatedAt=now,CorrelationId=correlationId,IdempotencyKey=key});}
    instance.RowVersion++;db.Events.Add(new EventRow{TicketId=instance.TicketId,Type="APPROVAL_LEVEL_ACTIVATED",Category="APPROVAL",ActorId=actor,NewValue=nextLevel.ToString(),At=now});
   }
   return;
  }
  throw new InvalidOperationException("Unsupported approval mode.");
 }
 private async Task EnsureApprovalParticipantAsync(long userId,long ticketId,CancellationToken ct)
 {
  var ticket=await ticketDb.Tickets.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==ticketId,ct)??throw new KeyNotFoundException("Ticket was not found.");
  var user=await ticketDb.AdministrationUsers.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==userId&&x.Active,ct);
  if(user is null)throw new UnauthorizedAccessException("Approval participant is not an active user.");
  if(ticket.CurrentDepartmentId.HasValue&&user.DepartmentId!=ticket.CurrentDepartmentId||ticket.CurrentTeamId.HasValue&&user.TeamId!=ticket.CurrentTeamId)
   throw new UnauthorizedAccessException("Approval participant is outside the ticket's organizational scope.");
 }
 private async Task CompleteInstanceAsync(ApprovalInstanceExecutionRow instance,List<ApprovalExecutionRow> rows,string status,long actor,DateTimeOffset now,string? correlationId,string key,CancellationToken ct)
 {
  var old=instance.Status;instance.Status=status;instance.CompletedAt=now;instance.RowVersion++;
  foreach(var pending in rows.Where(x=>x.Status=="PENDING")){pending.Status="SKIPPED";pending.Active=false;pending.RowVersion++;db.ApprovalHistory.Add(new ApprovalHistoryExecutionRow{ApprovalId=pending.Id,Action="SKIPPED",Decision=status,ActorId=actor,CreatedAt=now,CorrelationId=correlationId,IdempotencyKey=key});}
  db.Events.Add(new EventRow{TicketId=instance.TicketId,Type=status=="APPROVED"?"APPROVAL_INSTANCE_APPROVED":"APPROVAL_INSTANCE_REJECTED",Category="APPROVAL",ActorId=actor,OldValue=old,NewValue=status,At=now});
    if(instance.WorkflowStepInstanceId is not null){var step=await db.WorkflowSteps.SingleAsync(x=>x.Id==instance.WorkflowStepInstanceId.Value,ct);var stepOld=step.Status;step.Status=status=="APPROVED"?"COMPLETED":"REJECTED";step.CompletedAt=now;step.RowVersion++;db.WorkflowStepHistory.Add(new WorkflowStepHistoryRow{StepInstanceId=step.Id,Action=status=="APPROVED"?"STEP_COMPLETED":"STEP_REJECTED",OldStatus=stepOld,NewStatus=step.Status,ActorId=actor,EventAt=now,CorrelationId=correlationId,IdempotencyKey=key});db.Events.Add(new EventRow{TicketId=instance.TicketId,Type=status=="APPROVED"?"WORKFLOW_STEP_COMPLETED":"WORKFLOW_STEP_REJECTED",Category="WORKFLOW",ActorId=actor,OldValue=stepOld,NewValue=step.Status,At=now});}
  await AddAudit("APPROVAL_INSTANCE",instance.Id.ToString(),"APPROVAL_INSTANCE_COMPLETED",actor,now,old,status,null,ct);
 }
 internal static IReadOnlyCollection<DurableApprovalApprover> NormalizeApprovers(IReadOnlyCollection<DurableApprovalApprover> approvers)=>approvers.Select(x=>new DurableApprovalApprover(x.UserId,Math.Max(1,x.Level),x.Required)).GroupBy(x=>new{x.UserId,x.Level}).Select(g=>new DurableApprovalApprover(g.Key.UserId,g.Key.Level,g.Any(x=>x.Required))).OrderBy(x=>x.Level).ThenBy(x=>x.UserId).ToList();
 private static string NormalizeMode(DurableApprovalMode mode)=>mode switch{DurableApprovalMode.AnyOne=>"ANY_ONE",DurableApprovalMode.AllRequired=>"ALL_REQUIRED",DurableApprovalMode.Sequential=>"SEQUENTIAL",_=>throw new InvalidOperationException("Unsupported approval mode.")};
 private static string CreateInstanceFingerprint(DurableApprovalInstanceRequest request,string mode,IReadOnlyCollection<DurableApprovalApprover> approvers)=>JsonSerializer.Serialize(new{request.TicketId,request.WorkflowStepInstanceId,Mode=mode,Approvers=approvers.Select(x=>new{x.UserId,x.Level,x.Required}),request.RequesterUserId,request.CreatorUserId,request.ExecutorUserId,RejectionBehavior=(request.RejectionBehavior??"REJECT").ToUpperInvariant(),request.ApprovalRuleVersionId,request.ExpiresAfterMinutes,request.DelegationAllowed});
 private async Task AddAudit(string type,string id,string action,long actor,DateTimeOffset at,string? oldValue,string? newValue,string? reason,CancellationToken ct){await auditLocks.AcquireAsync(db,type,id,ct);var prev=await db.Audits.Where(x=>x.EntityType==type&&x.EntityId==id).OrderByDescending(x=>x.Id).Select(x=>x.Hash).FirstOrDefaultAsync(ct);var canonical=JsonSerializer.Serialize(new{entityType=type,entityId=id,action,actorUserId=actor,auditAtUtc=at.UtcDateTime,oldValue,newValue,reason,previousHash=prev});var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();db.Audits.Add(new AuditRow{EntityType=type,EntityId=id,Action=action,ActorId=actor,At=at,OldValue=oldValue,NewValue=newValue,Reason=reason,Hash=hash,PreviousHash=prev});}
}
