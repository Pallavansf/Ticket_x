using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlCommunicationAdministrationTests
{
    [Fact]
    public async Task Template_draft_publish_audit_and_auditor_read_access_work()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"T{Guid.NewGuid():N}".ToUpperInvariant();
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICommunicationAdministrationRepository>();
            var now = DateTimeOffset.UtcNow;
            var draft = new CommunicationTemplateDraft(code, CommunicationChannel.Email,
                "Ticket {{TicketNumber}}", "Hello {{TicketTitle}}", now.AddMinutes(-1), null);

            Assert.True((await repository.ValidateTemplateAsync(9001, draft, CancellationToken.None)).IsValid);
            var versionId = await repository.SaveTemplateDraftAsync(9001, draft, $"template-{Guid.NewGuid():N}", null,
                "communication-admin-test", CancellationToken.None);
            var saved = await repository.GetTemplateVersionAsync(9001, versionId, CancellationToken.None);
            Assert.Equal("DRAFT", saved.Summary.Status);

            await repository.PublishTemplateAsync(9001, versionId, $"publish-{Guid.NewGuid():N}",
                "communication-admin-test", CancellationToken.None);
            var published = await repository.GetTemplateVersionAsync(9001, versionId, CancellationToken.None);
            Assert.Equal("PUBLISHED", published.Summary.Status);
            Assert.Contains(published.Audit, entry => entry.Action == "COMMUNICATION_TEMPLATE_PUBLISHED");
            Assert.Contains(await repository.GetSupportedEventTypesAsync(9999, CancellationToken.None), eventType => eventType == "TICKET_CREATED");
            Assert.Contains(await repository.ListTemplatesAsync(9999, code, CancellationToken.None), template => template.VersionId == versionId);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SaveTemplateDraftAsync(9999, draft,
                $"auditor-write-{Guid.NewGuid():N}", null, "communication-admin-test", CancellationToken.None));
        }
        finally
        {
            await using var cleanup = provider.CreateAsyncScope();
            var db = cleanup.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.CommunicationTemplates.Where(row => row.Code == code).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Notification_rule_validation_publish_versioning_and_auditor_read_access_work()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"N{Guid.NewGuid():N}".ToUpperInvariant();
        var templateCode = $"T{Guid.NewGuid():N}".ToUpperInvariant();
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICommunicationAdministrationRepository>();
            var now = DateTimeOffset.UtcNow;
            var template = new CommunicationTemplateDraft(templateCode, CommunicationChannel.Email,
                "Ticket {{TicketNumber}}", "Hello {{TicketTitle}}", now.AddMinutes(-1), null);
            var templateId = await repository.SaveTemplateDraftAsync(9001, template, $"template-{Guid.NewGuid():N}", null,
                "notification-admin-test", CancellationToken.None);
            await repository.PublishTemplateAsync(9001, templateId, $"template-publish-{Guid.NewGuid():N}",
                "notification-admin-test", CancellationToken.None);

            var draft = new NotificationRuleDraft("New ticket email", null, "TICKET_CREATED", 10,
                "{\"field\":\"EventType\",\"op\":\"Equals\",\"value\":\"TICKET_CREATED\"}",
                "REQUESTER", [CommunicationChannel.Email], templateCode, "PER_EVENT", true, now.AddMinutes(-1), null);
            var created = await repository.CreateNotificationRuleAsync(9001, code, draft, $"rule-{Guid.NewGuid():N}",
                "notification-admin-test", CancellationToken.None);
            Assert.Equal("DRAFT", created.Status);
            var versionId = Assert.IsType<long>(created.CurrentVersionId);
            var invalid = draft with { Condition = "{\"field\":\"UnknownField\",\"op\":\"Execute\",\"value\":\"x\"}" };
            Assert.False((await repository.ValidateNotificationDraftAsync(9001, created.RuleId, invalid, CancellationToken.None)).IsValid);
            await repository.PublishNotificationVersionAsync(9001, versionId, $"rule-publish-{Guid.NewGuid():N}",
                "notification-admin-test", CancellationToken.None);
            var published = await repository.GetNotificationVersionAsync(9001, versionId, CancellationToken.None);
            Assert.Equal("PUBLISHED", published.Summary.Status);
            Assert.Contains(published.Audit, entry => entry.Action == "NOTIFICATION_RULE_VERSION_PUBLISHED");
            var supportedEventTypes = await repository.GetSupportedEventTypesAsync(9999, CancellationToken.None);
            Assert.Contains("WORKFLOW_INSTANCE_CREATED", supportedEventTypes);
            Assert.Contains("NOTIFICATION_MATCHED", supportedEventTypes);
            Assert.Contains("SLA_STARTED", supportedEventTypes);
            Assert.DoesNotContain("SLA_ESCALATE", supportedEventTypes);
            Assert.False((await repository.ValidateNotificationDraftAsync(9001, created.RuleId,
                draft with { EventType = "SLA_ESCALATE" }, CancellationToken.None)).IsValid);

            var changedDraft = draft with { Priority = 20 };
            var nextVersionId = await repository.SaveNotificationDraftAsync(9001, created.RuleId, changedDraft,
                $"rule-draft-{Guid.NewGuid():N}", null, "notification-admin-test", CancellationToken.None);
            var versions = await repository.ListNotificationVersionsAsync(9999, created.RuleId, CancellationToken.None);
            Assert.Equal(2, versions.Count);
            Assert.Equal("PUBLISHED", (await repository.GetNotificationVersionAsync(9999, versionId, CancellationToken.None)).Summary.Status);
            Assert.Equal("DRAFT", (await repository.GetNotificationVersionAsync(9001, nextVersionId, CancellationToken.None)).Summary.Status);
            Assert.Contains(await repository.ListNotificationRulesAsync(9999, CancellationToken.None), rule => rule.RuleId == created.RuleId);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SetNotificationRuleEnabledAsync(
                9999, created.RuleId, false, created.RowVersion, "auditor write", "notification-admin-test", CancellationToken.None));
        }
        finally
        {
            await using var cleanup = provider.CreateAsyncScope();
            var db = cleanup.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.NotificationRules.Where(row => row.Code == code).ExecuteDeleteAsync();
            await db.CommunicationTemplates.Where(row => row.Code == templateCode).ExecuteDeleteAsync();
        }
    }


    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddApplication();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
