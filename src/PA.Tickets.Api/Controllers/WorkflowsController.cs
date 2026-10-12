using System.Security.Claims;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using PA.Tickets.Application.Foundations;using PA.Tickets.Application.Workflow;using PA.Tickets.Domain.Workflow;
namespace PA.Tickets.Api.Controllers;
[ApiController,Authorize,Route("api/v1/workflows")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class WorkflowsController(IWorkflowDefinitionService service,PA.Tickets.Application.Foundations.IAuthorizationService authorization):ControllerBase
{
 [HttpPost("validate")]
 public async Task<ActionResult<WorkflowValidationResponse>> Validate(WorkflowValidationRequest request,CancellationToken ct){await authorization.EnsurePermissionAsync(Actor(),"WORKFLOW_ADMIN",null,ct);var version=new WorkflowVersion(request.Id,request.WorkflowId,request.VersionNo,request.Steps.Select(x=>new WorkflowStepDefinition(x.Code,x.Name,x.Type,x.JoinRule,x.Required,x.DepartmentId,x.TeamId)),request.Transitions.Select(x=>new WorkflowTransitionDefinition(x.Code,x.FromStep,x.ToStep,x.Condition is null?null:new WorkflowCondition(x.Condition.Field,x.Condition.Operator,x.Condition.Value))),request.EffectiveFrom,request.EffectiveTo);var result=service.Validate(version);return result.IsValid?Ok(new WorkflowValidationResponse(true,result.Errors)):UnprocessableEntity(new WorkflowValidationResponse(false,result.Errors));}
 private long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");return long.TryParse(raw,out var id)&&id>0?id:throw new UnauthorizedAccessException("Authenticated actor mapping is required.");}
}
