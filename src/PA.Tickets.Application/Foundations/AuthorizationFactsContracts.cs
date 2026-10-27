namespace PA.Tickets.Application.Foundations;

public sealed record AuthorizationActorFact(long Id, long? DepartmentId, long? TeamId, bool Active);

public sealed record AuthorizationPermissionFact(
    long Id,
    string Code,
    string ModuleCode,
    string ActionCode,
    string ScopeClass,
    string SecurityClass,
    bool ReadOnly,
    bool Active);

public sealed record AuthorizationRoleFact(long MembershipId, string Code, bool SystemRole, long RoleId = 0);

public sealed record AuthorizationGrantFact(
    long MembershipId,
    string RoleCode,
    bool SystemRole,
    bool Active,
    string EffectiveStatus,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string PermissionCode,
    string SecurityClass,
    string ScopeClass,
    bool ReadOnly);

public sealed record AuthorizationScopeFact(
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

public sealed record AuthorizationFacts(
    AuthorizationActorFact? Actor,
    AuthorizationPermissionFact? Permission,
    IReadOnlyCollection<AuthorizationRoleFact> ActiveRoles,
    IReadOnlyCollection<AuthorizationGrantFact> Grants,
    IReadOnlyCollection<AuthorizationScopeFact> Scopes);

public interface IAuthorizationFactsReader
{
    Task<AuthorizationFacts> ReadAsync(
        long actorUserId,
        string permission,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
