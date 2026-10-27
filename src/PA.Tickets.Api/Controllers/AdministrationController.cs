using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
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
    [HttpPatch("users/{id:long}/profile")] public Task<AdministrationUser> UpdateUserProfile(long id, UpdateUserProfileRequest request, CancellationToken ct) =>
        administration.UpdateUserProfileAsync(Actor(), id, new UserProfileUpdate(request.DisplayName, request.Email, request.Phone), ct);
    [HttpPut("users/{id:long}/reporting-manager")] public Task<AdministrationUser> SetReportingManager(long id, SetReportingManagerRequest request, CancellationToken ct) =>
        administration.SetReportingManagerAsync(Actor(), id, request.ManagerUserId, ct);
    [HttpPost("users")] public Task<AdministrationUser> CreateUser(CreateUserRequest request, CancellationToken ct) => administration.CreateUserAsync(Actor(), request.UserCode, request.LoginName, request.DisplayName, request.Email, request.DepartmentId, request.TeamId, ct);
    [HttpPost("users/{id:long}/active")] public Task<AdministrationUser> SetUserActive(long id, ActiveRequest request, CancellationToken ct) => administration.SetUserActiveAsync(Actor(), id, request.Active, ct);
    [HttpPost("users/{id:long}/password")] public async Task<IActionResult> SetUserPassword(long id, SetUserPasswordRequest request, CancellationToken ct) { await administration.SetUserPasswordAsync(Actor(), id, request.Password, ct); return NoContent(); }

    [HttpGet("departments")] public Task<IReadOnlyCollection<AdministrationDepartment>> Departments(CancellationToken ct) => administration.ListDepartmentsAsync(Actor(), ct);
    [HttpPost("departments")] public Task<AdministrationDepartment> CreateDepartment(CreateDepartmentRequest request, CancellationToken ct) => administration.CreateDepartmentAsync(Actor(), request.Code, request.Name, request.Description, ct);
    [HttpPost("departments/{id:long}/active")] public Task<AdministrationDepartment> SetDepartmentActive(long id, ActiveRequest request, CancellationToken ct) => administration.SetDepartmentActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("teams")] public Task<IReadOnlyCollection<AdministrationTeam>> Teams(CancellationToken ct) => administration.ListTeamsAsync(Actor(), ct);
    [HttpPost("teams")] public Task<AdministrationTeam> CreateTeam(CreateTeamRequest request, CancellationToken ct) => administration.CreateTeamAsync(Actor(), request.DepartmentId, request.Code, request.Name, request.Description, ct);
    [HttpPost("teams/{id:long}/active")] public Task<AdministrationTeam> SetTeamActive(long id, ActiveRequest request, CancellationToken ct) => administration.SetTeamActiveAsync(Actor(), id, request.Active, ct);

    [HttpGet("roles")] public Task<IReadOnlyCollection<AdministrationRole>> Roles(CancellationToken ct) => administration.ListRolesAsync(Actor(), ct);
    [HttpPut("roles/{id:long}/display-label")] public Task<AdministrationRole> UpdateSystemRoleDisplayLabel(long id, UpdateSystemRoleDisplayLabelRequest request, CancellationToken ct) =>
        administration.UpdateSystemRoleDisplayLabelAsync(Actor(), id, request.DisplayLabel, request.Reason, ct);
    [HttpPost("roles")] public Task<AdministrationRole> CreateRole(CreateCustomRoleRequest request, CancellationToken ct) =>
        administration.CreateCustomRoleAsync(Actor(), request.Code, request.Name, request.Description, ct);
    [HttpPut("roles/{id:long}")] public Task<AdministrationRole> UpdateRole(long id, UpdateCustomRoleRequest request, CancellationToken ct) =>
        administration.UpdateCustomRoleAsync(Actor(), id, request.Name, request.Description, ct);
    [HttpPost("roles/{id:long}/deactivate")] public async Task<IActionResult> DeactivateRole(long id, RoleReasonRequest request, CancellationToken ct)
    {
        await administration.DeactivateCustomRoleAsync(Actor(), id, request.Reason, ct);
        return NoContent();
    }
    [HttpGet("permissions")] public Task<IReadOnlyCollection<AdministrationPermission>> Permissions(CancellationToken ct) => administration.ListPermissionsAsync(Actor(), ct);
    [HttpGet("roles/{id:long}/permissions")] public Task<IReadOnlyCollection<AdministrationRolePermission>> RolePermissions(long id, CancellationToken ct) =>
        administration.ListRolePermissionsAsync(Actor(), id, ct);
    [HttpPut("roles/{roleId:long}/permissions/{permissionId:long}")] public async Task<IActionResult> SetRolePermission(
        long roleId, long permissionId, SetRolePermissionRequest request, CancellationToken ct)
    {
        await administration.SetRolePermissionAsync(Actor(), roleId, permissionId, request.Granted, request.Confirmation, request.Reason, ct);
        return NoContent();
    }
    [HttpPost("roles/{roleId:long}/permissions/batch")] public async Task<IActionResult> SetRolePermissions(
        long roleId, SetRolePermissionsRequest request, CancellationToken ct)
    {
        await administration.SetRolePermissionsAsync(
            Actor(), roleId, request.ExpectedActivePermissionIds, request.Changes,
            request.Reason, ct);
        return NoContent();
    }
    [HttpPost("roles/{roleId:long}/permission-proposals")]
    public Task<RbacProposal> CreatePermissionProposal(
        long roleId, CreatePermissionProposalRequest request, CancellationToken ct) =>
        administration.CreateSensitivePermissionGrantProposalAsync(
            Actor(), roleId, request.PermissionId, request.ExpectedActivePermissionIds,
            request.Reason, request.IdempotencyKey, HttpContext.TraceIdentifier, ct);
    [HttpGet("permission-proposals")]
    public Task<IReadOnlyCollection<RbacProposal>> PermissionProposals(CancellationToken ct) =>
        administration.ListRbacProposalsAsync(Actor(), ct);
    [HttpGet("permission-proposals/{proposalId}")]
    public Task<RbacProposal> PermissionProposal(string proposalId, CancellationToken ct) =>
        administration.GetRbacProposalAsync(Actor(), proposalId, ct);
    [HttpPost("permission-proposals/{proposalId}/approve")]
    public Task<RbacProposal> ApprovePermissionProposal(
        string proposalId, ProposalVersionRequest request, CancellationToken ct) =>
        administration.ApproveRbacProposalAsync(Actor(), proposalId, request.ExpectedVersion, ct);
    [HttpPost("permission-proposals/{proposalId}/reject")]
    public Task<RbacProposal> RejectPermissionProposal(
        string proposalId, ProposalDecisionRequest request, CancellationToken ct) =>
        administration.RejectRbacProposalAsync(Actor(), proposalId, request.ExpectedVersion, request.Reason, ct);
    [HttpPost("permission-proposals/{proposalId}/cancel")]
    public Task<RbacProposal> CancelPermissionProposal(
        string proposalId, ProposalDecisionRequest request, CancellationToken ct) =>
        administration.CancelRbacProposalAsync(Actor(), proposalId, request.ExpectedVersion, request.Reason, ct);
    [HttpGet("users/{id:long}/roles")] public Task<IReadOnlyCollection<AdministrationMembership>> UserRoles(long id, CancellationToken ct) =>
        administration.ListMembershipsAsync(Actor(), id, ct);
    [HttpPost("user-roles")] public Task<AdministrationMembership> AssignRole(AssignRoleRequest request, CancellationToken ct) => administration.AssignRoleAsync(Actor(), request.UserId, request.RoleId, request.ValidFrom, request.ValidTo, ct);
    [HttpGet("user-roles/{id:long}/scopes")] public Task<IReadOnlyCollection<AdministrationRoleScope>> RoleScopes(long id, CancellationToken ct) =>
        administration.ListRoleScopesAsync(Actor(), id, ct);
    [HttpPost("user-roles/{id:long}/scopes")] public Task<AdministrationRoleScope> AssignRoleScope(
        long id, AssignRoleScopeRequest request, CancellationToken ct) =>
        administration.AssignRoleScopeAsync(Actor(), id, request.ScopeType, request.OrganizationId, request.DepartmentId,
            request.TeamId, request.ClientId, request.ResourceType, request.ResourceId, request.ValidFrom, request.ValidTo, request.Reason, ct);
    [HttpDelete("role-scopes/{id:long}")] public async Task<IActionResult> RemoveRoleScope(long id, [FromQuery, Required, StringLength(1000, MinimumLength = 1)] string reason, CancellationToken ct)
    {
        await administration.RemoveRoleScopeAsync(Actor(), id, reason, ct);
        return NoContent();
    }
    [HttpGet("admin-head-delegations")] public Task<IReadOnlyCollection<AdminHeadDelegation>> AdminHeadDelegations(CancellationToken ct) =>
        administration.ListAdminDelegationsAsync(Actor(), ct);
    [HttpPost("admin-head-delegations")] public Task<AdminHeadDelegation> AssignAdminDelegation(AssignAdminDelegationRequest request, CancellationToken ct) =>
        administration.AssignAdminDelegationAsync(Actor(), request.AdminHeadUserId, request.AdminUserId, request.ValidFrom, request.ValidTo, request.Reason, ct);
    [HttpDelete("admin-head-delegations/{id:long}")] public async Task<IActionResult> RemoveAdminDelegation(
        long id,
        [FromQuery, Required, StringLength(1000, MinimumLength = 1)] string reason,
        CancellationToken ct)
    {
        await administration.RemoveAdminDelegationAsync(Actor(), id, reason, ct);
        return NoContent();
    }
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
public sealed record UpdateUserProfileRequest(
    [Required, StringLength(300, MinimumLength = 1)] string DisplayName,
    [EmailAddress, StringLength(320)] string? Email,
    [StringLength(50)] string? Phone);
