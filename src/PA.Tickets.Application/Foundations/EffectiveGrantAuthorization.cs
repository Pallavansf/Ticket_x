namespace PA.Tickets.Application.Foundations;

/// <summary>Provider-independent eligibility semantics for already-retrieved role-permission grants.</summary>
public static class EffectiveGrantAuthorization
{
    public const string Unknown = "UNKNOWN";
    public const string Active = "ACTIVE";
    public const string Expired = "EXPIRED";
    public const string Revoked = "REVOKED";

    public static bool IsEffective(
        bool active,
        string? effectiveStatus,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset now)
    {
        if (!active || !string.Equals(effectiveStatus, Active, StringComparison.OrdinalIgnoreCase) ||
            !validFrom.HasValue)
            return false;

        return validFrom.Value <= now && (!validTo.HasValue || validTo.Value > now);
    }
}
