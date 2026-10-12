using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Execution;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/work-items")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class WorkItemsController(IWorkflowExecutionRepository execution) : ControllerBase
{
    [HttpPost("{publicId}/complete")]
    public async Task<ActionResult<DurableMutationResult>> Complete(string publicId, CancellationToken ct)
    {
        var actor = Actor();
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return BadRequest(new { title = "Idempotency-Key is required." });
        return Ok(await execution.CompleteWorkItemAsync(publicId, actor, key.ToString(), HttpContext.TraceIdentifier, ct));
    }

    [HttpPost("{publicId}/assign")]
    public async Task<ActionResult<DurableMutationResult>> Assign(string publicId, WorkItemAssignmentRequest request, CancellationToken ct)
    {
        var actor = Actor();
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return BadRequest(new { title = "Idempotency-Key is required." });
        return Ok(await execution.ReassignWorkItemAsync(publicId, actor, request.DepartmentId, request.TeamId, request.UserId, request.Reason ?? "Reassigned", key.ToString(), HttpContext.TraceIdentifier, ct));
    }

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}

public sealed record WorkItemAssignmentRequest(long? DepartmentId, long? TeamId, long? UserId, string? Reason);


