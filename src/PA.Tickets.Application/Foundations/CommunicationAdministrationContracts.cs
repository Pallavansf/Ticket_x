using PA.Tickets.Domain.Communications;

namespace PA.Tickets.Application.Foundations;

public sealed record CommunicationAdminConfiguration(
    IReadOnlyCollection<CommunicationProviderConfiguration> Providers,
    IReadOnlyCollection<CommunicationChannelConfiguration> Channels);

public sealed record CommunicationProviderConfiguration(
    string Code,
    CommunicationChannel Channel,
    bool Enabled,
    int MaxAttempts,
    int RetryDelaySeconds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int RowVersion);

public sealed record CommunicationChannelConfiguration(
    CommunicationChannel Channel,
    bool Enabled,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int RowVersion);

public sealed record CommunicationProviderConfigurationRequest(
    bool Enabled,
    int MaxAttempts,
    int RetryDelaySeconds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int ExpectedRowVersion,
    string? Reason);

public sealed record CommunicationChannelConfigurationRequest(
    bool Enabled,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int ExpectedRowVersion,
    string? Reason);

public sealed record CommunicationTemplateDraft(
    string Code,
    CommunicationChannel Channel,
    string? Subject,
    string Body,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record CommunicationTemplateSummary(
    long VersionId,
    string Code,
    CommunicationChannel Channel,
    int Version,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int RowVersion);

public sealed record CommunicationTemplateVersionView(
    CommunicationTemplateSummary Summary,
    string? Subject,
    string Body,
    IReadOnlyCollection<ConfigurationAuditEntry> Audit);

public sealed record CommunicationTemplateValidation(bool IsValid, IReadOnlyCollection<string> Errors);

public sealed record NotificationRuleDraft(
    string Name,
    string? Description,
    string EventType,
    int Priority,
    string? Condition,
    string RecipientDefinition,
    IReadOnlyCollection<CommunicationChannel> Channels,
    string? TemplateCode,
    string DeduplicationScope,
    bool Enabled,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record NotificationRuleSummary(
    long RuleId,
    string Code,
    string Name,
    bool Enabled,
    long? CurrentVersionId,
    int? CurrentVersion,
    string? Status,
    DateTimeOffset CreatedAt,
    int RowVersion);

public sealed record NotificationRuleVersionSummary(
    long VersionId,
    long RuleId,
    int Version,
    string Status,
    string EventType,
    int Priority,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int RowVersion);

public sealed record NotificationRuleVersionView(
    NotificationRuleVersionSummary Summary,
    NotificationRuleDraft Rule,
    IReadOnlyCollection<ConfigurationAuditEntry> Audit);

public sealed record NotificationRuleValidation(bool IsValid, IReadOnlyCollection<string> Errors);

public sealed record ConfigurationAuditEntry(
    string Action,
    long ActorId,
    DateTimeOffset At,
    string? OldValue,
    string? NewValue,
    string? Reason,
    string? CorrelationId);

public interface ICommunicationAdministrationRepository
{
    Task<CommunicationAdminConfiguration> GetConfigurationAsync(long actor, CancellationToken ct);
    Task<CommunicationProviderConfiguration> UpdateProviderAsync(long actor, string providerCode, CommunicationChannel channel, CommunicationProviderConfigurationRequest request, string? correlationId, CancellationToken ct);
    Task<CommunicationChannelConfiguration> UpdateChannelAsync(long actor, CommunicationChannel channel, CommunicationChannelConfigurationRequest request, string? correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<CommunicationTemplateSummary>> ListTemplatesAsync(long actor, string? code, CancellationToken ct);
    Task<CommunicationTemplateVersionView> GetTemplateVersionAsync(long actor, long versionId, CancellationToken ct);
    Task<long> SaveTemplateDraftAsync(long actor, CommunicationTemplateDraft draft, string idempotencyKey, int? expectedRowVersion, string? correlationId, CancellationToken ct);
    Task<CommunicationTemplateValidation> ValidateTemplateAsync(long actor, CommunicationTemplateDraft draft, CancellationToken ct);
    Task PublishTemplateAsync(long actor, long versionId, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task RetireTemplateAsync(long actor, long versionId, string idempotencyKey, string? reason, string? correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationRuleSummary>> ListNotificationRulesAsync(long actor, CancellationToken ct);
    Task<NotificationRuleSummary> CreateNotificationRuleAsync(long actor, string code, NotificationRuleDraft draft, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<NotificationRuleVersionSummary>> ListNotificationVersionsAsync(long actor, long ruleId, CancellationToken ct);
    Task<NotificationRuleVersionView> GetNotificationVersionAsync(long actor, long versionId, CancellationToken ct);
    Task<long> SaveNotificationDraftAsync(long actor, long ruleId, NotificationRuleDraft draft, string idempotencyKey, int? expectedRowVersion, string? correlationId, CancellationToken ct);
    Task<NotificationRuleValidation> ValidateNotificationDraftAsync(long actor, long ruleId, NotificationRuleDraft draft, CancellationToken ct);
    Task PublishNotificationVersionAsync(long actor, long versionId, string idempotencyKey, string? correlationId, CancellationToken ct);
    Task RetireNotificationVersionAsync(long actor, long versionId, string idempotencyKey, string? reason, string? correlationId, CancellationToken ct);
    Task<NotificationRuleSummary> SetNotificationRuleEnabledAsync(long actor, long ruleId, bool enabled, int expectedRowVersion, string? reason, string? correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<string>> GetSupportedEventTypesAsync(long actor, CancellationToken ct);
    Task<IReadOnlyCollection<ConfigurationAuditEntry>> GetConfigurationAuditAsync(long actor, string entityType, string entityId, CancellationToken ct);
}
