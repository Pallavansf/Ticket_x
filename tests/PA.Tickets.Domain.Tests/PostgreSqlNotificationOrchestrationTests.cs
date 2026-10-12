using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

[Collection("PostgreSQL operational metadata")]
public sealed class PostgreSqlNotificationOrchestrationTests
{
    [Fact]
    public async Task Notification_rule_versioning_condition_and_deduplication_work()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ticketId = 0L;
        var eventId = 0L;
        var ruleId = 0L;
        var ruleVersionId = 0L;
        var templateId = 0L;

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                var ticket = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Notification {Guid.NewGuid():N}", "notification orchestration", 1001, 1001), CancellationToken.None);
                ticketId = ticket.Id;

                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var now = DateTimeOffset.UtcNow;
                ruleId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 7000;
                ruleVersionId = ruleId + 1;

                templateId = ruleId + 100;
                var ticketPriorityCode = await db.PriorityDefinitions.AsNoTracking()
                    .Where(x => x.Id == ticket.PriorityId)
                    .Select(x => x.Code)
                    .SingleAsync();
                db.CommunicationTemplates.Add(new CommunicationTemplateRow
                {
                    Id = templateId,
                    Code = "TICKET_CREATED",
                    Channel = "EMAIL",
                    Version = 1,
                    Subject = "Ticket {{TicketNumber}} created",
                    Body = "Ticket {{TicketNumber}} is now {{Status}}.",
                    Status = "ACTIVE",
                    EffectiveFrom = now.AddMinutes(-30)
                });

                db.NotificationRules.Add(new NotificationRuleRow
                {
                    Id = ruleId,
                    Code = $"TICKET_CREATED_EMAIL_{ruleId}",
                    Name = "Ticket created email",
                    Description = "Email requester when ticket created",
                    EventType = "TICKET_CREATED",
                    Enabled = true,
                    Priority = 100,
                    EffectiveFrom = now.AddMinutes(-5),
                    EffectiveTo = null,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                db.NotificationRuleVersions.Add(new NotificationRuleVersionRow
                {
                    Id = ruleVersionId,
                    RuleId = ruleId,
                    VersionNo = 1,
                    Condition = $"{{\"all\":[{{\"field\":\"Status\",\"op\":\"Equals\",\"value\":\"OPEN\"}},{{\"field\":\"Priority\",\"op\":\"Equals\",\"value\":\"{ticketPriorityCode}\"}}]}}",
                    RecipientDefinition = "REQUESTER",
                    ChannelDefinition = "EMAIL",
                    TemplateCode = "TICKET_CREATED",
                    DeduplicationScope = "PER_EVENT",
                    Enabled = true,
                    EffectiveFrom = now.AddMinutes(-5),
                    CreatedBy = 1001,
                    CreatedAt = now
                });

                db.Events.Add(new EventRow
                {
                    TicketId = ticketId,
                    Type = "TICKET_CREATED",
                    Category = "TICKET",
                    ActorId = 1001,
                    NewValue = "OPEN",
                    Reason = "notification orchestration",
                    At = now
                });
                await db.SaveChangesAsync();
                eventId = await db.Events
                    .Where(x => x.TicketId == ticketId && x.Type == "TICKET_CREATED" && x.Reason == "notification orchestration")
                    .Select(x => x.Id)
                    .SingleAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var orchestrator = scope.ServiceProvider.GetRequiredService<INotificationOrchestrator>();
                var first = await orchestrator.ProcessAsync(new NotificationEventEnvelope(eventId, ticketId, null, "TICKET_CREATED", "ticket created", null, DateTimeOffset.UtcNow, "notification-correlation-1", null), 1001, CancellationToken.None);
                Assert.True(first.Matched);
                Assert.Single(first.Communications);
                Assert.Equal("EMAIL", first.Communications[0].Channel.ToString().ToUpperInvariant());
                Assert.Equal(1, first.CreatedCommunicationCount);

                var second = await orchestrator.ProcessAsync(new NotificationEventEnvelope(eventId, ticketId, null, "TICKET_CREATED", "ticket created", null, DateTimeOffset.UtcNow, "notification-correlation-2", null), 1001, CancellationToken.None);
                Assert.False(second.Matched);
                Assert.Equal("DUPLICATE", second.Status);

                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.Single(await db.NotificationExecutions.Where(x => x.EventId == eventId && x.RuleVersionId == ruleVersionId).ToListAsync());
                Assert.Equal(first.ExecutionId, await db.Communications
                    .Where(x => x.TicketId == ticketId && x.EventId == eventId)
                    .Select(x => x.NotificationExecutionId)
                    .SingleAsync());
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "NOTIFICATION_CREATED"));

                var customCode = $"CUSTOM_{Guid.NewGuid():N}"[..24].ToUpperInvariant();
                var customPriority = await scope.ServiceProvider.GetRequiredService<ITicketPriorityConfigurationRepository>()
                    .CreatePriorityAsync(9001, customCode, "Custom notification priority", null, 88, 88, false, "notification regression", CancellationToken.None);
                var ticketRow = await db.Tickets.SingleAsync(x => x.Id == ticketId);
                ticketRow.PriorityId = customPriority.Id;
                var notificationRule = await db.NotificationRuleVersions.SingleAsync(x => x.Id == ruleVersionId);
                notificationRule.Condition = $"{{\"field\":\"Priority\",\"op\":\"Equals\",\"value\":\"{customCode}\"}}";
                db.Events.Add(new EventRow
                {
                    TicketId = ticketId,
                    Type = "TICKET_CREATED",
                    Category = "TICKET",
                    ActorId = 1001,
                    NewValue = "OPEN",
                    At = DateTimeOffset.UtcNow,
                    Reason = "custom priority notification"
                });
                await db.SaveChangesAsync();
                var customEventId = await db.Events.Where(x => x.TicketId == ticketId && x.Reason == "custom priority notification")
                    .Select(x => x.Id).SingleAsync();
                var customMatch = await orchestrator.ProcessAsync(new NotificationEventEnvelope(customEventId, ticketId, null,
                    "TICKET_CREATED", "ticket created", null, DateTimeOffset.UtcNow, "custom-priority-notification", null), 1001, CancellationToken.None);
                Assert.True(customMatch.Matched);

