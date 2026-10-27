namespace PA.Tickets.Application.Foundations;

public static class FeatureSwitchCodes
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MESSAGE_CENTER"] = "Message Center",
        ["CHAT"] = "Chat",
        ["CHAT_ATTACHMENTS"] = "Chat attachments",
        ["ANNOUNCEMENTS"] = "Announcements",
        ["CHANNEL_INAPP"] = "In-app notifications",
        ["CHANNEL_EMAIL"] = "Email notifications",
        ["CHANNEL_WHATSAPP"] = "WhatsApp notifications",
        ["CHANNEL_PUSH"] = "Push notifications"
    };
}

public sealed record FeatureSwitchView(
    string Code,
    string Name,
    long? DepartmentId,
    bool Enabled,
    bool Configured,
    bool Inherited,
    int RowVersion);

public sealed record FeatureAvailabilityView(string Code, bool Enabled);

public interface IFeatureSwitchService
{
    Task<IReadOnlyCollection<FeatureSwitchView>> ListAsync(long actor, long? departmentId, CancellationToken ct);
    Task<FeatureSwitchView> SetAsync(long actor, string code, long? departmentId, bool enabled,
        int? expectedRowVersion, string? reason, CancellationToken ct);
    Task<bool> IsEnabledAsync(string code, long? departmentId, CancellationToken ct);
    Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorAsync(long actor, CancellationToken ct);
}

public static class FeatureSwitchResolution
{
    public static bool IsEnabled(bool? departmentOverride, bool? globalValue) => departmentOverride ?? globalValue ?? false;
}

public sealed class FeatureDisabledException() : Exception("The requested feature is currently unavailable.")
{
    public const string ErrorCode = "FEATURE_DISABLED";
}

public sealed class FeatureSwitchUnavailableException(Exception innerException)
    : Exception("Feature configuration is unavailable.", innerException)
{
    public const string ErrorCode = "FEATURE_SWITCH_UNAVAILABLE";
}
