using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController,Authorize,Route("api/v1/evidence")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class EvidenceController(IAttachmentService attachments) : ControllerBase
{
    [HttpPost]
    public Task<PA.Tickets.Domain.Tickets.Evidence> Create(CreateEvidenceRequest request,CancellationToken ct)=>attachments.CreateEvidenceAsync(new(request.TicketId,request.WorkItemId,request.AttachmentId,request.Code,request.Type,request.Description,request.ControlReference,request.ReviewDueAt,request.RetentionUntil,Idempotency(),HttpContext.TraceIdentifier),Actor(),ct);
    [HttpPost("{id:long}/review")]
    public Task<PA.Tickets.Domain.Tickets.Evidence> Review(long id,ReviewEvidenceRequest request,CancellationToken ct)=>attachments.ReviewEvidenceAsync(id,new(request.Status,request.Comment),Actor(),ct);
    private string Idempotency()=>Request.Headers.TryGetValue("Idempotency-Key",out var key)&&!string.IsNullOrWhiteSpace(key)?key.ToString():throw new BadHttpRequestException("Idempotency-Key is required.");
    private long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException("Authenticated actor is required.");return id;}
}
public sealed record CreateEvidenceRequest(long TicketId,long? WorkItemId,long? AttachmentId,string Code,string Type,string? Description,string? ControlReference,DateTimeOffset? ReviewDueAt,DateTimeOffset? RetentionUntil);
public sealed record ReviewEvidenceRequest(string Status,string? Comment);
