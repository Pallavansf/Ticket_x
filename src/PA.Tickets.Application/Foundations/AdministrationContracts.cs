namespace PA.Tickets.Application.Foundations;

public sealed record AdministrationUser(long Id, string UserCode, string LoginName, string DisplayName, string? Email, long? DepartmentId, long? TeamId, bool Active);
public sealed record AdministrationDepartment(long Id, string Code, string Name, string? Description, bool Active);
public sealed record AdministrationTeam(long Id, long DepartmentId, string Code, string Name, string? Description, bool Active);
public sealed record AdministrationRole(long Id, string Code, string Name, string? Description, bool SystemRole, bool Active);
public sealed record AdministrationPermission(long Id, string Code, string Name, string? Description, bool Active);
public sealed record AdministrationMembership(long Id, long UserId, long RoleId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, bool Active);
public sealed record AdministrationClientMembership(long Id, long UserId, long ClientId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, bool Active);

public sealed record AuthorizationResource(long? TicketId = null, long? DepartmentId = null, long? TeamId = null, long? ClientId = null, long? AssignedUserId = null);

public interface IAuthorizationService
{
    Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct);
    Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct);
}

public interface IAdministrationService
{
    Task<IReadOnlyCollection<AdministrationUser>> ListUsersAsync(long actor, CancellationToken ct);
    Task<AdministrationUser> GetUserAsync(long actor, long userId, CancellationToken ct);
    Task<AdministrationUser> CreateUserAsync(long actor, string userCode, string loginName, string displayName, string? email, long? departmentId, long? teamId, CancellationToken ct);
    Task<AdministrationUser> SetUserActiveAsync(long actor, long userId, bool active, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationDepartment>> ListDepartmentsAsync(long actor, CancellationToken ct);
    Task<AdministrationDepartment> CreateDepartmentAsync(long actor, string code, string name, string? description, CancellationToken ct);
    Task<AdministrationDepartment> SetDepartmentActiveAsync(long actor, long departmentId, bool active, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationTeam>> ListTeamsAsync(long actor, CancellationToken ct);
    Task<AdministrationTeam> CreateTeamAsync(long actor, long departmentId, string code, string name, string? description, CancellationToken ct);
    Task<AdministrationTeam> SetTeamActiveAsync(long actor, long teamId, bool active, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationRole>> ListRolesAsync(long actor, CancellationToken ct);
    Task<IReadOnlyCollection<AdministrationPermission>> ListPermissionsAsync(long actor, CancellationToken ct);
    Task<AdministrationMembership> AssignRoleAsync(long actor, long userId, long roleId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct);
    Task<AdministrationClientMembership> AssignClientAsync(long actor, long userId, long clientId, DateTimeOffset validFrom, DateTimeOffset? validTo, CancellationToken ct);
    Task RemoveRoleAsync(long actor, long membershipId, CancellationToken ct);
    Task RemoveClientAsync(long actor, long membershipId, CancellationToken ct);
}