public sealed record SetReportingManagerRequest(long? ManagerUserId);
public sealed record CreateDepartmentRequest(string Code, string Name, string? Description);
public sealed record CreateTeamRequest(long DepartmentId, string Code, string Name, string? Description);
public sealed record ActiveRequest(bool Active);
public sealed record SetUserPasswordRequest([Required, StringLength(1024, MinimumLength = 12)] string Password);
public sealed record AssignRoleRequest(long UserId, long RoleId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);
public sealed record CreateCustomRoleRequest(
    [Required, StringLength(64, MinimumLength = 2)] string Code,
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [StringLength(1000)] string? Description);
public sealed record UpdateCustomRoleRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [StringLength(1000)] string? Description);
public sealed record UpdateSystemRoleDisplayLabelRequest(
    [Required, StringLength(200, MinimumLength = 1)] string DisplayLabel,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record RoleReasonRequest([Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record SetRolePermissionRequest(
    bool Granted,
    [StringLength(100)] string Confirmation,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record SetRolePermissionsRequest(
    [Required] long[] ExpectedActivePermissionIds,
    [Required, MinLength(1), MaxLength(250)] AdministrationRolePermissionChange[] Changes,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record ProposalVersionRequest([Range(1, int.MaxValue)] int ExpectedVersion);
public sealed record ProposalDecisionRequest(
    [Range(1, int.MaxValue)] int ExpectedVersion,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record CreatePermissionProposalRequest(
    [Range(1, long.MaxValue)] long PermissionId,
    [Required, MaxLength(5000)] long[] ExpectedActivePermissionIds,
    [Required, StringLength(100, MinimumLength = 16), RegularExpression("^[A-Za-z0-9._:-]+$")] string IdempotencyKey,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record AssignRoleScopeRequest(
    [Required, StringLength(30, MinimumLength = 1)] string ScopeType,
    long? OrganizationId,
    long? DepartmentId,
    long? TeamId,
    long? ClientId,
    [StringLength(100)] string? ResourceType,
    [StringLength(300)] string? ResourceId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record AssignAdminDelegationRequest(
    long AdminHeadUserId,
    long AdminUserId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    [Required, StringLength(1000, MinimumLength = 1)] string Reason);
public sealed record AssignClientRequest(long UserId, long ClientId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);
