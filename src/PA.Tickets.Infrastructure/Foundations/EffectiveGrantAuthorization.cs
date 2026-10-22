using ApplicationEffectiveGrantAuthorization = PA.Tickets.Application.Foundations.EffectiveGrantAuthorization;

namespace PA.Tickets.Infrastructure.Foundations;

/// <summary>Compatibility facade; effective-grant policy lives in the application layer.</summary>
public static class EffectiveGrantAuthorization
{
    public const string Unknown = ApplicationEffectiveGrantAuthorization.Unknown;
    public const string Active = ApplicationEffectiveGrantAuthorization.Active;
    public const string Expired = ApplicationEffectiveGrantAuthorization.Expired;
    public const string Revoked = ApplicationEffectiveGrantAuthorization.Revoked;

    public static bool IsEffective(
        bool active,
        string? effectiveStatus,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset now) =>
        ApplicationEffectiveGrantAuthorization.IsEffective(active, effectiveStatus, validFrom, validTo, now);
}
