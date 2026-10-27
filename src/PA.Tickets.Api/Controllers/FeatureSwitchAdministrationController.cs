using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/admin/feature-switches")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class FeatureSwitchAdministrationController(IFeatureSwitchService switches) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<FeatureSwitchView>> List([FromQuery] long? departmentId, CancellationToken ct) =>
        switches.ListAsync(Actor(), departmentId, ct);

    [HttpPut("{code}")]
    public async Task<ActionResult<FeatureSwitchView>> Set(string code, SetFeatureSwitchRequest request, CancellationToken ct)
    {
        if (request.Enabled is null)
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Enabled must be true or false." });
        return Ok(await switches.SetAsync(Actor(), code, request.DepartmentId, request.Enabled.Value,
            request.ExpectedRowVersion, request.Reason, ct));
    }

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0)
            throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}

public sealed record SetFeatureSwitchRequest(bool? Enabled, long? DepartmentId = null,
    int? ExpectedRowVersion = null, string? Reason = null);
