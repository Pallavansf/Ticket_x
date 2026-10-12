using Microsoft.AspNetCore.Mvc;

namespace PA.Tickets.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { service = "PA Tickets API", status = "ready" });
}
