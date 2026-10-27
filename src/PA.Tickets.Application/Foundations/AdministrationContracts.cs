namespace PA.Tickets.Application.Foundations;

public sealed record AdministrationUser(
    long Id,
    string UserCode,
    string LoginName,
    string DisplayName,
    string? Email,
    long? DepartmentId,
    long? TeamId,
    bool Active,
    string? Phone,
    long? ManagerUserId,
    bool EmailVerified);
public sealed record UserProfileUpdate(string DisplayName, string? Email, string? Phone);
public sealed record AdministrationDepartment(long Id, string Code, string Name, string? Description, bool Active);
public sealed record AdministrationTeam(long Id, long DepartmentId, string Code, string Name, string? Description, bool Active);
public sealed record AdministrationRole(long Id, string Code, string Name, string? Description, bool SystemRole, bool Active);
public sealed record AdministrationPermission(
    long Id,
    string Code,
    string Name,
    string? Description,
    string ModuleCode,
    string ActionCode,
    string ScopeClass,
    string SecurityClass,
    bool ReadOnly,
    bool Active);
public sealed record AdministrationRolePermission(long RoleId, long PermissionId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, bool Active, string EffectiveStatus);
public sealed record AdministrationRolePermissionChange(long PermissionId, bool Granted);
public sealed record AdministrationRoleScope(
    long Id,
    long UserRoleId,
    string ScopeType,
    long? OrganizationId,
    long? DepartmentId,
    long? TeamId,
    long? ClientId,
    string? ResourceType,
    string? ResourceId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    bool Active);
public sealed record AdministrationMembership(long Id, long UserId, long RoleId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, bool Active);
public sealed record AdministrationClientMembership(long Id, long UserId, long ClientId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, bool Active);
public sealed record AdminHeadDelegation(long Id, long AdminHeadUserId, long AdminUserId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, bool Active);
public sealed record RbacProposal(
    string Id,
    string OperationType,
    string Status,
    long TargetRoleId,
    string TargetRoleCode,
    long TargetPermissionId,
    string TargetPermissionCode,
    string TargetPermissionName,
    string ModuleCode,
    string ActionCode,
    string ScopeClass,
    string SecurityClass,
    long InitiatorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string Reason,
    long? ApproverUserId,
    long? CancelledByUserId,
    DateTimeOffset? DecisionAt,
    string? DecisionReason,
    string? ApplicationOutcome,
    string CorrelationId,
    int RowVersion,
    bool CanApprove,
    bool CanReject,
    bool CanCancel);

public sealed record AuthorizationResource(
    long? TicketId = null,
    long? DepartmentId = null,
    long? TeamId = null,
    long? ClientId = null,
    long? AssignedUserId = null,
    long? OrganizationId = null,
    string? ResourceType = null,
    string? ResourceId = null);

public interface IAuthorizationService
{
    Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct);
    Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct);
}

