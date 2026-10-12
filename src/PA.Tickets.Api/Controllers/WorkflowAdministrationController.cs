using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Domain.Workflow;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/workflow-admin")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class WorkflowAdministrationController(IWorkflowConfigurationRepository repository, PA.Tickets.Application.Foundations.IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("workflows")]
    public Task<IReadOnlyCollection<WorkflowDefinitionView>> List([FromQuery] string? search, CancellationToken ct)
        => repository.ListWorkflowsAsync(Actor(), search, ct);

    [HttpPost("workflows")]
    public async Task<ActionResult<long>> Create(WorkflowCreateRequest request, CancellationToken ct)
        => Ok(await repository.CreateWorkflowAsync(Actor(), request.Code, request.Name, request.Description, IdempotencyKey(), HttpContext.TraceIdentifier, ct));

    [HttpGet("workflows/{workflowId:long}")]
    public Task<WorkflowDefinitionView> Get(long workflowId, CancellationToken ct)
        => repository.GetWorkflowAsync(Actor(), workflowId, ct);

    [HttpPut("workflows/{workflowId:long}")]
    public Task<WorkflowDefinitionView> Update(long workflowId, WorkflowUpdateRequest request, CancellationToken ct)
        => repository.UpdateWorkflowAsync(Actor(), workflowId, request.Code, request.Name, request.Description, request.Reason, HttpContext.TraceIdentifier, ct);

    [HttpPost("workflows/{workflowId:long}/active")]
    public Task<WorkflowDefinitionView> SetActive(long workflowId,WorkflowActiveRequest request,CancellationToken ct)
        => repository.SetWorkflowActiveAsync(Actor(),workflowId,request.Active,request.Reason,HttpContext.TraceIdentifier,ct);

    [HttpGet("workflows/{workflowId:long}/versions")]
    public Task<IReadOnlyCollection<WorkflowVersionSummary>> Versions(long workflowId, CancellationToken ct)
        => repository.ListVersionsAsync(Actor(), workflowId, ct);

    [HttpGet("versions/{versionId:long}")]
    public Task<WorkflowVersionView> Version(long versionId, CancellationToken ct)
        => repository.GetVersionAsync(Actor(), versionId, ct);

    [HttpGet("versions/{firstVersionId:long}/compare/{secondVersionId:long}")]
    public Task<WorkflowVersionComparison> Compare(long firstVersionId, long secondVersionId, CancellationToken ct)
        => repository.CompareVersionsAsync(Actor(), firstVersionId, secondVersionId, ct);

    [HttpGet("tickets/{ticketId:long}/runtime-history")]
    public Task<IReadOnlyCollection<WorkflowInstanceHistoryView>> TicketHistory(long ticketId,CancellationToken ct)
        => repository.GetTicketRuntimeHistoryAsync(Actor(),ticketId,ct);

    [HttpPost("workflows/{workflowId:long}/versions/validate")]
    public async Task<ActionResult<WorkflowValidationResponse>> Validate(long workflowId, WorkflowValidationRequest request, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(Actor(), "WORKFLOW_ADMIN", null, ct);
        var result = await repository.ValidateAsync(Actor(),workflowId,Build(request, workflowId),ct);
        return result.IsValid ? Ok(new WorkflowValidationResponse(true, result.Errors)) : UnprocessableEntity(new WorkflowValidationResponse(false, result.Errors));
    }

    [HttpPost("workflows/{workflowId:long}/versions/draft")]
    public async Task<ActionResult<long>> SaveDraft(long workflowId, WorkflowValidationRequest request, CancellationToken ct)
    {
        var version = Build(request, workflowId);
        var result = await repository.ValidateAsync(Actor(),workflowId,version,ct);
        if (!result.IsValid) return UnprocessableEntity(new WorkflowValidationResponse(false, result.Errors));
        return Ok(await repository.SaveDraftAsync(workflowId, version, Actor(), IdempotencyKey(), ct, HttpContext.TraceIdentifier));
    }

    [HttpPost("versions/{versionId:long}/publish")]
    public async Task<IActionResult> Publish(long versionId, CancellationToken ct)
    {
        await repository.PublishAsync(versionId, Actor(), IdempotencyKey(), ct, HttpContext.TraceIdentifier);
        return NoContent();
    }

    [HttpPost("versions/{versionId:long}/retire")]
    public async Task<IActionResult> Retire(long versionId, WorkflowRetireRequest request, CancellationToken ct)
    {
        await repository.RetireAsync(versionId, Actor(), IdempotencyKey(), request.Reason, HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    private static WorkflowVersion Build(WorkflowValidationRequest request, long workflowId)
        => new(request.Id, workflowId, request.VersionNo,
            request.Steps.Select(x => new WorkflowStepDefinition(x.Code, x.Name, x.Type, x.JoinRule, x.Required, x.DepartmentId, x.TeamId)),
            request.Transitions.Select(x => new WorkflowTransitionDefinition(x.Code, x.FromStep, x.ToStep, x.Condition is null ? null : new WorkflowCondition(x.Condition.Field, x.Condition.Operator, x.Condition.Value))),
            request.EffectiveFrom, request.EffectiveTo);

    private string IdempotencyKey()
        => Request.Headers.TryGetValue("Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key) ? key.ToString() : throw new BadHttpRequestException("Idempotency-Key is required.");

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) && id > 0 ? id : throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
    }
}

public sealed record WorkflowCreateRequest(string Code, string Name, string? Description);
public sealed record WorkflowUpdateRequest(string Code, string Name, string? Description, string? Reason = null);
public sealed record WorkflowActiveRequest(bool Active,string? Reason=null);
public sealed record WorkflowRetireRequest(string? Reason = null);
public sealed record WorkflowValidationRequest(long Id, long WorkflowId, int VersionNo, IReadOnlyCollection<WorkflowStepRequest> Steps, IReadOnlyCollection<WorkflowTransitionRequest> Transitions, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null);
public sealed record WorkflowStepRequest(string Code, string Name, WorkflowStepType Type, WorkflowJoinRule JoinRule, bool Required = true, long? DepartmentId = null, long? TeamId = null);
public sealed record WorkflowTransitionRequest(string Code, string FromStep, string ToStep, WorkflowConditionRequest? Condition);
public sealed record WorkflowConditionRequest(string Field, string Operator, string Value);
public sealed record WorkflowValidationResponse(bool IsValid, IReadOnlyCollection<string> Errors);
