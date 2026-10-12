using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Execution;

namespace PA.Tickets.Api.Controllers;

[ApiController,Authorize,Route("api/v1/slas")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class SlasController(ISlaExecutionRepository slas):ControllerBase
{
 [HttpPost("{id:long}/operations")]
 public async Task<ActionResult<DurableMutationResult>> Apply(long id,SlaOperationRequest request,CancellationToken ct)
 {
  if(!Request.Headers.TryGetValue("Idempotency-Key",out var key)||string.IsNullOrWhiteSpace(key))return BadRequest(new{title="Idempotency-Key is required."});
  return Ok(await slas.ApplyAsync(id,request.Operation,request.Reason,Actor(),key.ToString(),HttpContext.TraceIdentifier,ct));
 }
 private long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");return id;}
}
public sealed record SlaOperationRequest(DurableSlaOperation Operation,string? Reason);