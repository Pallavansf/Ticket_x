namespace PA.Tickets.Application.Foundations;

/// <summary>
/// Provider-independent role and permission ceiling rules.
/// Persistence supplies the facts; this component only evaluates the policy.
/// </summary>
public static class RoleAuthorizationPolicy
{
    public const string User = "USER";
    public const string Admin = "ADMIN";
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string AdminHead = "ADMIN_HEAD";
    public const string Spectator = "SPECTATOR";
    public const string LegacyAdministrator = "ADMINISTRATOR";

    public static bool AllowsPermission(string role, bool systemRole, string securityClass, string actionCode, bool readOnly)
    {
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(securityClass) ||
            string.IsNullOrWhiteSpace(actionCode))
            return false;

        var security = securityClass.ToUpperInvariant();
        var action = actionCode.ToUpperInvariant();
        if (security is not ("READ_ONLY" or "BUSINESS" or "DEPARTMENT_ADMIN" or "ORGANIZATION_ADMIN" or "SECURITY_GOVERNANCE"))
            return false;
        if ((security == "READ_ONLY") != readOnly ||
            (readOnly && action is not ("VIEW" or "READ" or "LIST" or "EXPORT")))
            return false;

        return role.ToUpperInvariant() switch
        {
            SuperAdmin => systemRole,
            Spectator => systemRole && readOnly && security == "READ_ONLY" &&
                         action is "VIEW" or "READ" or "LIST" or "EXPORT",
            User => systemRole && security is "READ_ONLY" or "BUSINESS",
            Admin or AdminHead => systemRole && security is "READ_ONLY" or "BUSINESS" or "DEPARTMENT_ADMIN",
            LegacyAdministrator => systemRole && security is "READ_ONLY" or "BUSINESS",
            "OPERATIONS" => systemRole && security is "READ_ONLY" or "BUSINESS" or "DEPARTMENT_ADMIN",
            "AUDITOR" => systemRole && readOnly && security == "READ_ONLY" &&
                         action is "VIEW" or "READ" or "LIST" or "EXPORT",
            _ => !systemRole && IsCustomRoleCode(role) &&
                 security is "READ_ONLY" or "BUSINESS" or "DEPARTMENT_ADMIN"
        };
    }

    public static bool IsLegacyAdministratorPermission(string permission) =>
        permission.Equals("TICKET_READ", StringComparison.OrdinalIgnoreCase) ||
        permission.Equals("TICKET_UPDATE", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresIndependentApproval(string securityClass) =>
        securityClass.ToUpperInvariant() is "DEPARTMENT_ADMIN" or "ORGANIZATION_ADMIN" or "SECURITY_GOVERNANCE";

    private static bool IsCustomRoleCode(string role) =>
        role.Length is >= 2 and <= 64 &&
        char.IsAsciiLetterUpper(role[0]) &&
        role.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_') &&
        role is not ("USER" or "ADMIN" or "SUPER_ADMIN" or "ADMIN_HEAD" or "SPECTATOR" or "SUPERADMIN" or "ADMINHEAD" or
            "OPERATIONS" or "ADMINISTRATOR" or "AUDITOR");
}
