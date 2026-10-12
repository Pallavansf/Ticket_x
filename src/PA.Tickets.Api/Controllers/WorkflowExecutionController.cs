using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Execution;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/workflow-instances")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class WorkflowExecutionController(IWorkflowExecutionRepository execution,IWorkflowInstanceCreationService instances) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WorkflowInstanceCreationResult>> Create(WorkflowInstanceCreateRequest request, CancellationToken ct)
    {
        var actor = Actor();
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key)) return BadRequest(new { title = "Idempotency-Key is required." });
        return Ok(await instances.CreateAsync(request.TicketId, request.WorkflowId, actor, key.ToString(), HttpContext.TraceIdentifier, ct));
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<DurableMutationResult>> Activate(long id, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key)) return BadRequest(new { title = "Idempotency-Key is required." });
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var actor) || actor <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return Ok(await execution.ActivateInitialStepAsync(id, actor, key.ToString(), HttpContext.TraceIdentifier, ct));
    }

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var actor) || actor <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return actor;
    }
}

public sealed record WorkflowInstanceCreateRequest(long TicketId,long WorkflowId);
