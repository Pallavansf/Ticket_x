using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Sla;
using PA.Tickets.Domain.Sla;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/sla-admin")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class SlaAdministrationController(ISlaAdministrationRepository repository) : ControllerBase
{
    [HttpGet("policies")]
    public Task<IReadOnlyCollection<SlaPolicyDefinitionView>> ListPolicies([FromQuery] string? search, CancellationToken ct)
        => repository.ListPoliciesAsync(Actor(), search, ct);

    [HttpPost("policies")]
    public async Task<ActionResult<long>> CreatePolicy(SlaPolicyCreateRequest request, CancellationToken ct)
        => Ok(await repository.CreatePolicyAsync(Actor(), request.Code, request.Name, request.Description, IdempotencyKey(), HttpContext.TraceIdentifier, ct));

    [HttpGet("policies/{policyId:long}")]
    public Task<SlaPolicyDefinitionView> GetPolicy(long policyId, CancellationToken ct)
        => repository.GetPolicyAsync(Actor(), policyId, ct);

    [HttpPut("policies/{policyId:long}")]
    public Task<SlaPolicyDefinitionView> UpdatePolicy(long policyId, SlaPolicyUpdateRequest request, CancellationToken ct)
        => repository.UpdatePolicyAsync(Actor(), policyId, request.ExpectedRowVersion, request.Code, request.Name, request.Description, request.Reason, HttpContext.TraceIdentifier, ct);

    [HttpGet("policies/{policyId:long}/versions")]
    public Task<IReadOnlyCollection<SlaPolicyVersionSummary>> ListVersions(long policyId, CancellationToken ct)
        => repository.ListVersionsAsync(Actor(), policyId, ct);

    [HttpGet("versions/{versionId:long}")]
    public Task<SlaPolicyVersionView> GetVersion(long versionId, CancellationToken ct)
        => repository.GetVersionAsync(Actor(), versionId, ct);

    [HttpGet("versions/{firstVersionId:long}/compare/{secondVersionId:long}")]
    public Task<SlaPolicyVersionComparison> CompareVersions(long firstVersionId, long secondVersionId, CancellationToken ct)
        => repository.CompareVersionsAsync(Actor(), firstVersionId, secondVersionId, ct);

    [HttpPost("policies/{policyId:long}/versions/validate")]
    public async Task<ActionResult<SlaValidationResult>> Validate(long policyId, SlaPolicyVersion version, CancellationToken ct)
    {
        var result = await repository.ValidateAsync(Actor(), policyId, version, ct);
        return result.IsValid ? Ok(result) : UnprocessableEntity(result);
    }

    [HttpPost("policies/{policyId:long}/versions/draft")]
    public async Task<ActionResult<long>> SaveDraft(long policyId, SlaDraftRequest request, CancellationToken ct)
        => Ok(await repository.SaveDraftAsync(policyId, request.Version, Actor(), IdempotencyKey(), request.ExpectedRowVersion, HttpContext.TraceIdentifier, ct));

    [HttpPost("versions/{versionId:long}/publish")]
    public async Task<IActionResult> Publish(long versionId, CancellationToken ct)
    {
        await repository.PublishAsync(versionId, Actor(), IdempotencyKey(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [HttpPost("versions/{versionId:long}/retire")]
    public async Task<IActionResult> Retire(long versionId, SlaRetireRequest request, CancellationToken ct)
    {
        await repository.RetireAsync(versionId, Actor(), IdempotencyKey(), request.Reason, HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [HttpPut("versions/{versionId:long}/escalation")]
    public async Task<ActionResult<long>> UpdateEscalation(long versionId, SlaEscalationRulesRequest request, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(Actor(), versionId, ct);
        if (version.Status != "DRAFT") throw new InvalidOperationException("Escalation configuration can only be changed on a draft SLA version.");
        var updated = version.Policy with { EscalationRules = request.Rules };
        return Ok(await repository.SaveDraftAsync(updated.PolicyId, updated, Actor(), IdempotencyKey(), request.ExpectedRowVersion, HttpContext.TraceIdentifier, ct));
    }

    [HttpGet("calendars")]
    public Task<IReadOnlyCollection<SlaCalendarDefinitionView>> ListCalendars(CancellationToken ct)
        => repository.ListCalendarsAsync(Actor(), ct);

    [HttpGet("calendars/{code}")]
    public Task<SlaCalendarDefinitionView> GetCalendar(string code, CancellationToken ct)
        => repository.GetCalendarAsync(Actor(), code, ct);

    [HttpPost("calendars")]
    public async Task<IActionResult> CreateCalendar(SlaCalendarRequest request, CancellationToken ct)
    {
        await repository.CreateCalendarAsync(Actor(), request.Code, request.Calendar, request.EffectiveFrom, request.EffectiveTo, request.Reason, HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [HttpPut("calendars/{code}")]
    public async Task<IActionResult> UpdateCalendar(string code, SlaCalendarUpdateRequest request, CancellationToken ct)
    {
        await repository.UpdateCalendarAsync(Actor(), code, request.ExpectedRowVersion, request.Calendar, request.EffectiveFrom, request.EffectiveTo, request.Active, request.Reason, HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    private string IdempotencyKey()
        => Request.Headers.TryGetValue("Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key)
            ? key.ToString()
            : throw new BadHttpRequestException("Idempotency-Key is required.");

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) && id > 0 ? id : throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
    }
}

public sealed record SlaPolicyCreateRequest(string Code, string Name, string? Description);
public sealed record SlaPolicyUpdateRequest(int ExpectedRowVersion, string Code, string Name, string? Description, string? Reason = null);
public sealed record SlaDraftRequest(SlaPolicyVersion Version, int? ExpectedRowVersion = null);
public sealed record SlaRetireRequest(string? Reason = null);
public sealed record SlaEscalationRulesRequest(int ExpectedRowVersion, string? Rules);
public sealed record SlaCalendarRequest(string Code, BusinessCalendar Calendar, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string Reason);
public sealed record SlaCalendarUpdateRequest(int ExpectedRowVersion, BusinessCalendar Calendar, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, bool Active, string Reason);
