using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Reporting;
namespace PA.Tickets.Api.Controllers;
[ApiController,Authorize,Route("api/v1/dashboards"),Asp.Versioning.ApiVersion("1.0")]
public sealed class DashboardsController(IDashboardService dashboards):ControllerBase
{
 [HttpGet] public ActionResult<DashboardCatalogue> Catalogue()=>Ok(new DashboardCatalogue(dashboards.Catalogue()));
 [HttpGet("{dashboardCode}")] public ActionResult<DashboardDefinition> Definition(string dashboardCode)=>Ok(dashboards.GetDefinition(dashboardCode));
 [HttpPost("{dashboardCode}/query")] public Task<DashboardResponse> Query(string dashboardCode,DashboardQueryRequest request,CancellationToken ct)=>dashboards.QueryAsync(dashboardCode,request,Actor(),HttpContext.TraceIdentifier,ct);
 long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException();return id;}
}
