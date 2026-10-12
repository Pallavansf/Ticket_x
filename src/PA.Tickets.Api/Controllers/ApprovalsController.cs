using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Execution;

namespace PA.Tickets.Api.Controllers;

[ApiController,Authorize,Route("api/v1/approvals")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class ApprovalsController(IApprovalExecutionRepository approvals):ControllerBase
{
 [HttpPost]
 public async Task<ActionResult<DurableApprovalInstanceResult>> Create(ApprovalInstanceRequest request,CancellationToken ct)
 {
  if(!Request.Headers.TryGetValue("Idempotency-Key",out var key)||string.IsNullOrWhiteSpace(key))return BadRequest(new{title="Idempotency-Key is required."});
  var actor=Actor();
  var durableRequest=new DurableApprovalInstanceRequest(request.TicketId,request.WorkflowStepInstanceId,request.Mode,request.Approvers,request.RequesterUserId,actor,request.ExecutorUserId,request.RejectionBehavior);
  return Ok(await approvals.CreateInstanceAsync(durableRequest,actor,key.ToString(),HttpContext.TraceIdentifier,ct));
 }
 [HttpPost("{id:long}/decisions")]
 public async Task<ActionResult<DurableMutationResult>> Decide(long id,ApprovalDecisionRequest request,CancellationToken ct)
 {
  if(!Request.Headers.TryGetValue("Idempotency-Key",out var key)||string.IsNullOrWhiteSpace(key))return BadRequest(new{title="Idempotency-Key is required."});
  return Ok(await approvals.DecideAsync(id,Actor(),request.Decision,request.Comments,key.ToString(),HttpContext.TraceIdentifier,ct));
 }
 [HttpPost("{id:long}/delegations")]
 public async Task<ActionResult<DurableMutationResult>> Delegate(long id,ApprovalDelegationRequest request,CancellationToken ct)
 {
  if(!Request.Headers.TryGetValue("Idempotency-Key",out var key)||string.IsNullOrWhiteSpace(key))return BadRequest(new{title="Idempotency-Key is required."});
  return Ok(await approvals.DelegateAsync(id,request.OriginalApproverUserId,request.DelegateUserId,request.EffectiveFrom,request.EffectiveTo,request.Reason,Actor(),key.ToString(),HttpContext.TraceIdentifier,ct));
 }
 private long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");return id;}
}
public sealed record ApprovalDecisionRequest(DurableApprovalDecision Decision,string? Comments);
public sealed record ApprovalDelegationRequest(long OriginalApproverUserId,long DelegateUserId,DateTimeOffset EffectiveFrom,DateTimeOffset? EffectiveTo,string Reason);
public sealed record ApprovalInstanceRequest(long TicketId,long? WorkflowStepInstanceId,DurableApprovalMode Mode,IReadOnlyCollection<DurableApprovalApprover> Approvers,long RequesterUserId,long? ExecutorUserId,string RejectionBehavior="REJECT");