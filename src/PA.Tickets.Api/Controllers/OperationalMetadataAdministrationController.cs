using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/admin/operational-metadata")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class OperationalMetadataAdministrationController(
    ITicketStatusConfigurationRepository statuses,
    ITicketPriorityConfigurationRepository priorities) : ControllerBase
{
    [HttpGet("statuses")]
    public Task<IReadOnlyCollection<StatusDefinitionView>> ListStatuses([FromQuery] string? search, CancellationToken ct) =>
        statuses.ListStatusesAsync(Actor(), search, ct);

    [HttpGet("statuses/{id:long}")]
    public Task<StatusDefinitionView> GetStatus(long id, CancellationToken ct) =>
        statuses.GetStatusAsync(Actor(), id, ct);

    [HttpPost("statuses")]
    public Task<StatusDefinitionView> CreateStatus(CreateStatusRequest request, CancellationToken ct) =>
        statuses.CreateStatusAsync(Actor(), request.Code, request.Name, request.Description, request.IsInitial,
            request.IsOpen, request.IsClosed, request.IsCancelled, request.DisplayOrder, request.Reason, ct);

    [HttpPut("statuses/{id:long}")]
    public Task<StatusDefinitionView> UpdateStatus(long id, UpdateStatusRequest request, CancellationToken ct) =>
        statuses.UpdateStatusAsync(Actor(), id, request.Code, request.Name, request.Description, request.IsInitial,
            request.IsOpen, request.IsClosed, request.IsCancelled, request.DisplayOrder, request.Reason, ct,
            request.ExpectedRowVersion);

    [HttpPost("statuses/{id:long}/active")]
    public Task<StatusDefinitionView> SetStatusActive(long id, MetadataActiveRequest request, CancellationToken ct) =>
        statuses.SetStatusActiveAsync(Actor(), id, request.Active, request.Reason, ct);

    [HttpGet("statuses/audit/{id:long}")]
    public Task<IReadOnlyCollection<StatusConfigurationAudit>> GetStatusAudit(long id, CancellationToken ct) =>
        statuses.GetStatusAuditAsync(Actor(), id, ct);

    [HttpGet("transitions")]
    public Task<IReadOnlyCollection<StatusTransitionDefinition>> ListTransitions(CancellationToken ct) =>
        statuses.ListTransitionsAsync(Actor(), ct);

    [HttpPut("transitions")]
    public Task<StatusTransitionDefinition> SetTransition(SetTransitionRequest request, CancellationToken ct) =>
        statuses.SetTransitionAsync(Actor(), request.FromCode, request.ToCode, request.Condition, request.Allowed,
            request.Reason, ct, request.ExpectedRowVersion);

    [HttpGet("priorities")]
    public Task<IReadOnlyCollection<PriorityDefinitionView>> ListPriorities([FromQuery] string? search, CancellationToken ct) =>
        priorities.ListPrioritiesAsync(Actor(), search, ct);

    [HttpGet("priorities/{id:long}")]
    public Task<PriorityDefinitionView> GetPriority(long id, CancellationToken ct) =>
        priorities.GetPriorityAsync(Actor(), id, ct);

    [HttpPost("priorities")]
    public Task<PriorityDefinitionView> CreatePriority(CreatePriorityRequest request, CancellationToken ct) =>
        priorities.CreatePriorityAsync(Actor(), request.Code, request.Name, request.Description, request.Level,
            request.DisplayOrder, request.IsDefault, request.Reason, ct);

    [HttpPut("priorities/{id:long}")]
    public Task<PriorityDefinitionView> UpdatePriority(long id, UpdatePriorityRequest request, CancellationToken ct) =>
        priorities.UpdatePriorityAsync(Actor(), id, request.Code, request.Name, request.Description, request.Level,
            request.DisplayOrder, request.IsDefault, request.Reason, ct, request.ExpectedRowVersion);

    [HttpPost("priorities/{id:long}/active")]
    public Task<PriorityDefinitionView> SetPriorityActive(long id, MetadataActiveRequest request, CancellationToken ct) =>
        priorities.SetPriorityActiveAsync(Actor(), id, request.Active, request.Reason, ct);

    [HttpPost("priorities/{id:long}/default")]
    public Task<PriorityDefinitionView> SetDefaultPriority(long id, MetadataReasonRequest request, CancellationToken ct) =>
        priorities.SetDefaultPriorityAsync(Actor(), id, request.Reason, ct);

    [HttpGet("priorities/audit/{id:long}")]
    public Task<IReadOnlyCollection<PriorityConfigurationAudit>> GetPriorityAudit(long id, CancellationToken ct) =>
        priorities.GetPriorityAuditAsync(Actor(), id, ct);

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0)
            throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}

public sealed record CreateStatusRequest(string Code, string Name, string? Description, bool IsInitial, bool IsOpen,
    bool IsClosed, bool IsCancelled, int DisplayOrder, string? Reason = null);
public sealed record UpdateStatusRequest(string Code, string Name, string? Description, bool IsInitial, bool IsOpen,
    bool IsClosed, bool IsCancelled, int DisplayOrder, string? Reason, int? ExpectedRowVersion = null);
public sealed record SetTransitionRequest(string FromCode, string ToCode, string? Condition, bool Allowed,
    string? Reason, int? ExpectedRowVersion = null);
public sealed record CreatePriorityRequest(string Code, string Name, string? Description, int Level, int DisplayOrder,
    bool IsDefault, string? Reason = null);
public sealed record UpdatePriorityRequest(string Code, string Name, string? Description, int Level, int DisplayOrder,
    bool IsDefault, string? Reason, int? ExpectedRowVersion = null);
public sealed record MetadataActiveRequest(bool Active, string? Reason = null);
public sealed record MetadataReasonRequest(string? Reason);