                await Assert.ThrowsAsync<KeyNotFoundException>(() => scope.ServiceProvider.GetRequiredService<ITicketPriorityConfigurationRepository>()
                    .GetPriorityAsync(1001, long.MaxValue, CancellationToken.None));
            }
        }
        finally
        {
            if (ticketId != 0 || ruleId != 0)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_NOTIFICATION_EXECUTION\" where \"N_TICKET_ID\" = {ticketId} or \"N_RULE_ID\" = {ruleId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_NOTIFICATION_RULE_VERSION\" where \"N_RULE_ID\" = {ruleId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_NOTIFICATION_RULE\" where \"N_NOTIFICATION_RULE_ID\" = {ruleId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
                if (templateId != 0) await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION_TEMPLATE\" where \"N_TEMPLATE_VERSION_ID\" = {templateId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
            }
        }
    }

    [Fact]
    public async Task Notification_rule_disabled_and_condition_mismatch_create_skipped_execution()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ticketId = 0L;
        var eventId = 0L;
        var ruleId = 0L;
        var ruleVersionId = 0L;
        var templateId = 0L;

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                var ticket = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Notification skip {Guid.NewGuid():N}", "notification skip", 1001, 1001), CancellationToken.None);
                ticketId = ticket.Id;

                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var now = DateTimeOffset.UtcNow;
                ruleId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 8000;
                ruleVersionId = ruleId + 1;

                templateId = ruleId + 100;
                db.CommunicationTemplates.Add(new CommunicationTemplateRow
                {
                    Id = templateId,
                    Code = "TICKET_CREATED",
                    Channel = "EMAIL",
                    Version = 1,
                    Subject = "Ticket {{TicketNumber}} created",
                    Body = "Ticket {{TicketNumber}} is now {{Status}}.",
                    Status = "ACTIVE",
                    EffectiveFrom = now.AddMinutes(-30)
                });

                db.NotificationRules.Add(new NotificationRuleRow
                {
                    Id = ruleId,
                    Code = $"TICKET_CREATED_DISABLED_{ruleId}",
                    Name = "Disabled rule",
                    EventType = "TICKET_CREATED",
                    Enabled = false,
                    Priority = 50,
                    EffectiveFrom = now.AddMinutes(-10),
                    CreatedAt = now,
                    UpdatedAt = now
                });

                db.NotificationRuleVersions.Add(new NotificationRuleVersionRow
                {
                    Id = ruleVersionId,
                    RuleId = ruleId,
                    VersionNo = 1,
                    Condition = "{\"field\":\"Status\",\"op\":\"Equals\",\"value\":\"CLOSED\"}",
                    RecipientDefinition = "REQUESTER",
                    ChannelDefinition = "EMAIL",
                    TemplateCode = "TICKET_CREATED",
                    DeduplicationScope = "PER_EVENT",
                    Enabled = true,
                    EffectiveFrom = now.AddMinutes(-10),
                    CreatedBy = 1001,
                    CreatedAt = now
                });

                db.Events.Add(new EventRow { TicketId = ticketId, Type = "TICKET_CREATED", Category = "TICKET", ActorId = 1001, NewValue = "OPEN", At = now, Reason = "notification orchestration" });
                await db.SaveChangesAsync();
                eventId = await db.Events
                    .Where(x => x.TicketId == ticketId && x.Type == "TICKET_CREATED" && x.Reason == "notification orchestration")
                    .Select(x => x.Id)
                    .SingleAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var orchestrator = scope.ServiceProvider.GetRequiredService<INotificationOrchestrator>();
                var result = await orchestrator.ProcessAsync(new NotificationEventEnvelope(eventId, ticketId, null, "TICKET_CREATED", "ticket created", null, DateTimeOffset.UtcNow, "notification-skip", null), 1001, CancellationToken.None);
                Assert.False(result.Matched);
                Assert.Equal("SKIPPED", result.Status);
                Assert.Equal("RULE_DISABLED", result.SkipReason);

                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.Single(await db.NotificationExecutions.Where(x => x.EventId == eventId && x.RuleVersionId == ruleVersionId).ToListAsync());
            }
        }
        finally
        {
            if (ticketId != 0 || ruleId != 0)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_NOTIFICATION_EXECUTION\" where \"N_TICKET_ID\" = {ticketId} or \"N_RULE_ID\" = {ruleId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_NOTIFICATION_RULE_VERSION\" where \"N_RULE_ID\" = {ruleId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_NOTIFICATION_RULE\" where \"N_NOTIFICATION_RULE_ID\" = {ruleId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
                if (templateId != 0) await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION_TEMPLATE\" where \"N_TEMPLATE_VERSION_ID\" = {templateId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
            }
        }
    }


    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "PostgreSQL",
            ["Communication:Worker:Enabled"] = "false",
            ["Jobs:Enabled"] = "false"
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
