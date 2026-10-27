namespace PA.Tickets.Application.Foundations;

public static class SystemRoleDisplayLabelPolicy
{
    public static string ValidateAndNormalize(string roleCode, bool systemRole, string? displayLabel)
    {
        if (!systemRole || roleCode is not (RoleAuthorizationPolicy.Admin or RoleAuthorizationPolicy.AdminHead))
            throw new UnauthorizedAccessException("Only the ADMIN and ADMIN_HEAD display labels are configurable.");
        if (string.IsNullOrWhiteSpace(displayLabel))
            throw new ArgumentException("A role display label is required.", nameof(displayLabel));

        var normalized = displayLabel.Trim();
        if (normalized.Length > 200)
            throw new ArgumentException("A role display label cannot exceed 200 characters.", nameof(displayLabel));
        return normalized;
    }
}
