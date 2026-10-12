using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Reporting;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/report-schedules")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class ReportSchedulesController(IReportScheduleService schedules) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyCollection<ReportSchedule>> List(CancellationToken ct) => schedules.ListAsync(Actor(), ct);
    [HttpPost] public async Task<ActionResult<ReportSchedule>> Create(CreateScheduleRequest request, CancellationToken ct) => Ok(await schedules.CreateAsync(Actor(), request.ReportCode, request.Format, request.Filters ?? new(), request.Recurrence, request.TimeZone ?? "Asia/Kolkata", ct));
    [HttpGet("{scheduleId:long}")] public Task<ReportSchedule> Get(long scheduleId, CancellationToken ct) => schedules.GetAsync(Actor(), scheduleId, ct);
    [HttpPut("{scheduleId:long}")] public Task<ReportSchedule> Update(long scheduleId, UpdateScheduleRequest request, CancellationToken ct) => schedules.UpdateAsync(Actor(), scheduleId, request.Format, request.Filters ?? new(), request.Recurrence, request.TimeZone ?? "Asia/Kolkata", ct);
    [HttpGet("{scheduleId:long}/executions")] public Task<IReadOnlyCollection<ReportScheduleExecution>> Executions(long scheduleId, CancellationToken ct) => schedules.ExecutionsAsync(Actor(), scheduleId, ct);
    [HttpPost("{scheduleId:long}/pause")] public Task<ReportSchedule> Pause(long scheduleId, CancellationToken ct) => schedules.SetStatusAsync(Actor(), scheduleId, ReportScheduleStatus.Paused, ct);
    [HttpPost("{scheduleId:long}/resume")] public Task<ReportSchedule> Resume(long scheduleId, CancellationToken ct) => schedules.SetStatusAsync(Actor(), scheduleId, ReportScheduleStatus.Active, ct);
    [HttpPost("{scheduleId:long}/cancel")] public Task<ReportSchedule> Cancel(long scheduleId, CancellationToken ct) => schedules.SetStatusAsync(Actor(), scheduleId, ReportScheduleStatus.Cancelled, ct);
    private long Actor() { var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"); if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException(); return id; }
}

public sealed record UpdateScheduleRequest(ReportExportFormat Format, ReportScheduleRecurrence Recurrence, ReportQueryRequest? Filters = null, string? TimeZone = null);
public sealed record CreateScheduleRequest(string ReportCode, ReportExportFormat Format, ReportScheduleRecurrence Recurrence, ReportQueryRequest? Filters = null, string? TimeZone = null);