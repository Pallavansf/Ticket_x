using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
namespace PA.Tickets.Api.Controllers;
[ApiController,Authorize,Route("api/v1/auditor/reports"),Asp.Versioning.ApiVersion("1.0")]
public sealed class AuditorReportsController(IAuditorReportingService reports, PA.Tickets.Application.Foundations.IAuthorizationService authorization):ControllerBase
{
 [HttpGet] public async Task<ActionResult<AuditorCatalogue>> Catalogue(CancellationToken ct)
 {
  await authorization.EnsurePermissionAsync(Actor(), "AUDITOR_VIEW", null, ct);
  return Ok(new AuditorCatalogue(reports.Catalogue()));
 }
 [HttpGet("{reportCode}")] public async Task<ActionResult<AuditorReportDefinition>> Definition(string reportCode,CancellationToken ct){await authorization.EnsurePermissionAsync(Actor(),"AUDITOR_VIEW",null,ct);return Ok(reports.GetDefinition(reportCode));}
 [HttpPost("{reportCode}/query")] public Task<AuditorReportResult> Query(string reportCode,AuditorQueryRequest request,CancellationToken ct)=>reports.QueryAsync(reportCode,request,Actor(),HttpContext.TraceIdentifier,ct);
 [HttpGet("tickets/{ticketId:long}/integrity")] public Task<AuditIntegrityResult> VerifyTicketIntegrity(long ticketId,CancellationToken ct)=>reports.VerifyTicketAuditAsync(ticketId,Actor(),ct);
 [HttpGet("configuration/{entityType}/{entityId}/integrity")] public Task<AuditStreamIntegrityResult> VerifyOperationalMetadataIntegrity(string entityType,string entityId,CancellationToken ct)=>reports.VerifyOperationalMetadataAuditAsync(entityType,entityId,Actor(),ct);
 long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException();return id;}
}
