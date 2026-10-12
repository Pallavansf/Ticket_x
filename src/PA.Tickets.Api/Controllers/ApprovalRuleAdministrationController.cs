using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Approvals;
using PA.Tickets.Domain.Approvals;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/approval-admin")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class ApprovalRuleAdministrationController(IApprovalRuleConfigurationRepository repository, PA.Tickets.Application.Foundations.IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("rules")]
    public Task<IReadOnlyCollection<ApprovalRuleDefinitionView>> List([FromQuery] string? search, CancellationToken ct)
        => repository.ListRulesAsync(Actor(), search, ct);

    [HttpPost("rules")]
    public async Task<ActionResult<long>> Create(ApprovalRuleCreateRequest request, CancellationToken ct)
        => Ok(await repository.CreateRuleAsync(Actor(), request.Code, request.Name, request.Description, IdempotencyKey(), HttpContext.TraceIdentifier, ct));

    [HttpGet("rules/{ruleId:long}")]
    public Task<ApprovalRuleDefinitionView> Get(long ruleId, CancellationToken ct)
        => repository.GetRuleAsync(Actor(), ruleId, ct);

    [HttpPut("rules/{ruleId:long}")]
    public Task<ApprovalRuleDefinitionView> Update(long ruleId, ApprovalRuleUpdateRequest request, CancellationToken ct)
        => repository.UpdateRuleAsync(Actor(), ruleId, request.Code, request.Name, request.Description, request.Reason, HttpContext.TraceIdentifier, ct);

    [HttpPost("rules/{ruleId:long}/active")]
    public Task<ApprovalRuleDefinitionView> SetActive(long ruleId,ApprovalRuleActiveRequest request,CancellationToken ct)
        => repository.SetRuleActiveAsync(Actor(),ruleId,request.Active,request.Reason,HttpContext.TraceIdentifier,ct);

    [HttpGet("rules/{ruleId:long}/versions")]
    public Task<IReadOnlyCollection<ApprovalRuleVersionSummary>> Versions(long ruleId, CancellationToken ct)
        => repository.ListVersionsAsync(Actor(), ruleId, ct);

    [HttpGet("versions/{versionId:long}")]
    public Task<ApprovalRuleVersionView> Version(long versionId, CancellationToken ct)
        => repository.GetVersionAsync(Actor(), versionId, ct);

    [HttpGet("versions/{firstVersionId:long}/compare/{secondVersionId:long}")]
    public Task<ApprovalRuleVersionComparison> Compare(long firstVersionId, long secondVersionId, CancellationToken ct)
        => repository.CompareVersionsAsync(Actor(), firstVersionId, secondVersionId, ct);

    [HttpGet("tickets/{ticketId:long}/runtime-history")]
    public Task<IReadOnlyCollection<ApprovalInstanceHistoryView>> TicketHistory(long ticketId,CancellationToken ct)
        => repository.GetTicketRuntimeHistoryAsync(Actor(),ticketId,ct);

    [HttpPost("rules/{ruleId:long}/versions/validate")]
    public async Task<ActionResult<ApprovalRuleValidationResponse>> Validate(long ruleId, ApprovalRuleVersionRequest request, CancellationToken ct)
    {
        await authorization.EnsurePermissionAsync(Actor(), "APPROVAL_ADMIN", null, ct);
        var result = Build(request, ruleId).Validate();
        return result.IsValid ? Ok(new ApprovalRuleValidationResponse(true, result.Errors)) : UnprocessableEntity(new ApprovalRuleValidationResponse(false, result.Errors));
    }

    [HttpPost("rules/{ruleId:long}/versions/draft")]
    public async Task<ActionResult<long>> SaveDraft(long ruleId, ApprovalRuleVersionRequest request, CancellationToken ct)
    {
        var version = Build(request, ruleId);
        var result = version.Validate();
        if (!result.IsValid) return UnprocessableEntity(new ApprovalRuleValidationResponse(false, result.Errors));
        return Ok(await repository.SaveDraftAsync(ruleId, version, Actor(), IdempotencyKey(), HttpContext.TraceIdentifier, ct));
    }

    [HttpPost("versions/{versionId:long}/publish")]
    public async Task<IActionResult> Publish(long versionId, CancellationToken ct)
    {
        await repository.PublishAsync(versionId, Actor(), IdempotencyKey(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [HttpPost("versions/{versionId:long}/retire")]
    public async Task<IActionResult> Retire(long versionId, ApprovalRuleRetireRequest request, CancellationToken ct)
    {
        await repository.RetireAsync(versionId, Actor(), IdempotencyKey(), request.Reason, HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    private static ApprovalRuleVersion Build(ApprovalRuleVersionRequest request, long ruleId)
        => new(request.Id, ruleId, request.VersionNo, request.Mode, request.RejectionBehavior, request.ExpiresAfterMinutes, request.DelegationAllowed,
            request.RequireRequesterSeparation, request.RequireCreatorSeparation, request.RequireExecutorSeparation, request.EffectiveFrom, request.EffectiveTo,
            request.Steps.Select(x => new ApprovalRuleStepDefinition(x.StepNo, x.ApproverType, x.TargetId, x.Required)));

    private string IdempotencyKey()
        => Request.Headers.TryGetValue("Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key) ? key.ToString() : throw new BadHttpRequestException("Idempotency-Key is required.");

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) && id > 0 ? id : throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
    }
}

public sealed record ApprovalRuleCreateRequest(string Code, string Name, string? Description);
public sealed record ApprovalRuleUpdateRequest(string Code, string Name, string? Description, string? Reason = null);
public sealed record ApprovalRuleActiveRequest(bool Active,string? Reason=null);
public sealed record ApprovalRuleRetireRequest(string? Reason = null);
public sealed record ApprovalRuleVersionRequest(long Id, long RuleId, int VersionNo, ApprovalMode Mode, string RejectionBehavior, int? ExpiresAfterMinutes, bool DelegationAllowed, bool RequireRequesterSeparation, bool RequireCreatorSeparation, bool RequireExecutorSeparation, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, IReadOnlyCollection<ApprovalRuleStepRequest> Steps);
public sealed record ApprovalRuleStepRequest(int StepNo, ApprovalApproverType ApproverType, long TargetId, bool Required = true);
public sealed record ApprovalRuleValidationResponse(bool IsValid, IReadOnlyCollection<string> Errors);
