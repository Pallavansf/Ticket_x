namespace PA.Tickets.Application.Foundations;

/// <summary>Provider-independent scope-class hierarchy evaluation.</summary>
public static class ScopeAuthorizationPolicy
{
    public static bool ScopeMatchesClass(string scopeType, string scopeClass) => scopeClass.ToUpperInvariant() switch
    {
        "ORGANIZATION" => scopeType == "ORGANIZATION",
        "DEPARTMENT" => scopeType == "DEPARTMENT" || scopeType == "ORGANIZATION",
        "DEPARTMENT_ADMIN" => scopeType is "DEPARTMENT" or "ADMIN_GROUP" or "ORGANIZATION",
        "TEAM" => scopeType is "TEAM" or "DEPARTMENT" or "ORGANIZATION",
        "ADMIN_GROUP" => scopeType is "ADMIN_GROUP" or "ORGANIZATION",
        "RECORD" => scopeType is "RECORD" or "TEAM" or "DEPARTMENT" or "ORGANIZATION",
        "NONE" => true,
        _ => false
    };
}
