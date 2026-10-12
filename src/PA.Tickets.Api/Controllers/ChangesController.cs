using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/changes")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class ChangesController(IChangeService service) : ControllerBase
{
    [HttpPost]
    public Task<ChangeView> Create(CreateChangeRequest request, CancellationToken ct)
        => service.CreateAsync(new(request.Subject, request.Description, request.ChangeType, request.Impact, request.Risk, request.Reason, request.BusinessImpact, request.ImplementationPlan, request.ValidationPlan, request.RollbackPlan, request.SuccessCriteria, request.RequesterUserId, request.ServiceId, request.CategoryId, request.RequestTypeId, request.PriorityId, HttpContext.TraceIdentifier, request.IdempotencyKey, request.SubCategoryId, true), Actor(), ct);

    [HttpGet("{number}")]
    public Task<ChangeView> Get(string number, CancellationToken ct) => service.GetByNumberAsync(number, Actor(), ct);

    [HttpPost("{number}/assess")]
    public async Task<ChangeView> Assess(string number, ChangeAssessmentRequest request, CancellationToken ct)
        => await service.AssessAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Impact, request.Risk, Actor(), request.Reason, ct);

    [HttpPost("{number}/schedule")]
    public async Task<ChangeView> Schedule(string number, ChangeScheduleRequest request, CancellationToken ct)
        => await service.ScheduleAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, new(request.PlannedStartAt, request.PlannedEndAt, request.Reason), Actor(), ct);

    [HttpPost("{number}/start-implementation")]
    public async Task<ChangeView> StartImplementation(string number, CancellationToken ct)
        => await service.StartImplementationAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, Actor(), ct);

    [HttpPost("{number}/complete-implementation")]
    public async Task<ChangeView> CompleteImplementation(string number, ChangeResultRequest request, CancellationToken ct)
        => await service.CompleteImplementationAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, new(request.Result, request.Reason), Actor(), ct);

    [HttpPost("{number}/fail-implementation")]
    public async Task<ChangeView> FailImplementation(string number, ChangeFailureRequest request, CancellationToken ct)
        => await service.FailImplementationAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, new(request.FailureReason), Actor(), ct);

    [HttpPost("{number}/rollback")]
    public async Task<ChangeView> Rollback(string number, ChangeResultRequest request, CancellationToken ct)
        => await service.RollbackAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, new(request.Result), Actor(), ct);

    [HttpPost("{number}/validate")]
    public async Task<ChangeView> Validate(string number, ChangeValidationRequest request, CancellationToken ct)
        => await service.ValidateAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, new(request.Result, request.Successful), Actor(), ct);

    [HttpPost("{number}/resolve")]
    public async Task<ChangeView> Resolve(string number, ChangeResultRequest request, CancellationToken ct)
        => await service.ResolveAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Result, Actor(), ct);

    [HttpPost("{number}/close")]
    public async Task<ChangeView> Close(string number, CancellationToken ct)
        => await service.CloseAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, Actor(), ct);

    [HttpPost("{number}/reopen")]
    public async Task<ChangeView> Reopen(string number, ChangeReasonRequest request, CancellationToken ct)
        => await service.ReopenAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Reason, Actor(), ct);

    [HttpPost("{number}/incidents/{incidentNumber}")]
    public async Task<ChangeRelationshipResult> RelateIncident(string number, string incidentNumber, ChangeRelationshipRequest request, CancellationToken ct)
        => await service.RelateIncidentByNumberAsync(number, incidentNumber, request.Description, Actor(), ct);

    [HttpPost("{number}/problems/{problemNumber}")]
    public async Task<ChangeRelationshipResult> RelateProblem(string number, string problemNumber, ChangeRelationshipRequest request, CancellationToken ct)
        => await service.RelateProblemByNumberAsync(number, problemNumber, request.Description, Actor(), ct);

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}

public sealed record CreateChangeRequest(string Subject, string? Description, ChangeType ChangeType, ChangeImpact Impact, ChangeRisk Risk, string Reason, string? BusinessImpact, string ImplementationPlan, string ValidationPlan, string? RollbackPlan, string? SuccessCriteria, long RequesterUserId, long? ServiceId, long? CategoryId, long? RequestTypeId, long? PriorityId, string? IdempotencyKey, long? SubCategoryId = null);
public sealed record ChangeAssessmentRequest(ChangeImpact Impact, ChangeRisk Risk, string? Reason);
public sealed record ChangeScheduleRequest(DateTimeOffset PlannedStartAt, DateTimeOffset PlannedEndAt, string? Reason);
public sealed record ChangeResultRequest(string Result, string? Reason);
public sealed record ChangeFailureRequest(string FailureReason);
public sealed record ChangeValidationRequest(string Result, bool Successful = true);
public sealed record ChangeRelationshipRequest(string? Description);
public sealed record ChangeReasonRequest(string Reason);
