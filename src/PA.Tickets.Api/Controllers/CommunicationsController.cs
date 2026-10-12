using System.Security.Claims;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using PA.Tickets.Application.Foundations;using PA.Tickets.Domain.Communications;
namespace PA.Tickets.Api.Controllers;
[ApiController,Authorize,Route("api/v1/communications")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class CommunicationsController(ICommunicationService service):ControllerBase
{
 [HttpPost]
 public Task<CommunicationMessage> Queue(QueueCommunicationRequest r,CancellationToken ct)=>service.QueueAsync(new(r.TicketId,r.WorkItemId,r.Channel,r.ProviderCode,r.TemplateCode,r.Recipient,r.Subject,r.Body,r.StructuredPayload,r.ScheduledAt,r.CorrelationId,Idempotency()),Actor(),ct);
 [HttpGet("{publicId}")]
 public Task<CommunicationView> Get(string publicId,CancellationToken ct)=>service.GetAsync(publicId,Actor(),ct);
 [HttpGet("by-ticket/{ticketId:long}")]
 public Task<IReadOnlyCollection<CommunicationView>> ForTicket(long ticketId,CancellationToken ct)=>service.ForTicketAsync(ticketId,Actor(),ct);
 [HttpPost("{publicId}/retry")]
 public Task<bool> Retry(string publicId,CancellationToken ct)=>service.RetryAsync(publicId,Actor(),HttpContext.TraceIdentifier,ct);
 [HttpPost("{publicId}/cancel")]
 public Task<bool> Cancel(string publicId,CancellationToken ct)=>service.CancelAsync(publicId,Actor(),HttpContext.TraceIdentifier,ct);
 private string Idempotency()=>Request.Headers.TryGetValue("Idempotency-Key",out var key)&&!string.IsNullOrWhiteSpace(key)?key.ToString():throw new BadHttpRequestException("Idempotency-Key is required."); private long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");return id;}
}
public sealed record QueueCommunicationRequest(long TicketId,long? WorkItemId,CommunicationChannel Channel,string? ProviderCode,string? TemplateCode,string Recipient,string? Subject,string? Body,string? StructuredPayload,DateTimeOffset? ScheduledAt,string? CorrelationId);

