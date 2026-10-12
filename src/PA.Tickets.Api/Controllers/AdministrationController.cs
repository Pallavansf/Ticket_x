using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/admin")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class AdministrationController(IAdministrationService administration) : ControllerBase
{
    [HttpGet("users")] public Task<IReadOnlyCollection<AdministrationUser>> Users(CancellationToken ct) => administration.ListUsersAsync(Actor(), ct);
    [HttpGet("users/{id:long}")] public Task<AdministrationUser> GetUser(long id, CancellationToken ct) => administration.GetUserAsync(Actor(), id, ct);
    [HttpPost("users")] public Task<AdministrationUser> CreateUser(CreateUserRequest request, CancellationToken ct) => administration.CreateUserAsync(Actor(), request.UserCode, request.LoginName, request.DisplayName, request.Email, request.DepartmentId, request.TeamId, ct);
    [HttpPost("users/{id:long}/active")] public Task<AdministrationUser> SetUserActive(long id, ActiveRequest request, CancellationToken ct) => administration.SetUserActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("departments")] public Task<IReadOnlyCollection<AdministrationDepartment>> Departments(CancellationToken ct) => administration.ListDepartmentsAsync(Actor(), ct);
    [HttpPost("departments")] public Task<AdministrationDepartment> CreateDepartment(CreateDepartmentRequest request, CancellationToken ct) => administration.CreateDepartmentAsync(Actor(), request.Code, request.Name, request.Description, ct);
    [HttpPost("departments/{id:long}/active")] public Task<AdministrationDepartment> SetDepartmentActive(long id, ActiveRequest request, CancellationToken ct) => administration.SetDepartmentActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("teams")] public Task<IReadOnlyCollection<AdministrationTeam>> Teams(CancellationToken ct) => administration.ListTeamsAsync(Actor(), ct);
    [HttpPost("teams")] public Task<AdministrationTeam> CreateTeam(CreateTeamRequest request, CancellationToken ct) => administration.CreateTeamAsync(Actor(), request.DepartmentId, request.Code, request.Name, request.Description, ct);
    [HttpPost("teams/{id:long}/active")] public Task<AdministrationTeam> SetTeamActive(long id, ActiveRequest request, CancellationToken ct) => administration.SetTeamActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("roles")] public Task<IReadOnlyCollection<AdministrationRole>> Roles(CancellationToken ct) => administration.ListRolesAsync(Actor(), ct);
    [HttpGet("permissions")] public Task<IReadOnlyCollection<AdministrationPermission>> Permissions(CancellationToken ct) => administration.ListPermissionsAsync(Actor(), ct);
    [HttpPost("user-roles")] public Task<AdministrationMembership> AssignRole(AssignRoleRequest request, CancellationToken ct) => administration.AssignRoleAsync(Actor(), request.UserId, request.RoleId, request.ValidFrom, request.ValidTo, ct);
    [HttpDelete("user-roles/{id:long}")] public async Task<IActionResult> RemoveRole(long id, CancellationToken ct) { await administration.RemoveRoleAsync(Actor(), id, ct); return NoContent(); }
    [HttpPost("user-clients")] public Task<AdministrationClientMembership> AssignClient(AssignClientRequest request, CancellationToken ct) => administration.AssignClientAsync(Actor(), request.UserId, request.ClientId, request.ValidFrom, request.ValidTo, ct);
    [HttpDelete("user-clients/{id:long}")] public async Task<IActionResult> RemoveClient(long id, CancellationToken ct) { await administration.RemoveClientAsync(Actor(), id, ct); return NoContent(); }

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(raw, out var id) || id <= 0) throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
        return id;
    }
}

public sealed record CreateUserRequest(string UserCode, string LoginName, string DisplayName, string? Email, long? DepartmentId, long? TeamId);
public sealed record CreateDepartmentRequest(string Code, string Name, string? Description);
public sealed record CreateTeamRequest(long DepartmentId, string Code, string Name, string? Description);
public sealed record ActiveRequest(bool Active);
public sealed record AssignRoleRequest(long UserId, long RoleId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);
public sealed record AssignClientRequest(long UserId, long ClientId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);