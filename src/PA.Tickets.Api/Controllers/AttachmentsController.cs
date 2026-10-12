using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController,Authorize,Route("api/v1/attachments")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class AttachmentsController(IAttachmentService attachments) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(51 * 1024 * 1024)]
    public async Task<ActionResult<object>> Create([FromForm] long ticketId,[FromForm] long? workItemId,[FromForm] string attachmentType,[FromForm] string? classification,[FromForm] string? description,IFormFile file,CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { title = "A non-empty file is required." });
        return Ok(Map(await attachments.CreateAsync(new(ticketId,workItemId,attachmentType,file.FileName,file.ContentType,classification,description,file.OpenReadStream(),Idempotency(),HttpContext.TraceIdentifier),Actor(),ct)));
    }

    [HttpGet("{id:long}")] public async Task<object> Get(long id,CancellationToken ct)=>Map(await attachments.GetAsync(id,Actor(),ct));
    [HttpGet("{id:long}/versions")] public async Task<IReadOnlyCollection<object>> Versions(long id,CancellationToken ct)=>(await attachments.VersionsAsync(id,Actor(),ct)).Select(Map).ToArray();
    [HttpGet("{id:long}/versions/{version:int}/download")]
    public async Task<IActionResult> Download(long id,int version,CancellationToken ct){var result=await attachments.DownloadAsync(id,version,Actor(),ct);Response.ContentType=result.Version.ContentType??"application/octet-stream";Response.ContentLength=result.Version.ContentLength;Response.Headers.ContentDisposition=$"attachment; filename=\"{Uri.EscapeDataString(result.Version.FileName)}\"";return File(result.Content,Response.ContentType,result.Version.FileName);}
    private string Idempotency()=>Request.Headers.TryGetValue("Idempotency-Key",out var key)&&!string.IsNullOrWhiteSpace(key)?key.ToString():throw new BadHttpRequestException("Idempotency-Key is required.");
    private long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException("Authenticated actor is required.");return id;}
    private static object Map(AttachmentResult result)=>new { attachment=Map(result.Attachment), version=Map(result.Version) };
    private static object Map(PA.Tickets.Domain.Tickets.Attachment value)=>new { value.Id,value.TicketId,value.WorkItemId,value.Type,value.FileName,value.ContentType,value.ContentLength,value.Sha256Hash,value.CurrentVersion,value.Active,value.UploadedAt,value.UploadedBy };
    private static object Map(PA.Tickets.Domain.Tickets.AttachmentVersion value)=>new { value.Id,value.AttachmentId,value.VersionNo,value.FileName,value.ContentType,value.ContentLength,value.Sha256Hash,value.Status,value.ChangeReason,value.UploadedAt,value.UploadedBy };
}
