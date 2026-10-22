namespace PA.Tickets.Application.Foundations;

/// <summary>Pure eligibility rule for a target that may be treated as a delegated Admin.</summary>
public static class AdminHeadDelegationPolicy
{
    public static bool IsDelegatedAdminOnly(IReadOnlyCollection<string> activeRoleCodes)
    {
        if (activeRoleCodes is null || activeRoleCodes.Count == 0)
            return false;

        var hasAdmin = activeRoleCodes.Contains("ADMIN", StringComparer.Ordinal);
        var hasPrivilegedRole = activeRoleCodes.Contains("ADMIN_HEAD", StringComparer.Ordinal) ||
                                activeRoleCodes.Contains("SUPER_ADMIN", StringComparer.Ordinal) ||
                                activeRoleCodes.Contains("ADMINISTRATOR", StringComparer.Ordinal);
        return hasAdmin && !hasPrivilegedRole;
    }
}
