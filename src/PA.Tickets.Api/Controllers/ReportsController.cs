using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Reporting;
namespace PA.Tickets.Api.Controllers;
[ApiController,Authorize,Route("api/v1/reports")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class ReportsController(IReportingQueryService reports):ControllerBase
{
 [HttpGet] public ActionResult<ReportCatalogue> Catalogue()=>Ok(new ReportCatalogue(reports.Catalogue()));
 [HttpGet("{reportCode}")] public ActionResult<ReportDefinition> Definition(string reportCode)=>Ok(reports.GetDefinition(reportCode));
 [HttpPost("{reportCode}/query")] public Task<ReportResult> Query(string reportCode,ReportQueryRequest request,CancellationToken ct)=>reports.QueryAsync(reportCode,request,Actor(),HttpContext.TraceIdentifier,ct);
 private long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");return id;}
}