using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/problems")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class ProblemsController(IProblemService service) : ControllerBase
{
    [HttpPost]
    public Task<ProblemView> Create(CreateProblemRequest request, CancellationToken ct)
        => service.CreateAsync(new(request.Subject, request.Description, request.ProblemStatement, Actor(), request.ServiceId, request.CategoryId, request.RequestTypeId, request.PriorityId, HttpContext.TraceIdentifier, request.IdempotencyKey, request.SubCategoryId, true), Actor(), ct);

    [HttpGet("{number}")]
    public Task<ProblemView> Get(string number, CancellationToken ct) => service.GetByNumberAsync(number, Actor(), ct);

    [HttpPost("{number}/assign")]
    public async Task<ProblemView> Assign(string number, AssignmentRequest request, CancellationToken ct)
        => await service.AssignAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.AssigneeUserId, request.Reason, Actor(), ct);

    [HttpPost("{number}/start-investigation")]
    public async Task<ProblemView> StartInvestigation(string number, CancellationToken ct)
        => await service.StartInvestigationAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, Actor(), ct);

    [HttpPost("{number}/root-cause")]
    public async Task<ProblemView> RootCause(string number, ProblemTextRequest request, CancellationToken ct)
        => await service.UpdateRootCauseAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Value, Actor(), ct);

    [HttpPost("{number}/workaround")]
    public async Task<ProblemView> Workaround(string number, ProblemTextRequest request, CancellationToken ct)
        => await service.UpdateWorkaroundAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Value, Actor(), ct);

    [HttpPost("{number}/declare-known-error")]
    public async Task<ProblemView> DeclareKnownError(string number, CancellationToken ct)
        => await service.DeclareKnownErrorAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, Actor(), ct);

    [HttpPost("{number}/permanent-fix")]
    public async Task<ProblemView> PermanentFix(string number, ProblemTextRequest request, CancellationToken ct)
        => await service.UpdatePermanentFixAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Value, Actor(), ct);

    [HttpPost("{number}/resolve")]
    public async Task<ProblemView> Resolve(string number, ProblemTextRequest request, CancellationToken ct)
        => await service.ResolveAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Value, Actor(), ct);

    [HttpPost("{number}/close")]
    public async Task<ProblemView> Close(string number, CancellationToken ct)
        => await service.CloseAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, Actor(), ct);

    [HttpPost("{number}/reopen")]
    public async Task<ProblemView> Reopen(string number, ReasonRequest request, CancellationToken ct)
        => await service.ReopenAsync((await service.GetByNumberAsync(number, Actor(), ct)).TicketId, request.Reason, Actor(), ct);

    [HttpPost("{number}/incidents/{incidentNumber}")]
    public Task<TicketRelationship> RelateIncident(string number, string incidentNumber, ProblemRelationshipRequest request, CancellationToken ct)
        => service.RelateIncidentByNumberAsync(number, incidentNumber, request.Description, Actor(), ct);

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}

public sealed record CreateProblemRequest(string Subject, string? Description, string ProblemStatement, long? ServiceId, long? CategoryId, long? RequestTypeId, long? PriorityId, string? IdempotencyKey, long? SubCategoryId = null);
public sealed record ProblemTextRequest(string Value);
public sealed record ProblemRelationshipRequest(string? Description);