public interface IAdministrationService
{
    Task<IReadOnlyCollection<AdministrationUser>> ListUsersAsync(long actor, CancellationToken ct);
    Task<AdministrationUser> GetUserAsync(long actor, long userId, CancellationToken ct);
    Task<AdministrationUser> UpdateUserProfileAsync(long actor, long userId, UserProfileUpdate profile, CancellationToken ct);
    Task<AdministrationUser> SetReportingManagerAsync(long actor, long userId, long? managerUserId, CancellationToken ct);
    Task<AdministrationUser> CreateUserAsync(long actor, string userCode, string loginName, string displayName, string? email, long? departmentId, long? teamId, CancellationToken ct);
    Task<AdministrationUser> SetUserActiveAsync(long actor, long userId, bool active, CancellationToken ct);
    Task SetUserPasswordAsync(long actor, long userId, string password, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationDepartment>> ListDepartmentsAsync(long actor, CancellationToken ct);
    Task<AdministrationDepartment> CreateDepartmentAsync(long actor, string code, string name, string? description, CancellationToken ct);
    Task<AdministrationDepartment> SetDepartmentActiveAsync(long actor, long departmentId, bool active, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationTeam>> ListTeamsAsync(long actor, CancellationToken ct);
    Task<AdministrationTeam> CreateTeamAsync(long actor, long departmentId, string code, string name, string? description, CancellationToken ct);
    Task<AdministrationTeam> SetTeamActiveAsync(long actor, long teamId, bool active, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationRole>> ListRolesAsync(long actor, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationPermission>> ListPermissionsAsync(long actor, CancellationToken ct);
    Task<AdministrationRole> UpdateSystemRoleDisplayLabelAsync(long actor, long roleId, string displayLabel, string reason, CancellationToken ct);
    Task<AdministrationRole> CreateCustomRoleAsync(long actor, string code, string name, string? description, CancellationToken ct);
    Task<AdministrationRole> UpdateCustomRoleAsync(long actor, long roleId, string name, string? description, CancellationToken ct);
    Task DeactivateCustomRoleAsync(long actor, long roleId, string reason, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationRolePermission>> ListRolePermissionsAsync(long actor, long roleId, CancellationToken ct);
    Task SetRolePermissionAsync(long actor, long roleId, long permissionId, bool granted, string confirmation, string reason, CancellationToken ct);
    Task SetRolePermissionsAsync(
        long actor,
        long roleId,
        IReadOnlyCollection<long> expectedActivePermissionIds,
        IReadOnlyCollection<AdministrationRolePermissionChange> changes,
        string reason,
        CancellationToken ct);
    Task<RbacProposal> CreateSensitivePermissionGrantProposalAsync(
        long actor,
        long roleId,
        long permissionId,
        IReadOnlyCollection<long> expectedActivePermissionIds,
        string reason,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct);
    Task<IReadOnlyCollection<RbacProposal>> ListRbacProposalsAsync(long actor, CancellationToken ct);
    Task<RbacProposal> GetRbacProposalAsync(long actor, string proposalId, CancellationToken ct);
    Task<RbacProposal> ApproveRbacProposalAsync(long actor, string proposalId, int expectedVersion, CancellationToken ct);
    Task<RbacProposal> RejectRbacProposalAsync(long actor, string proposalId, int expectedVersion, string reason, CancellationToken ct);
    Task<RbacProposal> CancelRbacProposalAsync(long actor, string proposalId, int expectedVersion, string reason, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationMembership>> ListMembershipsAsync(long actor, long userId, CancellationToken ct);
    Task<AdministrationMembership> AssignRoleAsync(long actor, long userId, long roleId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationRoleScope>> ListRoleScopesAsync(long actor, long membershipId, CancellationToken ct);
    Task<AdministrationRoleScope> AssignRoleScopeAsync(long actor, long membershipId, string scopeType, long? organizationId, long? departmentId, long? teamId, long? clientId, string? resourceType, string? resourceId, DateTimeOffset validFrom, DateTimeOffset? validTo, string reason, CancellationToken ct);
    Task RemoveRoleScopeAsync(long actor, long scopeId, string reason, CancellationToken ct);
    Task<IReadOnlyCollection<AdminHeadDelegation>> ListAdminDelegationsAsync(long actor, CancellationToken ct);
    Task<AdminHeadDelegation> AssignAdminDelegationAsync(long actor, long adminHeadUserId, long adminUserId, DateTimeOffset validFrom, DateTimeOffset? validTo, string reason, CancellationToken ct);
    Task RemoveAdminDelegationAsync(long actor, long delegationId, string reason, CancellationToken ct);
    Task<AdministrationClientMembership> AssignClientAsync(long actor, long userId, long clientId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct);
    Task RemoveRoleAsync(long actor, long membershipId, CancellationToken ct);
    Task RemoveClientAsync(long actor, long membershipId, CancellationToken ct);
}
