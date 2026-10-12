using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class CommunicationAdministrationRepository(
    TicketsDbContext db,
    IAuthorizationService authorization,
    AuditChainCoordinator auditLocks,
    ProviderTransactionRetry retry,
    TimeProvider clock,
    IEnumerable<ICommunicationProvider> providers) : ICommunicationAdministrationRepository
{
    private static readonly Regex CodePattern = new("^[A-Z][A-Z0-9_]{1,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TemplateTokenPattern = new("\\{\\{[A-Za-z][A-Za-z0-9_]*\\}\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SecretAssignmentPattern = new(
        @"(?i)\b(password|passwd|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|authorization|bearer|private[_ -]?key|credential)\b\s*[:=]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ConditionFields = new(StringComparer.Ordinal)
    {
        "TicketId", "EventType", "Status", "Priority", "CurrentDepartmentId", "CurrentTeamId",
        "CurrentAssignee", "RequestedBy", "CorrelationId", "OPEN", "LOW", "TicketStatus",
        "TicketPriority", "StatusId", "PriorityId"
    };
    private static readonly HashSet<string> RecipientDefinitions = new(StringComparer.Ordinal)
    {
        "REQUESTER", "ASSIGNEE", "TEAM", "DEPARTMENT", "EXPLICIT_RECIPIENT"
    };
    private static readonly string[] SupportedEventTypes =
    [
        "APPROVAL_DECIDED", "APPROVAL_DELEGATED", "APPROVAL_INSTANCE_APPROVED", "APPROVAL_INSTANCE_CREATED",
        "APPROVAL_INSTANCE_REJECTED", "APPROVAL_LEVEL_ACTIVATED", "ATTACHMENT_CREATED", "ATTACHMENT_VERSION_CREATED",
        "CHANGE_ASSESSED", "CHANGE_CLOSED", "CHANGE_CREATED", "CHANGE_IMPLEMENTATION_COMPLETED",
        "CHANGE_IMPLEMENTATION_FAILED", "CHANGE_IMPLEMENTATION_STARTED", "CHANGE_REOPENED", "CHANGE_RESOLVED",
        "CHANGE_ROLLBACK_COMPLETED", "CHANGE_SCHEDULED", "CHANGE_VALIDATED", "COMMUNICATION_CANCELLED",
        "COMMUNICATION_DELIVERED", "COMMUNICATION_FAILED", "COMMUNICATION_QUEUED", "COMMUNICATION_RETRY_SCHEDULED",
        "COMMUNICATION_SENT", "EVIDENCE_CREATED", "EVIDENCE_REVIEWED", "INCIDENT_ACKNOWLEDGED",
        "INCIDENT_CLOSED", "INCIDENT_COMMANDER_ASSIGNED", "INCIDENT_CREATED", "INCIDENT_IMPACT_CHANGED",
        "INCIDENT_MAJOR_DECLARED", "INCIDENT_REOPENED", "INCIDENT_RESOLVED", "INCIDENT_RESTORED",
        "INCIDENT_URGENCY_CHANGED", "PROBLEM_ASSIGNED", "PROBLEM_CLOSED", "PROBLEM_CREATED",
        "PROBLEM_DECLARED_KNOWN_ERROR", "PROBLEM_INVESTIGATION_STARTED", "PROBLEM_PERMANENT_FIX_UPDATED",
        "PROBLEM_REOPENED", "PROBLEM_RESOLVED", "PROBLEM_ROOT_CAUSE_UPDATED", "PROBLEM_WORKAROUND_UPDATED",
        "NOTIFICATION_CREATED", "NOTIFICATION_MATCHED", "RELATION_CREATED", "SLA_BREACH", "SLA_COMPLETED",
        "SLA_ESCALATED", "SLA_PAUSED", "SLA_RESUMED", "SLA_STARTED", "SLA_UPDATED", "TICKET_CREATED", "TICKET_REOPENED",
        "WORKFLOW_INSTANCE_CREATED", "WORK_ITEM_ASSIGNED",
        "WORK_ITEM_COMPLETED", "WORK_ITEM_CREATED", "WORK_ITEM_REASSIGNED", "WORKFLOW_STEP_ACTIVATED",
        "WORKFLOW_STEP_COMPLETED", "WORKFLOW_STEP_REJECTED"
    ];
    private static readonly HashSet<string> AuditEntityTypes = new(StringComparer.Ordinal)
    {
        "COMMUNICATION_PROVIDER", "COMMUNICATION_CHANNEL", "COMMUNICATION_TEMPLATE",
        "NOTIFICATION_RULE", "NOTIFICATION_RULE_VERSION"
    };
    private readonly string[] supportedProviderCodes = providers
        .Select(x => x.Code)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public async Task<CommunicationAdminConfiguration> GetConfigurationAsync(long actor, CancellationToken ct)
    {
        await EnsureReadAsync(actor, ct);
        var providers = await db.CommunicationProviderConfigurations.AsNoTracking()
            .OrderBy(x => x.Channel).ThenBy(x => x.ProviderCode).ToListAsync(ct);
        var channels = await db.CommunicationChannelConfigurations.AsNoTracking()
            .OrderBy(x => x.Channel).ToListAsync(ct);
        return new(providers.Select(Map).ToArray(), channels.Select(Map).ToArray());
    }

    public Task<CommunicationProviderConfiguration> UpdateProviderAsync(
        long actor, string providerCode, CommunicationChannel channel,
        CommunicationProviderConfigurationRequest request, string? correlationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = RequiredCode(providerCode, "Provider code");
        EnsureSupportedProvider(code);
        EnsureSupportedChannel(channel);
        ValidateDates(request.EffectiveFrom, request.EffectiveTo);
        if (request.MaxAttempts is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(request), "Max attempts must be between 1 and 20.");
        if (request.RetryDelaySeconds is < 0 or > 86400) throw new ArgumentOutOfRangeException(nameof(request), "Retry delay must be between 0 and 86400 seconds.");

        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "COMMUNICATION_ADMIN", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var row = await db.CommunicationProviderConfigurations.SingleOrDefaultAsync(
                x => x.ProviderCode == code && x.Channel == channel.ToString().ToUpperInvariant(), ct);
            CheckExpectedRowVersion(row?.RowVersion, request.ExpectedRowVersion, "Provider configuration");
            var old = row is null ? null : ProviderAuditValue(row);
            if (row is null)
            {
                row = new CommunicationProviderConfigurationRow { ProviderCode = code, Channel = channel.ToString().ToUpperInvariant(), RowVersion = 1 };
                db.CommunicationProviderConfigurations.Add(row);
            }
            else row.RowVersion++;
            row.Enabled = request.Enabled;
            row.MaxAttempts = request.MaxAttempts;
            row.RetryDelaySeconds = request.RetryDelaySeconds;
            row.EffectiveFrom = request.EffectiveFrom;
            row.EffectiveTo = request.EffectiveTo;
            row.UpdatedAt = clock.GetUtcNow();
            row.UpdatedBy = actor;
            await db.SaveChangesAsync(ct);
            await AddAuditAsync("COMMUNICATION_PROVIDER", $"{code}:{channel}", "COMMUNICATION_PROVIDER_UPDATED", actor,
                old, ProviderAuditValue(row), request.Reason, correlationId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(row);
        }, ct);
    }

    public Task<CommunicationChannelConfiguration> UpdateChannelAsync(
        long actor, CommunicationChannel channel, CommunicationChannelConfigurationRequest request,
        string? correlationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupportedChannel(channel);
        ValidateDates(request.EffectiveFrom, request.EffectiveTo);
        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "COMMUNICATION_ADMIN", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var channelCode = channel.ToString().ToUpperInvariant();
            var row = await db.CommunicationChannelConfigurations.SingleOrDefaultAsync(x => x.Channel == channelCode, ct);
            CheckExpectedRowVersion(row?.RowVersion, request.ExpectedRowVersion, "Channel configuration");
            var old = row is null ? null : ChannelAuditValue(row);
            if (row is null)
            {
                row = new CommunicationChannelConfigurationRow { Channel = channelCode, RowVersion = 1 };
                db.CommunicationChannelConfigurations.Add(row);
            }
            else row.RowVersion++;
            row.Enabled = request.Enabled;
            row.EffectiveFrom = request.EffectiveFrom;
            row.EffectiveTo = request.EffectiveTo;
            row.UpdatedAt = clock.GetUtcNow();
            row.UpdatedBy = actor;
            await db.SaveChangesAsync(ct);
            await AddAuditAsync("COMMUNICATION_CHANNEL", channelCode, "COMMUNICATION_CHANNEL_UPDATED", actor,
                old, ChannelAuditValue(row), request.Reason, correlationId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(row);
        }, ct);
    }

    public async Task<IReadOnlyCollection<CommunicationTemplateSummary>> ListTemplatesAsync(long actor, string? code, CancellationToken ct)
    {
        await EnsureReadAsync(actor, ct);
        var query = db.CommunicationTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(code)) query = query.Where(x => x.Code == code.Trim().ToUpperInvariant());
        return (await query.OrderBy(x => x.Code).ThenBy(x => x.Channel).ThenByDescending(x => x.Version).ToListAsync(ct))
            .Select(Map).ToArray();
    }

    public async Task<CommunicationTemplateVersionView> GetTemplateVersionAsync(long actor, long versionId, CancellationToken ct)
    {
        await EnsureReadAsync(actor, ct);
        var row = await db.CommunicationTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, ct)
            ?? throw new KeyNotFoundException("Communication template version was not found.");
        return new(Map(row), row.Subject, row.Body, await LoadAuditAsync("COMMUNICATION_TEMPLATE", row.Id.ToString(), ct));
    }

    public Task<long> SaveTemplateDraftAsync(
        long actor, CommunicationTemplateDraft draft, string idempotencyKey, int? expectedRowVersion,
        string? correlationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireIdempotencyKey(idempotencyKey);
        var normalized = Normalize(draft);
        var validation = ValidateTemplateCore(normalized);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
        var fingerprint = Fingerprint(normalized);
        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "COMMUNICATION_ADMIN", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var prior = await FindIdempotencyAsync("COMM_TEMPLATE_DRAFT", actor, idempotencyKey, ct);
            if (prior is not null)
            {
                EnsureFingerprint(prior, fingerprint);
                var priorId = prior.ResultTicketId ?? throw new InvalidOperationException("The idempotent template draft result is unavailable.");
                return priorId;
            }

            var row = await db.CommunicationTemplates
                .Where(x => x.Code == normalized.Code && x.Channel == ChannelCode(normalized.Channel) && x.Status == "DRAFT")
                .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            CheckExpectedRowVersion(row?.RowVersion, expectedRowVersion, "Communication template draft");
            var created = row is null;
            var old = row is null ? null : TemplateAuditValue(row);
            if (row is null)
            {
                var version = await db.CommunicationTemplates.Where(x => x.Code == normalized.Code && x.Channel == ChannelCode(normalized.Channel))
                    .Select(x => (int?)x.Version).MaxAsync(ct) ?? 0;
                row = new CommunicationTemplateRow
                {
                    Code = normalized.Code,
                    Channel = ChannelCode(normalized.Channel),
                    Version = version + 1,
                    Status = "DRAFT",
                    CreatedAt = clock.GetUtcNow(),
                    CreatedBy = actor,
                    RowVersion = 1
                };
                db.CommunicationTemplates.Add(row);
            }
            else row.RowVersion++;
            row.Subject = normalized.Subject;
            row.Body = normalized.Body;
            row.EffectiveFrom = normalized.EffectiveFrom;
            row.EffectiveTo = normalized.EffectiveTo;
            row.Status = "DRAFT";
            await db.SaveChangesAsync(ct);
            await AddAuditAsync("COMMUNICATION_TEMPLATE", row.Id.ToString(),
                created ? "COMMUNICATION_TEMPLATE_DRAFT_CREATED" : "COMMUNICATION_TEMPLATE_DRAFT_UPDATED",
                actor, old, TemplateAuditValue(row), null, correlationId, ct);
            AddIdempotency("COMM_TEMPLATE_DRAFT", actor, idempotencyKey, fingerprint, row.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return row.Id;
        }, ct);
    }

    public async Task<CommunicationTemplateValidation> ValidateTemplateAsync(long actor, CommunicationTemplateDraft draft, CancellationToken ct)
    {
        await EnsureAdminAsync(actor, ct);
        return ValidateTemplateCore(Normalize(draft));
    }

    public async Task PublishTemplateAsync(long actor, long versionId, string idempotencyKey, string? correlationId, CancellationToken ct)
    {
        RequireIdempotencyKey(idempotencyKey);
        await retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "COMMUNICATION_PUBLISH", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var row = await db.CommunicationTemplates.SingleOrDefaultAsync(x => x.Id == versionId, ct)
                ?? throw new KeyNotFoundException("Communication template version was not found.");
            var fingerprint = Fingerprint(new { operation = "publish", versionId });
            var prior = await FindIdempotencyAsync("COMM_TEMPLATE_PUBLISH", actor, idempotencyKey, ct);
            if (prior is not null) { EnsureFingerprint(prior, fingerprint); await tx.CommitAsync(ct); return true; }
            if (row.Status != "DRAFT") throw new InvalidOperationException("Only a draft communication template can be published.");
            var validation = ValidateTemplateCore(new(row.Code, ParseChannel(row.Channel), row.Subject, row.Body, row.EffectiveFrom, row.EffectiveTo));
            if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
            var old = TemplateAuditValue(row);
            row.Status = "PUBLISHED";
            row.PublishedAt = clock.GetUtcNow();
            row.PublishedBy = actor;
            row.RowVersion++;
            await AddAuditAsync("COMMUNICATION_TEMPLATE", row.Id.ToString(), "COMMUNICATION_TEMPLATE_PUBLISHED", actor,
                old, TemplateAuditValue(row), null, correlationId, ct);
            AddIdempotency("COMM_TEMPLATE_PUBLISH", actor, idempotencyKey, fingerprint, row.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    public async Task RetireTemplateAsync(long actor, long versionId, string idempotencyKey, string? reason, string? correlationId, CancellationToken ct)
    {
        RequireIdempotencyKey(idempotencyKey);
        await retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "COMMUNICATION_RETIRE", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var fingerprint = Fingerprint(new { operation = "retire", versionId, reason });
            var prior = await FindIdempotencyAsync("COMM_TEMPLATE_RETIRE", actor, idempotencyKey, ct);
            if (prior is not null) { EnsureFingerprint(prior, fingerprint); await tx.CommitAsync(ct); return true; }
            var row = await db.CommunicationTemplates.SingleOrDefaultAsync(x => x.Id == versionId, ct)
                ?? throw new KeyNotFoundException("Communication template version was not found.");
            if (row.Status is not ("ACTIVE" or "PUBLISHED")) throw new InvalidOperationException("Only a published communication template can be retired.");
            var old = TemplateAuditValue(row);
            row.Status = "RETIRED";
            row.RetiredAt = clock.GetUtcNow();
            row.RetiredBy = actor;
            row.RowVersion++;
            await AddAuditAsync("COMMUNICATION_TEMPLATE", row.Id.ToString(), "COMMUNICATION_TEMPLATE_RETIRED", actor,
                old, TemplateAuditValue(row), reason, correlationId, ct);
            AddIdempotency("COMM_TEMPLATE_RETIRE", actor, idempotencyKey, fingerprint, row.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyCollection<NotificationRuleSummary>> ListNotificationRulesAsync(long actor, CancellationToken ct)
    {
        await EnsureNotificationReadAsync(actor, ct);
        var rules = await db.NotificationRules.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);
        var result = new List<NotificationRuleSummary>(rules.Count);
        foreach (var rule in rules)
        {
            var version = await db.NotificationRuleVersions.AsNoTracking().Where(x => x.RuleId == rule.Id)
                .OrderByDescending(x => x.VersionNo).FirstOrDefaultAsync(ct);
            result.Add(Map(rule, version));
        }
        return result;
    }

    public Task<NotificationRuleSummary> CreateNotificationRuleAsync(
        long actor, string code, NotificationRuleDraft draft, string idempotencyKey, string? correlationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireIdempotencyKey(idempotencyKey);
        var normalizedCode = RequiredCode(code, "Notification rule code");
        var normalized = Normalize(draft);
        var fingerprint = Fingerprint(new { normalizedCode, normalized });
        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "NOTIFICATION_ADMIN", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var validation = await ValidateNotificationCoreAsync(normalized, ct);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
            var prior = await FindIdempotencyAsync("NOTIFICATION_RULE_CREATE", actor, idempotencyKey, ct);
            if (prior is not null)
            {
                EnsureFingerprint(prior, fingerprint);
                var priorId = prior.ResultTicketId ?? await db.NotificationRules.Where(x => x.Code == normalizedCode).Select(x => x.Id).SingleAsync(ct);
                var existing = await db.NotificationRules.SingleAsync(x => x.Id == priorId, ct);
                var priorVersion = await db.NotificationRuleVersions.Where(x => x.RuleId == priorId).OrderByDescending(x => x.VersionNo).FirstOrDefaultAsync(ct);
                await tx.CommitAsync(ct);
                return Map(existing, priorVersion);
            }
            if (await db.NotificationRules.AnyAsync(x => x.Code == normalizedCode, ct))
                throw new InvalidOperationException("Notification rule code already exists.");
            var now = clock.GetUtcNow();
            var rule = new NotificationRuleRow
            {
                Code = normalizedCode,
                Name = normalized.Name,
                Description = normalized.Description,
                EventType = normalized.EventType,
                Enabled = normalized.Enabled,
                Priority = normalized.Priority,
                EffectiveFrom = normalized.EffectiveFrom,
                EffectiveTo = normalized.EffectiveTo,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1,
                RowVersion = 1
            };
            db.NotificationRules.Add(rule);
            await db.SaveChangesAsync(ct);
            var version = ToRuleVersion(rule.Id, 1, actor, normalized, now);
            db.NotificationRuleVersions.Add(version);
            await db.SaveChangesAsync(ct);
            await AddAuditAsync("NOTIFICATION_RULE", rule.Id.ToString(), "NOTIFICATION_RULE_CREATED", actor,
                null, RuleAuditValue(rule, version), null, correlationId, ct);
            await AddAuditAsync("NOTIFICATION_RULE_VERSION", version.Id.ToString(), "NOTIFICATION_RULE_DRAFT_CREATED", actor,
                null, RuleVersionAuditValue(version), null, correlationId, ct);
            AddIdempotency("NOTIFICATION_RULE_CREATE", actor, idempotencyKey, fingerprint, rule.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(rule, version);
        }, ct);
    }

    public async Task<IReadOnlyCollection<NotificationRuleVersionSummary>> ListNotificationVersionsAsync(long actor, long ruleId, CancellationToken ct)
    {
        await EnsureNotificationReadAsync(actor, ct);
        if (!await db.NotificationRules.AsNoTracking().AnyAsync(x => x.Id == ruleId, ct))
            throw new KeyNotFoundException("Notification rule was not found.");
        return (await db.NotificationRuleVersions.AsNoTracking().Where(x => x.RuleId == ruleId)
            .OrderByDescending(x => x.VersionNo).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<NotificationRuleVersionView> GetNotificationVersionAsync(long actor, long versionId, CancellationToken ct)
    {
        await EnsureNotificationReadAsync(actor, ct);
        var version = await db.NotificationRuleVersions.AsNoTracking().Include(x => x.Rule)
            .SingleOrDefaultAsync(x => x.Id == versionId, ct) ?? throw new KeyNotFoundException("Notification rule version was not found.");
        return new(Map(version), ToDraft(version), await LoadAuditAsync("NOTIFICATION_RULE_VERSION", version.Id.ToString(), ct));
    }

    public Task<long> SaveNotificationDraftAsync(
        long actor, long ruleId, NotificationRuleDraft draft, string idempotencyKey, int? expectedRowVersion,
        string? correlationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireIdempotencyKey(idempotencyKey);
        var normalized = Normalize(draft);
        var fingerprint = Fingerprint(new { ruleId, normalized, expectedRowVersion });
        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "NOTIFICATION_ADMIN", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var prior = await FindIdempotencyAsync("NOTIFICATION_RULE_DRAFT", actor, idempotencyKey, ct);
            if (prior is not null)
            {
                EnsureFingerprint(prior, fingerprint);
                return prior.ResultTicketId ?? throw new InvalidOperationException("The idempotent notification draft result is unavailable.");
            }
            var rule = await db.NotificationRules.SingleOrDefaultAsync(x => x.Id == ruleId, ct)
                ?? throw new KeyNotFoundException("Notification rule was not found.");
            var validation = await ValidateNotificationCoreAsync(normalized, ct);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
            var row = await db.NotificationRuleVersions.Where(x => x.RuleId == ruleId && x.Status == "DRAFT")
                .OrderByDescending(x => x.VersionNo).FirstOrDefaultAsync(ct);
            CheckExpectedRowVersion(row?.RowVersion, expectedRowVersion, "Notification rule draft");
            var created = row is null;
            var old = row is null ? null : RuleVersionAuditValue(row);
            var now = clock.GetUtcNow();
            if (row is null)
            {
                var versionNo = (await db.NotificationRuleVersions.Where(x => x.RuleId == ruleId)
                    .Select(x => (int?)x.VersionNo).MaxAsync(ct) ?? 0) + 1;
                row = ToRuleVersion(ruleId, versionNo, actor, normalized, now);
                db.NotificationRuleVersions.Add(row);
                rule.Version = versionNo;
            }
            else
            {
                row.RowVersion++;
                Copy(normalized, row);
                rule.Version = row.VersionNo;
            }
            rule.Name = normalized.Name;
            rule.Description = normalized.Description;
            rule.UpdatedAt = now;
            rule.RowVersion++;
            await db.SaveChangesAsync(ct);
            await AddAuditAsync("NOTIFICATION_RULE_VERSION", row.Id.ToString(),
                created ? "NOTIFICATION_RULE_DRAFT_CREATED" : "NOTIFICATION_RULE_DRAFT_UPDATED",
                actor, old, RuleVersionAuditValue(row), null, correlationId, ct);
            await AddAuditAsync("NOTIFICATION_RULE", rule.Id.ToString(), "NOTIFICATION_RULE_UPDATED", actor,
                null, RuleAuditValue(rule, row), null, correlationId, ct);
            AddIdempotency("NOTIFICATION_RULE_DRAFT", actor, idempotencyKey, fingerprint, row.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return row.Id;
        }, ct);
    }

    public async Task<NotificationRuleValidation> ValidateNotificationDraftAsync(long actor, long ruleId, NotificationRuleDraft draft, CancellationToken ct)
    {
        await EnsureAdminAsync(actor, ct);
        if (!await db.NotificationRules.AsNoTracking().AnyAsync(x => x.Id == ruleId, ct))
            return new(false, ["Notification rule was not found."]);
        return await ValidateNotificationCoreAsync(Normalize(draft), ct);
    }

    public Task PublishNotificationVersionAsync(long actor, long versionId, string idempotencyKey, string? correlationId, CancellationToken ct)
    {
        RequireIdempotencyKey(idempotencyKey);
        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "NOTIFICATION_PUBLISH", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var fingerprint = Fingerprint(new { operation = "publish", versionId });
            var prior = await FindIdempotencyAsync("NOTIFICATION_RULE_PUBLISH", actor, idempotencyKey, ct);
            if (prior is not null) { EnsureFingerprint(prior, fingerprint); await tx.CommitAsync(ct); return true; }
            var row = await db.NotificationRuleVersions.Include(x => x.Rule).SingleOrDefaultAsync(x => x.Id == versionId, ct)
                ?? throw new KeyNotFoundException("Notification rule version was not found.");
            if (row.Status != "DRAFT" || row.Rule is null) throw new InvalidOperationException("Only a draft notification rule version can be published.");
            var validation = await ValidateNotificationCoreAsync(ToDraft(row), ct);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
            var old = RuleVersionAuditValue(row);
            var now = clock.GetUtcNow();
            row.Status = "PUBLISHED";
            row.PublishedAt = now;
            row.PublishedBy = actor;
            row.RowVersion++;
            row.Rule.Version = row.VersionNo;
            row.Rule.EventType = row.EventType;
            row.Rule.Priority = row.Priority;
            row.Rule.Enabled = row.Enabled;
            row.Rule.EffectiveFrom = row.EffectiveFrom;
            row.Rule.EffectiveTo = row.EffectiveTo;
            row.Rule.UpdatedAt = now;
            row.Rule.RowVersion++;
            await AddAuditAsync("NOTIFICATION_RULE_VERSION", row.Id.ToString(), "NOTIFICATION_RULE_VERSION_PUBLISHED", actor,
                old, RuleVersionAuditValue(row), null, correlationId, ct);
            await AddAuditAsync("NOTIFICATION_RULE", row.RuleId.ToString(), "NOTIFICATION_RULE_VERSION_PUBLISHED", actor,
                null, RuleAuditValue(row.Rule, row), null, correlationId, ct);
            AddIdempotency("NOTIFICATION_RULE_PUBLISH", actor, idempotencyKey, fingerprint, row.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    public Task RetireNotificationVersionAsync(long actor, long versionId, string idempotencyKey, string? reason, string? correlationId, CancellationToken ct)
    {
        RequireIdempotencyKey(idempotencyKey);
        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "NOTIFICATION_RETIRE", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var fingerprint = Fingerprint(new { operation = "retire", versionId, reason });
            var prior = await FindIdempotencyAsync("NOTIFICATION_RULE_RETIRE", actor, idempotencyKey, ct);
            if (prior is not null) { EnsureFingerprint(prior, fingerprint); await tx.CommitAsync(ct); return true; }
            var row = await db.NotificationRuleVersions.Include(x => x.Rule).SingleOrDefaultAsync(x => x.Id == versionId, ct)
                ?? throw new KeyNotFoundException("Notification rule version was not found.");
            if (row.Status != "PUBLISHED") throw new InvalidOperationException("Only a published notification rule version can be retired.");
            var old = RuleVersionAuditValue(row);
            var now = clock.GetUtcNow();
            row.Status = "RETIRED";
            row.RetiredAt = now;
            row.RetiredBy = actor;
            row.RowVersion++;
            var hasNewerPublishedVersion = await db.NotificationRuleVersions.AnyAsync(x =>
                x.RuleId == row.RuleId && x.Id != row.Id && x.VersionNo > row.VersionNo
                && x.Status == "PUBLISHED" && x.Enabled
                && (x.EffectiveTo == null || x.EffectiveTo > now), ct);
            await AddAuditAsync("NOTIFICATION_RULE_VERSION", row.Id.ToString(), "NOTIFICATION_RULE_VERSION_RETIRED", actor,
                old, RuleVersionAuditValue(row), reason, correlationId, ct);
            if (!hasNewerPublishedVersion && row.Rule is { Enabled: true } rule)
            {
                var oldRule = RuleAuditValue(rule, null);
                rule.Enabled = false;
                rule.UpdatedAt = now;
                rule.RowVersion++;
                await AddAuditAsync("NOTIFICATION_RULE", rule.Id.ToString(), "NOTIFICATION_RULE_DISABLED_AFTER_VERSION_RETIREMENT", actor,
                    oldRule, RuleAuditValue(rule, null), reason, correlationId, ct);
            }
            AddIdempotency("NOTIFICATION_RULE_RETIRE", actor, idempotencyKey, fingerprint, row.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }, ct);
    }

    public Task<NotificationRuleSummary> SetNotificationRuleEnabledAsync(
        long actor, long ruleId, bool enabled, int expectedRowVersion, string? reason, string? correlationId, CancellationToken ct)
    {
        return retry.ExecuteAsync(db, async () =>
        {
            await authorization.EnsurePermissionAsync(actor, "NOTIFICATION_ADMIN", null, ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var rule = await db.NotificationRules.SingleOrDefaultAsync(x => x.Id == ruleId, ct)
                ?? throw new KeyNotFoundException("Notification rule was not found.");
            if (rule.RowVersion != expectedRowVersion)
                throw new InvalidOperationException("Notification rule changed since it was read; reload before updating.");
            var old = RuleAuditValue(rule, null);
            rule.Enabled = enabled;
            rule.UpdatedAt = clock.GetUtcNow();
            rule.RowVersion++;
            await AddAuditAsync("NOTIFICATION_RULE", rule.Id.ToString(),
                enabled ? "NOTIFICATION_RULE_ENABLED" : "NOTIFICATION_RULE_DISABLED", actor,
                old, RuleAuditValue(rule, null), reason, correlationId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var version = await db.NotificationRuleVersions.AsNoTracking().Where(x => x.RuleId == rule.Id)
                .OrderByDescending(x => x.VersionNo).FirstOrDefaultAsync(ct);
            return Map(rule, version);
        }, ct);
    }

    public async Task<IReadOnlyCollection<string>> GetSupportedEventTypesAsync(long actor, CancellationToken ct)
    {
        await EnsureNotificationReadAsync(actor, ct);
        return SupportedEventTypes;
    }

    public async Task<IReadOnlyCollection<ConfigurationAuditEntry>> GetConfigurationAuditAsync(
        long actor, string entityType, string entityId, CancellationToken ct)
    {
        var type = Required(entityType, "Audit entity type").ToUpperInvariant();
        var id = Required(entityId, "Audit entity id");
        if (!AuditEntityTypes.Contains(type)) throw new ArgumentException("Unsupported communication configuration audit entity type.");
        await authorization.EnsurePermissionAsync(actor,
            type.StartsWith("NOTIFICATION_", StringComparison.Ordinal) ? "NOTIFICATION_READ" : "COMMUNICATION_READ", null, ct);
        return await LoadAuditAsync(type, id, ct);
    }

    private async Task<NotificationRuleValidation> ValidateNotificationCoreAsync(NotificationRuleDraft draft, CancellationToken ct)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Name) || draft.Name.Trim().Length > 200) errors.Add("Rule name is required and must be at most 200 characters.");
        if (draft.Description?.Length > 2000) errors.Add("Rule description must be at most 2000 characters.");
        if (draft.Name?.Contains('\0') == true || draft.Description?.Contains('\0') == true) errors.Add("Rule text contains an invalid character.");
        if (SecretAssignmentPattern.IsMatch(draft.Name ?? "") || SecretAssignmentPattern.IsMatch(draft.Description ?? ""))
            errors.Add("Rule text must not contain credential-like assignments.");
        if (string.IsNullOrWhiteSpace(draft.EventType) || draft.EventType.Length > 100 || draft.EventType != draft.EventType.Trim()
            || !SupportedEventTypes.Contains(draft.EventType, StringComparer.Ordinal))
            errors.Add("Event type is not supported.");
        if (draft.Priority is < 0 or > 100000) errors.Add("Priority must be between 0 and 100000.");
        ValidateDatesInto(draft.EffectiveFrom, draft.EffectiveTo, errors, "Notification rule");
        if (string.IsNullOrWhiteSpace(draft.RecipientDefinition) || !RecipientDefinitions.Contains(draft.RecipientDefinition.Trim().ToUpperInvariant()))
            errors.Add("Recipient definition is not supported.");
        if (draft.Channels is null || draft.Channels.Count == 0) errors.Add("At least one communication channel is required.");
        else if (draft.Channels.Distinct().Count() != draft.Channels.Count) errors.Add("Communication channels must be unique.");
        else foreach (var channel in draft.Channels)
            if (!Enum.IsDefined(channel)) errors.Add($"Unsupported communication channel '{channel}'.");
        if (draft.DeduplicationScope != "PER_EVENT")
            errors.Add("Deduplication scope is not supported.");
        if (!string.IsNullOrWhiteSpace(draft.Condition) && SecretAssignmentPattern.IsMatch(draft.Condition))
            errors.Add("Notification condition must not contain credential-like assignments.");
        else if (!string.IsNullOrWhiteSpace(draft.Condition) && !ValidateCondition(draft.Condition, out var conditionError))
            errors.Add(conditionError);
        if (!string.IsNullOrWhiteSpace(draft.TemplateCode))
        {
            var templateCode = draft.TemplateCode.Trim().ToUpperInvariant();
            if (!CodePattern.IsMatch(templateCode)) errors.Add("Template code is invalid.");
            else if (draft.Channels is { Count: > 0 })
            {
                foreach (var channel in draft.Channels.Distinct())
                {
                    var found = await db.CommunicationTemplates.AsNoTracking().AnyAsync(x =>
                        x.Code == templateCode && x.Channel == ChannelCode(channel)
                        && (x.Status == "ACTIVE" || x.Status == "PUBLISHED")
                        && x.EffectiveFrom <= draft.EffectiveFrom
                        && ((!draft.EffectiveTo.HasValue && !x.EffectiveTo.HasValue)
                            || (draft.EffectiveTo.HasValue && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= draft.EffectiveTo.Value))), ct);
                    if (!found) errors.Add($"No published template '{templateCode}' exists for channel '{channel}'.");
                }
            }
        }
        return new(errors.Count == 0, errors);
    }

    private static CommunicationTemplateValidation ValidateTemplateCore(CommunicationTemplateDraft draft)
    {
        var errors = new List<string>();
        if (!CodePattern.IsMatch(draft.Code ?? "")) errors.Add("Template code must start with a letter and contain only letters, digits, or underscores.");
        if (!Enum.IsDefined(draft.Channel)) errors.Add("Communication channel is not supported.");
        if (string.IsNullOrWhiteSpace(draft.Body) || draft.Body.Length > 20000) errors.Add("Template body is required and must be at most 20000 characters.");
        if (draft.Subject?.Length > 1000) errors.Add("Template subject must be at most 1000 characters.");
        if (draft.Body?.Contains('\0') == true || draft.Subject?.Contains('\0') == true) errors.Add("Template text contains an invalid character.");
        if (SecretAssignmentPattern.IsMatch(draft.Body ?? "") || SecretAssignmentPattern.IsMatch(draft.Subject ?? ""))
            errors.Add("Template text must not contain credential-like assignments.");
        if (draft.EffectiveTo.HasValue && draft.EffectiveTo <= draft.EffectiveFrom) errors.Add("Effective To must be later than Effective From.");
        if (!string.IsNullOrEmpty(draft.Body) && !ValidTemplateTokens(draft.Body)) errors.Add("Template body contains an invalid or unclosed variable token.");
        if (!string.IsNullOrEmpty(draft.Subject) && !ValidTemplateTokens(draft.Subject)) errors.Add("Template subject contains an invalid or unclosed variable token.");
        return new(errors.Count == 0, errors);
    }

    private static bool ValidTemplateTokens(string value)
    {
        var stripped = TemplateTokenPattern.Replace(value, "");
        return !stripped.Contains("{{", StringComparison.Ordinal) && !stripped.Contains("}}", StringComparison.Ordinal);
    }

    private static bool ValidateCondition(string condition, out string error)
    {
        error = "Notification condition is not valid or uses unsupported fields/operators.";
        if (condition.Length > 4000) return false;
        try
        {
            using var document = JsonDocument.Parse(condition, new JsonDocumentOptions { MaxDepth = 12, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
            var nodeCount = 0;
            return ValidateConditionNode(document.RootElement, 0, ref nodeCount);
        }
        catch (JsonException) { return false; }
    }

    private static bool ValidateConditionNode(JsonElement node, int depth, ref int nodeCount)
    {
        if (++nodeCount > 100 || depth > 8 || node.ValueKind != JsonValueKind.Object) return false;
        var properties = node.EnumerateObject().ToArray();
        if (properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) return false;
        if (properties.Length == 1 && (properties[0].Name is "all" or "any") && properties[0].Value.ValueKind == JsonValueKind.Array)
        {
            var children = properties[0].Value.EnumerateArray().ToArray();
            if (children.Length == 0) return false;
            foreach (var child in children)
                if (!ValidateConditionNode(child, depth + 1, ref nodeCount)) return false;
            return true;
        }
        if (properties.Length != 3 || properties.Any(x => x.Name is not ("field" or "op" or "value"))) return false;
        if (!node.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String ||
            !ConditionFields.Contains(field.GetString() ?? "")) return false;
        if (!node.TryGetProperty("op", out var op) || op.ValueKind != JsonValueKind.String ||
            op.GetString() is not ("Equals" or "NotEquals" or "Present")) return false;
        if (!node.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String) return false;
        return (value.GetString()?.Length ?? 0) <= 500;
    }

    private async Task EnsureReadAsync(long actor, CancellationToken ct) =>
        await authorization.EnsurePermissionAsync(actor, "COMMUNICATION_READ", null, ct);

    private async Task EnsureNotificationReadAsync(long actor, CancellationToken ct) =>
        await authorization.EnsurePermissionAsync(actor, "NOTIFICATION_READ", null, ct);

    private async Task EnsureAdminAsync(long actor, CancellationToken ct) =>
        await authorization.EnsurePermissionAsync(actor, "NOTIFICATION_ADMIN", null, ct);

    private void EnsureSupportedProvider(string code)
    {
        if (!supportedProviderCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Provider is not registered by the communication runtime.");
    }

    private static void EnsureSupportedChannel(CommunicationChannel channel)
    {
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel), "Communication channel is not supported.");
    }

    private async Task<IdempotencyRow?> FindIdempotencyAsync(string scope, long actor, string key, CancellationToken ct) =>
        await db.Set<IdempotencyRow>().SingleOrDefaultAsync(x => x.Scope == scope && x.ActorId == actor && x.Key == key, ct);

    private void AddIdempotency(string scope, long actor, string key, string fingerprint, long resultId) =>
        db.Set<IdempotencyRow>().Add(new IdempotencyRow
        {
            Scope = scope, Key = key, ActorId = actor, Fingerprint = fingerprint, Status = "COMPLETED",
            CreatedAt = clock.GetUtcNow(), CompletedAt = clock.GetUtcNow(), ResultTicketId = resultId
        });

    private async Task AddAuditAsync(string entityType, string entityId, string action, long actor,
        string? oldValue, string? newValue, string? reason, string? correlationId, CancellationToken ct)
    {
        if (reason?.Length > 500 || reason?.Contains('\0') == true || (reason is not null && SecretAssignmentPattern.IsMatch(reason)))
            throw new ArgumentException("Audit reason is invalid or contains a secret-like value.");
        if (correlationId?.Length > 100 || correlationId?.Contains('\0') == true
            || (correlationId is not null && (!Regex.IsMatch(correlationId, "^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)
                || Regex.IsMatch(correlationId, @"^[A-Za-z0-9_-]{16,}\.[A-Za-z0-9_-]{16,}\.[A-Za-z0-9_-]{8,}$", RegexOptions.CultureInvariant))))
            throw new ArgumentException("Correlation id is invalid.");
        await using var tx = await AuditTransaction.BeginIfNeededAsync(db, ct);
        await auditLocks.AcquireAsync(db, entityType, entityId, ct);
        var at = clock.GetUtcNow();
        var previous = await db.Audits.Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.Id).Select(x => x.Hash).FirstOrDefaultAsync(ct);
        var auditNewValue = JsonSerializer.Serialize(new { value = newValue, correlationId });
        var canonical = JsonSerializer.Serialize(new
        {
            entityType, entityId, action, actorUserId = actor, auditAtUtc = at.UtcDateTime,
            oldValue, newValue = auditNewValue, reason, previousHash = previous
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        db.Audits.Add(new AuditRow
        {
            EntityType = entityType, EntityId = entityId, Action = action, ActorId = actor, At = at,
            OldValue = oldValue, NewValue = auditNewValue, Reason = reason, Hash = hash, PreviousHash = previous
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitIfOwnedAsync(ct);
    }

    private async Task<IReadOnlyCollection<ConfigurationAuditEntry>> LoadAuditAsync(string entityType, string entityId, CancellationToken ct)
    {
        var rows = await db.Audits.AsNoTracking().Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderBy(x => x.Id).ToListAsync(ct);
        return rows.Select(x =>
        {
            string? correlation = null;
            string? newValue = x.NewValue;
            if (!string.IsNullOrWhiteSpace(x.NewValue))
            {
                try
                {
                    using var json = JsonDocument.Parse(x.NewValue);
                    if (json.RootElement.TryGetProperty("value", out var value)) newValue = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                    if (json.RootElement.TryGetProperty("correlationId", out var corr)) correlation = corr.ValueKind == JsonValueKind.String ? corr.GetString() : null;
                }
                catch (JsonException) { }
            }
            return new ConfigurationAuditEntry(x.Action, x.ActorId, x.At, x.OldValue, newValue, x.Reason, correlation);
        }).ToArray();
    }

    private static string Fingerprint<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private static void EnsureFingerprint(IdempotencyRow row, string fingerprint)
    {
        if (!string.Equals(row.Fingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Idempotency key was reused for a different communication configuration.");
    }
    private static void RequireIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) throw new ArgumentException("A valid Idempotency-Key is required.");
    }
    private static string Required(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label} is required.");
        var result = value.Trim();
        if (result.Length > 500 || result.Contains('\0')) throw new ArgumentException($"{label} is invalid.");
        return result;
    }
    private static string RequiredCode(string value, string label)
    {
        var code = Required(value, label).ToUpperInvariant();
        if (!CodePattern.IsMatch(code)) throw new ArgumentException($"{label} is invalid.");
        return code;
    }
    private static string ChannelCode(CommunicationChannel channel) => channel.ToString().ToUpperInvariant();
    private static CommunicationChannel ParseChannel(string value) =>
        Enum.TryParse<CommunicationChannel>(value, true, out var channel) && Enum.IsDefined(channel)
            ? channel : throw new InvalidOperationException("Stored communication channel is unsupported.");
    private static void ValidateDates(DateTimeOffset from, DateTimeOffset? to)
    {
        if (to.HasValue && to.Value <= from) throw new ArgumentException("Effective To must be later than Effective From.");
    }
    private static void ValidateDatesInto(DateTimeOffset from, DateTimeOffset? to, ICollection<string> errors, string label)
    {
        if (to.HasValue && to.Value <= from) errors.Add($"{label} Effective To must be later than Effective From.");
    }
    private static void CheckExpectedRowVersion(int? current, int expected, string entity)
    {
        if (current.HasValue && expected != current.Value)
            throw new InvalidOperationException($"{entity} changed since it was read; reload before updating.");
        if (!current.HasValue && expected != 0)
            throw new InvalidOperationException($"A new {entity.ToLowerInvariant()} must use expected row version 0.");
    }
    private static void CheckExpectedRowVersion(int? current, int? expected, string entity)
    {
        if (current.HasValue && (!expected.HasValue || expected.Value != current.Value))
            throw new InvalidOperationException($"{entity} changed since it was read; reload before updating.");
        if (!current.HasValue && expected.HasValue)
            throw new InvalidOperationException($"A new {entity.ToLowerInvariant()} cannot specify an expected row version.");
    }

    private static CommunicationTemplateDraft Normalize(CommunicationTemplateDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return draft with { Code = draft.Code?.Trim().ToUpperInvariant() ?? "" };
    }
    private static NotificationRuleDraft Normalize(NotificationRuleDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var codeChannels = draft.Channels?.ToArray() ?? [];
        return draft with
        {
            Name = draft.Name?.Trim() ?? "",
            Description = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim(),
            EventType = draft.EventType?.Trim() ?? "",
            RecipientDefinition = draft.RecipientDefinition?.Trim().ToUpperInvariant() ?? "",
            Channels = codeChannels,
            TemplateCode = string.IsNullOrWhiteSpace(draft.TemplateCode) ? null : draft.TemplateCode.Trim().ToUpperInvariant(),
            DeduplicationScope = string.IsNullOrWhiteSpace(draft.DeduplicationScope) ? "PER_EVENT" : draft.DeduplicationScope.Trim().ToUpperInvariant()
        };
    }

    private static CommunicationProviderConfiguration Map(CommunicationProviderConfigurationRow row) =>
        new(row.ProviderCode, ParseChannel(row.Channel), row.Enabled, row.MaxAttempts, row.RetryDelaySeconds, row.EffectiveFrom, row.EffectiveTo, row.RowVersion);
    private static CommunicationChannelConfiguration Map(CommunicationChannelConfigurationRow row) =>
        new(ParseChannel(row.Channel), row.Enabled, row.EffectiveFrom, row.EffectiveTo, row.RowVersion);
    private static CommunicationTemplateSummary Map(CommunicationTemplateRow row) =>
        new(row.Id, row.Code, ParseChannel(row.Channel), row.Version, row.Status, row.EffectiveFrom, row.EffectiveTo, row.RowVersion);
    private static NotificationRuleVersionSummary Map(NotificationRuleVersionRow row) =>
        new(row.Id, row.RuleId, row.VersionNo, row.Status, row.EventType, row.Priority, row.EffectiveFrom, row.EffectiveTo, row.RowVersion);
    private static NotificationRuleSummary Map(NotificationRuleRow rule, NotificationRuleVersionRow? version) =>
        new(rule.Id, rule.Code, rule.Name, rule.Enabled, version?.Id, version?.VersionNo, version?.Status, rule.CreatedAt, rule.RowVersion);
    private static NotificationRuleDraft ToDraft(NotificationRuleVersionRow row) =>
        new(row.Rule?.Name ?? "", row.Rule?.Description, row.EventType, row.Priority, row.Condition,
            row.RecipientDefinition ?? "", ParseChannels(row.ChannelDefinition), row.TemplateCode,
            row.DeduplicationScope, row.Enabled, row.EffectiveFrom, row.EffectiveTo);
    private static IReadOnlyCollection<CommunicationChannel> ParseChannels(string? definition)
    {
        if (string.IsNullOrWhiteSpace(definition)) return [];
        try
        {
            var channels = JsonSerializer.Deserialize<string[]>(definition);
            if (channels is not null) return channels.Select(ParseChannel).ToArray();
        }
        catch (JsonException) { }
        return definition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ParseChannel).ToArray();
    }

    private static NotificationRuleVersionRow ToRuleVersion(long ruleId, int versionNo, long actor, NotificationRuleDraft draft, DateTimeOffset now) =>
        new()
        {
            RuleId = ruleId, VersionNo = versionNo, EventType = draft.EventType, Priority = draft.Priority,
            Condition = draft.Condition, RecipientDefinition = draft.RecipientDefinition,
            ChannelDefinition = JsonSerializer.Serialize(draft.Channels.Select(ChannelCode).ToArray()),
            TemplateCode = draft.TemplateCode, DeduplicationScope = draft.DeduplicationScope, Enabled = draft.Enabled,
            Status = "DRAFT", EffectiveFrom = draft.EffectiveFrom, EffectiveTo = draft.EffectiveTo,
            CreatedBy = actor, CreatedAt = now, RowVersion = 1
        };
    private static void Copy(NotificationRuleDraft draft, NotificationRuleVersionRow row)
    {
        row.EventType = draft.EventType;
        row.Priority = draft.Priority;
        row.Condition = draft.Condition;
        row.RecipientDefinition = draft.RecipientDefinition;
        row.ChannelDefinition = JsonSerializer.Serialize(draft.Channels.Select(ChannelCode).ToArray());
        row.TemplateCode = draft.TemplateCode;
        row.DeduplicationScope = draft.DeduplicationScope;
        row.Enabled = draft.Enabled;
        row.EffectiveFrom = draft.EffectiveFrom;
        row.EffectiveTo = draft.EffectiveTo;
        row.Status = "DRAFT";
    }

    private static string ProviderAuditValue(CommunicationProviderConfigurationRow row) =>
        JsonSerializer.Serialize(new { row.ProviderCode, row.Channel, row.Enabled, row.MaxAttempts, row.RetryDelaySeconds, row.EffectiveFrom, row.EffectiveTo, row.RowVersion });
    private static string ChannelAuditValue(CommunicationChannelConfigurationRow row) =>
        JsonSerializer.Serialize(new { row.Channel, row.Enabled, row.EffectiveFrom, row.EffectiveTo, row.RowVersion });
    private static string TemplateAuditValue(CommunicationTemplateRow row) =>
        JsonSerializer.Serialize(new
        {
            row.Code, row.Channel, row.Version, row.Status, row.EffectiveFrom, row.EffectiveTo, row.RowVersion,
            subjectHash = row.Subject is null ? null : Fingerprint(row.Subject),
            bodyHash = Fingerprint(row.Body)
        });
    private static string RuleAuditValue(NotificationRuleRow rule, NotificationRuleVersionRow? version) =>
        JsonSerializer.Serialize(new { rule.Code, rule.Name, rule.Enabled, rule.Version, currentVersion = version?.VersionNo, status = version?.Status, rule.RowVersion });
    private static string RuleVersionAuditValue(NotificationRuleVersionRow row) =>
        JsonSerializer.Serialize(new
        {
            row.RuleId, row.VersionNo, row.EventType, row.Priority, row.Status, row.EffectiveFrom, row.EffectiveTo, row.RowVersion,
            conditionHash = row.Condition is null ? null : Fingerprint(row.Condition),
            row.RecipientDefinition, row.ChannelDefinition, row.TemplateCode, row.DeduplicationScope
        });
}
