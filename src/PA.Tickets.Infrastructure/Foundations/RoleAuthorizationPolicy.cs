using ApplicationRoleAuthorizationPolicy = PA.Tickets.Application.Foundations.RoleAuthorizationPolicy;

namespace PA.Tickets.Infrastructure.Foundations;

/// <summary>Compatibility façade; policy logic lives in the application layer.</summary>
internal static class RoleAuthorizationPolicy
{
    public const string User = ApplicationRoleAuthorizationPolicy.User;
    public const string Admin = ApplicationRoleAuthorizationPolicy.Admin;
    public const string SuperAdmin = ApplicationRoleAuthorizationPolicy.SuperAdmin;
    public const string AdminHead = ApplicationRoleAuthorizationPolicy.AdminHead;
    public const string Spectator = ApplicationRoleAuthorizationPolicy.Spectator;
    public const string LegacyAdministrator = ApplicationRoleAuthorizationPolicy.LegacyAdministrator;

    public static bool AllowsPermission(string role, bool systemRole, string securityClass, string actionCode, bool readOnly) =>
        ApplicationRoleAuthorizationPolicy.AllowsPermission(role, systemRole, securityClass, actionCode, readOnly);

    public static bool IsCanonicalRoleCode(string? role) =>
        ApplicationRoleAuthorizationPolicy.IsCanonicalRoleCode(role);

    public static bool IsCanonicalPermissionCode(string? permission) =>
        ApplicationRoleAuthorizationPolicy.IsCanonicalPermissionCode(permission);

    public static bool AllowsPermissionCode(string role, string permissionCode) =>
        ApplicationRoleAuthorizationPolicy.AllowsPermissionCode(role, permissionCode);

    public static void EnsurePermissionRevocationAllowed(string role, string permissionCode) =>
        ApplicationRoleAuthorizationPolicy.EnsurePermissionRevocationAllowed(role, permissionCode);

    public static bool IsFixedCorePermission(string role, string permissionCode) =>
        ApplicationRoleAuthorizationPolicy.IsFixedCorePermission(role, permissionCode);

    public static bool IsLegacyAdministratorPermission(string permission) =>
        ApplicationRoleAuthorizationPolicy.IsLegacyAdministratorPermission(permission);

    public static bool RequiresIndependentApproval(string securityClass) =>
        ApplicationRoleAuthorizationPolicy.RequiresIndependentApproval(securityClass);
}
