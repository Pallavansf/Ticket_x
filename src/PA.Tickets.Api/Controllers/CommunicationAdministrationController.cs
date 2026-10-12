using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Communications;

namespace PA.Tickets.Api.Controllers;

[ApiController, Authorize, Route("api/v1/communication-admin")]
[Asp.Versioning.ApiVersion("1.0")]
public sealed class CommunicationAdministrationController(ICommunicationAdministrationRepository repository) : ControllerBase
{
    [HttpGet("configuration")]
    public Task<CommunicationAdminConfiguration> Configuration(CancellationToken ct)
        => repository.GetConfigurationAsync(Actor(), ct);

    [HttpPut("providers/{providerCode}/{channel}")]
    public Task<CommunicationProviderConfiguration> UpdateProvider(string providerCode, string channel, CommunicationProviderConfigurationRequest request, CancellationToken ct)
        => repository.UpdateProviderAsync(Actor(), providerCode, ParseChannel(channel), request, CorrelationId(), ct);

    [HttpPut("channels/{channel}")]
    public Task<CommunicationChannelConfiguration> UpdateChannel(string channel, CommunicationChannelConfigurationRequest request, CancellationToken ct)
        => repository.UpdateChannelAsync(Actor(), ParseChannel(channel), request, CorrelationId(), ct);

    [HttpGet("templates")]
    public Task<IReadOnlyCollection<CommunicationTemplateSummary>> Templates([FromQuery] string? code, CancellationToken ct)
        => repository.ListTemplatesAsync(Actor(), code, ct);

    [HttpGet("templates/versions/{versionId:long}")]
    public Task<CommunicationTemplateVersionView> TemplateVersion(long versionId, CancellationToken ct)
        => repository.GetTemplateVersionAsync(Actor(), versionId, ct);

    [HttpPost("templates/validate")]
    public async Task<ActionResult<CommunicationTemplateValidation>> ValidateTemplate(CommunicationTemplateDraft draft, CancellationToken ct)
    {
        var result = await repository.ValidateTemplateAsync(Actor(), draft, ct);
        return result.IsValid ? Ok(result) : UnprocessableEntity(result);
    }

    [HttpPost("templates/drafts")]
    public Task<long> SaveTemplateDraft(CommunicationTemplateDraft draft, CancellationToken ct)
        => repository.SaveTemplateDraftAsync(Actor(), draft, IdempotencyKey(), ExpectedRowVersion(), CorrelationId(), ct);

    [HttpPost("templates/versions/{versionId:long}/publish")]
    public async Task<IActionResult> PublishTemplate(long versionId, CancellationToken ct)
    {
        await repository.PublishTemplateAsync(Actor(), versionId, IdempotencyKey(), CorrelationId(), ct);
        return NoContent();
    }

    [HttpPost("templates/versions/{versionId:long}/retire")]
    public async Task<IActionResult> RetireTemplate(long versionId, CommunicationRetireRequest request, CancellationToken ct)
    {
        await repository.RetireTemplateAsync(Actor(), versionId, IdempotencyKey(), request.Reason, CorrelationId(), ct);
        return NoContent();
    }

    [HttpGet("notification-rules")]
    public Task<IReadOnlyCollection<NotificationRuleSummary>> NotificationRules(CancellationToken ct)
        => repository.ListNotificationRulesAsync(Actor(), ct);

    [HttpPost("notification-rules")]
    public Task<NotificationRuleSummary> CreateNotificationRule(NotificationRuleCreateRequest request, CancellationToken ct)
        => repository.CreateNotificationRuleAsync(Actor(), request.Code, request.Draft, IdempotencyKey(), CorrelationId(), ct);

    [HttpGet("notification-rules/{ruleId:long}/versions")]
    public Task<IReadOnlyCollection<NotificationRuleVersionSummary>> NotificationVersions(long ruleId, CancellationToken ct)
        => repository.ListNotificationVersionsAsync(Actor(), ruleId, ct);

