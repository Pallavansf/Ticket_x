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

public sealed class PostgreSqlPhase5FoundationTests
{
    [Fact]
    public async Task Phase5_metadata_foundations_persist_and_enforce_postgresql_constraints()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ids = new List<long>();
        var baseId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(1, 500);
        var attachmentId = baseId + 10;
        var changeId = baseId + 20;
        try
        {
            long ticketId;
            long relatedTicketId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                var ticket = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Phase 5 foundation {Guid.NewGuid():N}", "metadata foundation", 1001, 1001), CancellationToken.None);
                var related = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Task, $"Phase 5 related {Guid.NewGuid():N}", "relation foundation", 1001, 1001), CancellationToken.None);
                ticketId = ticket.Id;
                relatedTicketId = related.Id;
                ids.Add(ticketId);
                ids.Add(relatedTicketId);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var communication = scope.ServiceProvider.GetRequiredService<ICommunicationService>();
                var communicationKey = $"phase5-communication-{baseId}";
                var message = await communication.QueueAsync(new QueueCommunicationCommand(ticketId, null, CommunicationChannel.Email, "test", "PHASE5", "phase5@example.invalid", "Phase 5", "queued", null, null, "phase5-communication", communicationKey), 1001, CancellationToken.None);
                Assert.Equal(CommunicationStatus.Pending, message.Status);
                await Assert.ThrowsAsync<InvalidOperationException>(() => communication.QueueAsync(new QueueCommunicationCommand(ticketId, null, CommunicationChannel.Email, "test", "PHASE5", "phase5@example.invalid", "Phase 5", "queued", null, null, "phase5-communication", communicationKey), 1001, CancellationToken.None));

                var foundation = scope.ServiceProvider.GetRequiredService<IPhase5FoundationStore>();
                var now = DateTimeOffset.UtcNow;
                await foundation.AddNotificationAsync(new NotificationFoundation(baseId + 1, ticketId, null, 1001, "TICKET_CREATED", "IN_APP", "Created", "Ticket created", "PENDING", null, null, null, now, "phase5", "phase5-notification-1"), CancellationToken.None);
                await foundation.AddAttachmentAsync(new AttachmentFoundation(attachmentId, ticketId, null, "EVIDENCE", "evidence.txt", "text/plain", 12, "METADATA_ONLY", "pending://evidence", "aabbcc", "INTERNAL", "foundation attachment", 1, true, now, 1001), CancellationToken.None);
                await foundation.AddAttachmentVersionAsync(new AttachmentVersionFoundation(baseId + 11, attachmentId, 1, "evidence.txt", "text/plain", 12, "METADATA_ONLY", "pending://evidence", "aabbcc", "CURRENT", "initial", now, 1001), CancellationToken.None);
                await foundation.AddEvidenceAsync(new EvidenceFoundation(baseId + 12, ticketId, null, attachmentId, "EV-FOUNDATION", "TEST_RESULT", "Evidence metadata", "CTRL-1", "VALID", now, now.AddDays(30), now, 1001), CancellationToken.None);
                await foundation.AddProblemAsync(new ProblemFoundation(baseId + 13, ticketId, "Problem statement", "Root cause", "Workaround", "Permanent fix", "OPEN", now, null, null, 1001, now), CancellationToken.None);
                await foundation.AddChangeAsync(new ChangeFoundation(changeId, relatedTicketId, "Change reason", "Business impact", "Low risk", "Implementation", "Testing", "Rollback", "Verification", "DRAFT", now.AddDays(1), now.AddDays(1).AddHours(2), null, null, 1001, now, null, null), CancellationToken.None);
                await foundation.AddChangeImplementationAsync(new ChangeImplementationFoundation(baseId + 21, changeId, 1, "Implement", "Implementation step", "PENDING", null, null, null, null), CancellationToken.None);
                await foundation.AddEscalationAsync(new EscalationFoundation(baseId + 30, ticketId, null, "SLA", "Foundation escalation", null, 1001, 501, now, null, 1001), CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                await tickets.AddRelationshipAsync(ids[0], ids[1], "REQUEST_TASK", "Phase 5 relation", 1001, CancellationToken.None);
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.Single(await db.Communications.Where(x => x.TicketId == ids[0]).ToListAsync());
                Assert.Single(await db.Notifications.Where(x => x.TicketId == ids[0]).ToListAsync());
                Assert.Single(await db.Attachments.Where(x => x.Id == attachmentId).ToListAsync());
                Assert.Single(await db.AttachmentVersions.Where(x => x.AttachmentId == attachmentId && x.Status == "CURRENT").ToListAsync());
                Assert.Single(await db.Evidence.Where(x => x.TicketId == ids[0]).ToListAsync());
                Assert.Single(await db.Problems.Where(x => x.TicketId == ids[0]).ToListAsync());
                Assert.Single(await db.Changes.Where(x => x.Id == changeId).ToListAsync());
                Assert.Single(await db.ChangeImplementations.Where(x => x.ChangeId == changeId).ToListAsync());
                Assert.Single(await db.Escalations.Where(x => x.TicketId == ids[0]).ToListAsync());
                Assert.Single(await db.Relations.Where(x => x.TicketId == ids[0] && x.RelatedTicketId == ids[1]).ToListAsync());

                var foundation = scope.ServiceProvider.GetRequiredService<IPhase5FoundationStore>();
                await Assert.ThrowsAsync<DbUpdateException>(() => foundation.AddAttachmentVersionAsync(new AttachmentVersionFoundation(baseId + 14, attachmentId, 2, "evidence-v2.txt", "text/plain", 12, "METADATA_ONLY", "pending://evidence-v2", "ddeeff", "CURRENT", "duplicate current", DateTimeOffset.UtcNow, 1001), CancellationToken.None));
                await Assert.ThrowsAsync<DbUpdateException>(() => foundation.AddEvidenceAsync(new EvidenceFoundation(baseId + 15, ids[0], null, null, "EV-FK", "TEST", null, null, "VALID", null, null, DateTimeOffset.UtcNow, 1001), CancellationToken.None));
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                db.Notifications.Add(new NotificationRow { Id = baseId + 40, TicketId = ids[0], Type = "ROLLBACK", Channel = "IN_APP", Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
                await transaction.RollbackAsync();
                Assert.False(await db.Notifications.AnyAsync(x => x.Id == baseId + 40));
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            foreach (var id in ids)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({id})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_RELATION"" where ""N_TICKET_ID"" = {id} or ""N_RELATED_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ESCALATION"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVIDENCE"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_NOTIFICATION"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_COMMUNICATION"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_PROBLEM"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_CHANGE_IMPLEMENTATION"" where ""N_CHANGE_ID"" in (select ""N_CHANGE_ID"" from ""PA_TICKETS_CHANGE"" where ""N_TICKET_ID"" = {id})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_CHANGE"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ATTACHMENT_VERSION"" where ""N_ATTACHMENT_ID"" in (select ""N_ATTACHMENT_ID"" from ""PA_TICKETS_ATTACHMENT"" where ""N_TICKET_ID"" = {id})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ATTACHMENT"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {id}");
            }
        }
    }


    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL", ["Relationships:Allowed:0"] = "Request:Task:REQUEST_TASK" }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
