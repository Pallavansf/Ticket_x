using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class ReportExportsController(IReportExportService exports) : ControllerBase
{
    [HttpPost("reports/{reportCode}/exports")]
    public async Task<ActionResult<ExportStatus>> Create(string reportCode, ReportExportRequest request, CancellationToken ct) => Accepted(await exports.CreateAsync(reportCode, request, Actor(), HttpContext.TraceIdentifier, ct));

    [HttpGet("exports/{exportId:long}")]
    public Task<ExportStatus> Get(long exportId, CancellationToken ct) => exports.GetAsync(exportId, Actor(), ct);

    [HttpGet("exports/{exportId:long}/download")]
    public async Task<IActionResult> Download(long exportId, CancellationToken ct)
    {
        var result = await exports.DownloadAsync(exportId, Actor(), ct);
        return File(result.Content, result.Status.ContentType ?? "application/octet-stream", result.FileName, enableRangeProcessing: false);
    }

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}