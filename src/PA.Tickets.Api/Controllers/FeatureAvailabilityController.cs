using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/features")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class FeatureAvailabilityController(IFeatureSwitchService switches) : ControllerBase
{
    [HttpGet("availability")]
    public Task<IReadOnlyCollection<FeatureAvailabilityView>> Get(CancellationToken ct) =>
        switches.ForActorAsync(Actor(), ct);

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0)
            throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}
