using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Sla;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Application.Approvals;
using PA.Tickets.Application.Execution;
using PA.Tickets.Domain.Sla;
namespace PA.Tickets.Application.Tickets;
public sealed class TicketLifecycleService(ITicketRepository repository,ITicketIdentityGenerator identities,ITicketAccessAuthorizer authorizer,ITicketRelationshipPolicy relationPolicy,IWaitingReasonCatalog waitingReasons,ITicketCatalogueRuntime catalogue,ITicketCreationIdempotencyStore idempotency,IWorkflowVersionSelector workflowVersions,IWorkflowInstanceCreationService workflowInstances,IWorkflowExecutionRepository workflowExecution,ISlaPolicyResolver slaPolicies,ISlaOrchestrationService slaOrchestration,TimeProvider clock,IApprovalRuleVersionResolver approvalRuleVersions,IApprovalExecutionRepository approvalExecution,ITicketStatusConfigurationRepository statusConfiguration = null!,ITicketPriorityConfigurationRepository priorityConfiguration = null!):ITicketLifecycleService
{
 public async Task<TicketView> CreateAsync(CreateTicketCommand c,CancellationToken ct)
 {
  var values=c.FieldValues??Array.Empty<PA.Tickets.Application.Foundations.TicketFieldValue>();
  var fingerprint=Fingerprint(c,values);
  var idempotencyKey=string.IsNullOrWhiteSpace(c.IdempotencyKey)?null:c.IdempotencyKey.Trim();
  long? resumeTicketId=null;
  if(idempotencyKey is not null)
  {
   var claim=await idempotency.ClaimAsync(c.CreatorUserId,idempotencyKey,fingerprint,ct);
   if(claim.Replay)
   {
	var replay=await repository.GetAsync(claim.TicketId!.Value,ct)??throw new InvalidOperationException("The idempotent ticket result is no longer available.");
	await authorizer.EnsureCanAccessAsync(c.CreatorUserId,replay,ct);return Map(replay);
   }
   if(claim.Resume)resumeTicketId=claim.TicketId??throw new InvalidOperationException("The incomplete ticket creation has no persisted ticket result.");
  }
  try
  {
   var existingTicket=resumeTicketId is long existingId?await repository.GetAsync(existingId,ct)??throw new InvalidOperationException("The incomplete ticket creation result is no longer available."):null;
   var at=existingTicket?.CreatedAt??clock.GetUtcNow();
   if(!await statusConfiguration.IsStatusActiveAsync(c.CreatorUserId,TicketStatusCodes.ToConfigurationCode(TicketStatus.New),ct))
    throw new InvalidOperationException("The initial ticket status is not configured as active.");
   var selected=await catalogue.ResolveForCreationAsync(c.CreatorUserId,c.RequestTypeId,at,values,ct);
   ValidateRequestedPath(c,selected);
   var type=selected is null?c.Type:(TicketType)selected.TicketType;
   EffectiveWorkflowVersion? workflowVersion=null;SlaPolicyVersion? slaPolicy=null;SelectedApprovalRuleVersion? approvalRuleVersion=null;
   if(selected?.WorkflowId is long workflowId)workflowVersion=selected.WorkflowVersionId is long selectedWorkflowVersionId
    ?await workflowVersions.ResolveVersionAsync(workflowId,selectedWorkflowVersionId,at,ct)
    :await workflowVersions.ResolveAsync(workflowId,at,ct);
   if(selected?.ApprovalRuleVersionId is long approvalRuleVersionId)approvalRuleVersion=await approvalRuleVersions.ResolveAsync(approvalRuleVersionId,at,ct);
   if(selected?.SlaPolicyId is long)
   {
	slaPolicy=await slaPolicies.ResolveAsync(new SlaEvaluationContext(selected.ServiceId,selected.CategoryId,selected.RequestTypeId,(int)type,selected.DefaultPriorityId,selected.DefaultDepartmentId,selected.DefaultTeamId,null,selected.SubCategoryId),at,ct)??throw new InvalidOperationException("No effective SLA policy matched the Request Type configuration.");
	if(slaPolicy.PolicyId!=selected.SlaPolicyId||selected.SlaPolicyVersion.HasValue&&slaPolicy.VersionNo!=selected.SlaPolicyVersion.Value)throw new InvalidOperationException("The selected SLA policy version does not match the Request Type configuration.");
   }
   var priority=await priorityConfiguration.ResolvePriorityForSelectionAsync(c.CreatorUserId,c.PriorityId??selected?.DefaultPriorityId,ct);
   var t=existingTicket;
   if(t is null)
   {
    var i=await identities.NextAsync(type,ct);t=Ticket.Create(i.Id,i.Number,type,c.Subject,c.Description,c.RequesterUserId,c.CreatorUserId,at);
    if(selected is not null)t.ApplyCatalogue(selected.RequestTypeId,selected.VersionNo,selected.TicketType,selected.ServiceId,selected.CategoryId,priority.Id,selected.DefaultDepartmentId,selected.DefaultTeamId,selected.SubCategoryId);
    else t.SetPriority(priority.Id);
    await repository.AddAsync(t,values,c.CreatorUserId,idempotencyKey,ct);
   }
   else EnsureResumeMatches(t,c,selected,type,priority.Id);
   if(idempotencyKey is not null)await idempotency.SetTicketAsync(c.CreatorUserId,idempotencyKey,t.Id,ct);
   if(selected?.WorkflowId is long configuredWorkflowId&&workflowVersion is not null)
   {
	var workflowKey=$"CATALOGUE-WORKFLOW:{t.Id}:{selected.VersionNo}";
	var instance=await workflowInstances.CreateAsync(t.Id,configuredWorkflowId,c.CreatorUserId,workflowKey,$"CATALOGUE:{selected.RequestTypeId}:{selected.VersionNo}",ct,workflowVersion.VersionId);
	await workflowExecution.ActivateInitialStepAsync(instance.InstanceId,c.CreatorUserId,$"{workflowKey}:ACTIVATE",$"CATALOGUE:{selected.RequestTypeId}:{selected.VersionNo}",ct);
   }
   if(selected is not null&&approvalRuleVersion is not null)
   {
    var mode=approvalRuleVersion.Mode switch{PA.Tickets.Domain.Approvals.ApprovalMode.AnyOne=>DurableApprovalMode.AnyOne,PA.Tickets.Domain.Approvals.ApprovalMode.AllRequired=>DurableApprovalMode.AllRequired,PA.Tickets.Domain.Approvals.ApprovalMode.Sequential=>DurableApprovalMode.Sequential,_=>throw new InvalidOperationException("Unsupported approval rule mode.")};
    var approvers=approvalRuleVersion.Approvers.Select(x=>new DurableApprovalApprover(x.UserId,x.Level,x.Required)).ToArray();
    var request=new DurableApprovalInstanceRequest(t.Id,null,mode,approvers,c.RequesterUserId,c.CreatorUserId,null,approvalRuleVersion.RejectionBehavior,approvalRuleVersion.RuleVersionId,approvalRuleVersion.ExpiresAfterMinutes,approvalRuleVersion.DelegationAllowed);
    await approvalExecution.CreateInstanceAsync(request,c.CreatorUserId,$"TICKET:{t.Id}:APPROVAL:{selected.VersionNo}",$"CATALOGUE:{selected.RequestTypeId}:{selected.VersionNo}",ct);
   }
   if(slaPolicy is not null)await slaOrchestration.StartAsync(t.Id+900000000,t.Id,new SlaEvaluationContext(selected!.ServiceId,selected.CategoryId,selected.RequestTypeId,(int)type,priority.Id,selected.DefaultDepartmentId,selected.DefaultTeamId,null,selected.SubCategoryId),c.CreatorUserId,at,ct);
   if(idempotencyKey is not null)await idempotency.CompleteAsync(c.CreatorUserId,idempotencyKey,ct);
   return Map(t);
  }
  catch{if(idempotencyKey is not null)await idempotency.FailAsync(c.CreatorUserId,idempotencyKey,ct);throw;}
 }
 private static void EnsureResumeMatches(Ticket existing,CreateTicketCommand command,CatalogueTicketConfiguration? selected,TicketType type,long priorityId)
 {
  if(existing.CreatedBy!=command.CreatorUserId||existing.RequestedBy!=command.RequesterUserId||existing.Type!=type||existing.PriorityId!=priorityId)
   throw new InvalidOperationException("The original ticket configuration changed; downstream creation cannot be safely resumed.");
  if(selected is null)
  {
   if(existing.RequestTypeId is not null)throw new InvalidOperationException("The original ticket catalogue configuration is no longer available for safe recovery.");
   return;
  }
  if(existing.RequestTypeId!=selected.RequestTypeId||existing.RequestTypeVersion!=selected.VersionNo||existing.ServiceId!=selected.ServiceId||existing.CategoryId!=selected.CategoryId||existing.SubCategoryId!=selected.SubCategoryId||existing.CurrentDepartmentId!=selected.DefaultDepartmentId||existing.CurrentTeamId!=selected.DefaultTeamId)
   throw new InvalidOperationException("The original ticket catalogue configuration changed; downstream creation cannot be safely resumed.");
 }
 private static string Fingerprint(CreateTicketCommand c,IReadOnlyCollection<PA.Tickets.Application.Foundations.TicketFieldValue> values){var fields=values.OrderBy(x=>x.FieldId).Select(x=>new{FieldId=x.FieldId,x.StringValue,x.NumberValue,x.DateValue,x.TextValue});var canonical=c.ServiceId is null&&c.CategoryId is null&&c.SubCategoryId is null?JsonSerializer.Serialize(new{c.Type,c.Subject,c.Description,c.RequesterUserId,c.CreatorUserId,c.RequestTypeId,c.PriorityId,FieldValues=fields}):JsonSerializer.Serialize(new{c.Type,c.Subject,c.Description,c.RequesterUserId,c.CreatorUserId,c.RequestTypeId,c.ServiceId,c.CategoryId,c.SubCategoryId,c.PriorityId,FieldValues=fields});return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();}
 private static void ValidateRequestedPath(CreateTicketCommand command,CatalogueTicketConfiguration? selected){var pathSupplied=command.ServiceId.HasValue||command.CategoryId.HasValue||command.SubCategoryId.HasValue;if(selected is null){if(pathSupplied)throw new ArgumentException("A Request Type is required when a catalogue path is supplied.");return;}if(pathSupplied&&(!command.ServiceId.HasValue||!command.CategoryId.HasValue))throw new ArgumentException("Service and Category are required when a catalogue path is supplied.");if(command.ServiceId.HasValue&&command.ServiceId!=selected.ServiceId||command.CategoryId.HasValue&&command.CategoryId!=selected.CategoryId||command.SubCategoryId.HasValue&&command.SubCategoryId!=selected.SubCategoryId)throw new InvalidOperationException("The supplied Service, Category, and SubCategory do not match the selected Request Type path.");}
 public async Task<TicketView> GetAsync(long id,long actor,CancellationToken ct){var t=await repository.GetAsync(id,ct)??throw new KeyNotFoundException($"Ticket {id} was not found.");await authorizer.EnsureCanAccessAsync(actor,t,ct);var history=await repository.GetHistoryAsync(id,ct);return Map(t,history);} public async Task<TicketSearchPage> SearchAsync(TicketSearchCriteria criteria,long actor,CancellationToken ct){var scoped=await authorizer.ScopeSearchAsync(actor,criteria,ct);await authorizer.EnsureCanSearchAsync(actor,ct);return await repository.SearchAsync(scoped,ct);}
 public async Task<TicketView> ClassifyAsync(long id,long s,long cat,long req,long pri,long actor,CancellationToken ct){var priority=await priorityConfiguration.ResolvePriorityForSelectionAsync(actor,pri,ct);return await Mutate(id,actor,(t,n)=>t.Classify(s,cat,req,priority.Id,actor,n),ct,expectedStatus:TicketStatus.Classified);}
 public async Task<TicketView> RouteAsync(long id,long? dept,long? team,long actor,CancellationToken ct){var ticket=await repository.GetAsync(id,ct)??throw new KeyNotFoundException($"Ticket {id} was not found.");await authorizer.EnsureCanAssignAsync(actor,Resource(ticket),dept,team,null,ct);var wi=await identities.NextWorkItemIdAsync(ct);return await Mutate(id,actor,(t,n)=>t.Route(wi,$"{t.Number}-WI-{t.WorkItems.Count+1:D3}",dept,team,actor,n),ct,expectedStatus:TicketStatus.Routed);}
 public Task<TicketView> RequestApprovalAsync(long id,long approver,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.RequestApproval(approver,actor,n),ct,expectedStatus:TicketStatus.PendingApproval);
 public Task<TicketView> ApproveAsync(long id,long approver,CancellationToken ct)=>Mutate(id,approver,(t,n)=>t.Approve(approver,n),ct,expectedStatus:TicketStatus.Routed);
 public async Task<TicketView> ExecuteAsync(long id,long wi,long executor,long actor,CancellationToken ct)
 {
  var ticket=await repository.GetAsync(id,ct)??throw new KeyNotFoundException($"Ticket {id} was not found.");
  var item=Find(ticket,wi);
  await authorizer.EnsureCanAssignAsync(actor,Resource(ticket),item.ToDepartmentId,item.ToTeamId,executor,ct);
  return await Mutate(id,actor,(t,n)=>t.StartExecution(Find(t,wi),executor,actor,n),ct,expectedStatus:TicketStatus.InProgress);
 }
 public async Task<TicketView> ReassignAsync(long id,long wi,long assignee,long actor,string? reason,CancellationToken ct){var ticket=await repository.GetAsync(id,ct)??throw new KeyNotFoundException($"Ticket {id} was not found.");var item=Find(ticket,wi);await authorizer.EnsureCanAssignAsync(actor,Resource(ticket),item.ToDepartmentId,item.ToTeamId,assignee,ct);return await Mutate(id,actor,(t,n)=>t.Assign(Find(t,wi),assignee,actor,n,reason),ct);}
 public async Task<TicketView> WaitAsync(long id,long wi,WaitingRequest request,long actor,CancellationToken ct){var at=clock.GetUtcNow();var definition=await waitingReasons.ResolveAsync(request.Reason,at,ct);if(!definition.AllowedFromStatuses.Contains(TicketStatus.InProgress))throw new InvalidOperationException("Waiting reason is not allowed from the current ticket status.");if(definition.RequireComment&&string.IsNullOrWhiteSpace(request.Comment))throw new ArgumentException("A comment is required for this waiting reason.");if(definition.RequireEvidence&&string.IsNullOrWhiteSpace(request.EvidenceReference))throw new ArgumentException("Evidence is required for this waiting reason.");if(definition.RequireExpectedDate&&request.ExpectedDate is null)throw new ArgumentException("An expected date is required for this waiting reason.");return await Mutate(id,actor,(t,n)=>t.Wait(Find(t,wi),definition.Code,request.Comment,request.EvidenceReference,request.ExpectedDate,actor,n),ct,expectedStatus:TicketStatus.Waiting);}
 public Task<TicketView> ResumeAsync(long id,long wi,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.Resume(Find(t,wi),actor,n),ct,expectedStatus:TicketStatus.InProgress);
 public Task<TicketView> AddEvidenceAsync(long id,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.AddEvidence(actor,n),ct);
 public Task<TicketView> CompleteWorkAsync(long id,long wi,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.CompleteWork(Find(t,wi),actor,n),ct);
 public Task<TicketView> ResolveAsync(long id,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.Resolve(actor,n),ct,expectedStatus:TicketStatus.Resolved);
 public Task<TicketView> ReopenAsync(long id,string reason,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.Reopen(reason,actor,n),ct,expectedStatus:TicketStatus.Reopened);
 public Task<TicketView> CancelAsync(long id,string reason,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.Cancel(reason,actor,n),ct,expectedStatus:TicketStatus.Cancelled);
 public Task<TicketView> CloseAsync(long id,long actor,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.Close(actor,n),ct,expectedStatus:TicketStatus.Closed);
 public Task<TicketView> TransitionSpecializedStatusAsync(long id,TicketStatus status,long actor,string? reason,CancellationToken ct)=>Mutate(id,actor,(t,n)=>t.TransitionSpecializedStatus(status,actor,n,reason),ct,t=>t.ValidateSpecializedStatusTransition(status),status);
 public async Task EnsureSpecializedStatusTransitionAllowedAsync(long id,TicketStatus status,long actor,CancellationToken ct){var t=await repository.GetAsync(id,ct)??throw new KeyNotFoundException($"Ticket {id} was not found.");await authorizer.EnsureCanMutateAsync(actor,t,ct);t.ValidateSpecializedStatusTransition(status);if(t.Status!=status)await TicketStatusTransitionPolicy.EnsureAllowedAsync(statusConfiguration,actor,t.Status,status,ct);}
 public async Task<TicketRelationship> AddRelationshipAsync(long id,long relatedId,string type,string? description,long actor,CancellationToken ct){var t=await repository.GetAsync(id,ct)??throw new KeyNotFoundException();var target=await repository.GetAsync(relatedId,ct)??throw new KeyNotFoundException();await authorizer.EnsureCanMutateAsync(actor,t,ct);if(id==relatedId||!relationPolicy.IsAllowed(t.Type,target.Type,type))throw new InvalidOperationException("The configured relationship policy does not allow this relationship.");var r=new TicketRelationship(id,relatedId,type,description,actor,clock.GetUtcNow());await repository.AddRelationshipAsync(r,ct);return r;}
 private async Task<TicketView> Mutate(long id,long actor,Action<Ticket,DateTimeOffset> action,CancellationToken ct,Action<Ticket>? validateBeforeMutation=null,TicketStatus? expectedStatus=null){var t=await repository.GetAsync(id,ct)??throw new KeyNotFoundException($"Ticket {id} was not found.");await authorizer.EnsureCanMutateAsync(actor,t,ct);validateBeforeMutation?.Invoke(t);var before=t.Status;if(expectedStatus is TicketStatus target&&before!=target)await TicketStatusTransitionPolicy.EnsureAllowedAsync(statusConfiguration,actor,before,target,ct);action(t,clock.GetUtcNow());if(expectedStatus is null&&before!=t.Status)await TicketStatusTransitionPolicy.EnsureAllowedAsync(statusConfiguration,actor,before,t.Status,ct);await repository.SaveAsync(t,ct);return Map(t);}
 private static WorkItem Find(Ticket t,long id)=>t.WorkItems.SingleOrDefault(x=>x.Id==id)??throw new KeyNotFoundException($"Work item {id} was not found.");
 private static AuthorizationResource Resource(Ticket ticket)=>new(ticket.Id,ticket.CurrentDepartmentId,ticket.CurrentTeamId,ticket.ClientId,ticket.CurrentAssignee);

 public static TicketView Map(Ticket t)=>Map(t,Array.Empty<TicketHistoryView>());
 private static TicketView Map(Ticket t,IReadOnlyCollection<TicketHistoryView> history)=>new(t.Id,t.Number,t.Type,t.Status,t.Subject,t.PriorityId,t.CurrentDepartmentId,t.CurrentTeamId,t.CurrentAssignee,t.WorkItems.Select(w=>new WorkItemView(w.Id,w.Number,w.PublicId,w.Status,w.AssignedToUserId,w.WaitingFor,w.WaitingComment,w.WaitingEvidenceReference,w.WaitingExpectedDate,w.WaitingMinutes)).ToArray(),t.ServiceId,t.CategoryId,t.SubCategoryId,t.RequestTypeId,t.RequestTypeVersion,history);
}