    [HttpGet("notification-versions/{versionId:long}")]
    public Task<NotificationRuleVersionView> NotificationVersion(long versionId, CancellationToken ct)
        => repository.GetNotificationVersionAsync(Actor(), versionId, ct);

    [HttpPost("notification-rules/{ruleId:long}/validate")]
    public async Task<ActionResult<NotificationRuleValidation>> ValidateNotification(long ruleId, NotificationRuleDraft draft, CancellationToken ct)
    {
        var result = await repository.ValidateNotificationDraftAsync(Actor(), ruleId, draft, ct);
        return result.IsValid ? Ok(result) : UnprocessableEntity(result);
    }

    [HttpPost("notification-rules/{ruleId:long}/drafts")]
    public Task<long> SaveNotificationDraft(long ruleId, NotificationRuleDraft draft, CancellationToken ct)
        => repository.SaveNotificationDraftAsync(Actor(), ruleId, draft, IdempotencyKey(), ExpectedRowVersion(), CorrelationId(), ct);

    [HttpPost("notification-versions/{versionId:long}/publish")]
    public async Task<IActionResult> PublishNotification(long versionId, CancellationToken ct)
    {
        await repository.PublishNotificationVersionAsync(Actor(), versionId, IdempotencyKey(), CorrelationId(), ct);
        return NoContent();
    }

    [HttpPost("notification-versions/{versionId:long}/retire")]
    public async Task<IActionResult> RetireNotification(long versionId, CommunicationRetireRequest request, CancellationToken ct)
    {
        await repository.RetireNotificationVersionAsync(Actor(), versionId, IdempotencyKey(), request.Reason, CorrelationId(), ct);
        return NoContent();
    }

    [HttpPut("notification-rules/{ruleId:long}/enabled")]
    public Task<NotificationRuleSummary> SetNotificationEnabled(long ruleId, NotificationRuleEnabledRequest request, CancellationToken ct)
        => repository.SetNotificationRuleEnabledAsync(Actor(), ruleId, request.Enabled, ExpectedRowVersion(request.ExpectedRowVersion), request.Reason, CorrelationId(), ct);

    [HttpGet("supported-event-types")]
    public Task<IReadOnlyCollection<string>> SupportedEvents(CancellationToken ct)
        => repository.GetSupportedEventTypesAsync(Actor(), ct);

    [HttpGet("audit/{entityType}/{entityId}")]
    public Task<IReadOnlyCollection<ConfigurationAuditEntry>> Audit(string entityType, string entityId, CancellationToken ct)
        => repository.GetConfigurationAuditAsync(Actor(), entityType, entityId, ct);

    private string CorrelationId() => HttpContext.TraceIdentifier;

    private string IdempotencyKey()
        => Request.Headers.TryGetValue("Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key)
            ? key.ToString()
            : throw new BadHttpRequestException("Idempotency-Key is required.");

    private int? ExpectedRowVersion()
        => Request.Headers.TryGetValue("If-Match", out var value) && int.TryParse(value.ToString().Trim('"'), out var version)
            ? version
            : null;

    private int ExpectedRowVersion(int bodyValue) => ExpectedRowVersion() ?? bodyValue;

    private long Actor()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) && id > 0 ? id : throw new UnauthorizedAccessException("The authenticated subject must map to a PA Tickets user.");
    }

    private static CommunicationChannel ParseChannel(string value)
        => Enum.TryParse<CommunicationChannel>(value, true, out var channel) && Enum.IsDefined(channel)
            ? channel
            : throw new ArgumentException("The communication channel is not supported.");
}

public sealed record CommunicationRetireRequest(string? Reason = null);
public sealed record NotificationRuleCreateRequest(string Code, NotificationRuleDraft Draft);
public sealed record NotificationRuleEnabledRequest(bool Enabled, int ExpectedRowVersion, string? Reason = null);
