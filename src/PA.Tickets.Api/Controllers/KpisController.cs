using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Reporting;
namespace PA.Tickets.Api.Controllers;
[ApiController,Authorize,Route("api/v1/kpis"),Asp.Versioning.ApiVersion("1.0")]
public sealed class KpisController(IKpiService service):ControllerBase
{
 [HttpGet] public ActionResult<KpiCatalogue> Catalogue()=>Ok(new KpiCatalogue(service.Catalogue()));
 [HttpGet("{kpiCode}")] public ActionResult<KpiDefinition> Definition(string kpiCode)=>Ok(service.GetDefinition(kpiCode));
 [HttpPost("{kpiCode}/calculate")] public Task<KpiResult> Calculate(string kpiCode,KpiCalculationRequest request,CancellationToken ct)=>service.CalculateAsync(kpiCode,request,Actor(),HttpContext.TraceIdentifier,ct);
 long Actor(){var raw=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");if(!long.TryParse(raw,out var id)||id<=0)throw new UnauthorizedAccessException();return id;}
}
