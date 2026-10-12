using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlChangeTests
{
    [Fact]
    public async Task Change_creation_assessment_schedule_implementation_validation_and_close_are_persisted()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var changeId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var changes = scope.ServiceProvider.GetRequiredService<IChangeService>();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => changes.CreateAsync(new CreateChangeCommand(
                "Impersonated requester",
                null,
                ChangeType.Standard,
                ChangeImpact.Low,
                ChangeRisk.Low,
                "unauthorized requester substitution",
                null,
                "implementation plan",
                "validation plan",
                null,
                null,
                1002), 1001, CancellationToken.None));
            var created = await changes.CreateAsync(new CreateChangeCommand(
                "Rotate production certificates",
                "Controlled certificate rotation",
                ChangeType.Normal,
                ChangeImpact.High,
                ChangeRisk.High,
                "Certificate expiry is within the operational window",
                "Brief connection renegotiation",
                "Rotate certificates on the primary and standby nodes",
                "Validate new certificate chain and service health",
                "Restore the previous certificate bundle",
                "All health checks green for ten minutes",
                1001,
                ServiceId: 1,
                PriorityId: 4,
                CorrelationId: "change-create",
                IdempotencyKey: "change-create-1"), 1001, CancellationToken.None);
            changeId = created.TicketId;
            Assert.StartsWith("CHG-", created.Number);
            Assert.Equal(TicketType.Change, created.TicketType);
            Assert.Equal(ChangeType.Normal, created.ChangeType);
            Assert.Equal(ChangeRisk.High, created.Risk);
            Assert.Equal(ChangeStatus.Draft, created.Status);

            await Assert.ThrowsAsync<InvalidOperationException>(() => changes.CreateAsync(new CreateChangeCommand(
                "Duplicate", null, ChangeType.Normal, ChangeImpact.Low, ChangeRisk.Low, "duplicate", null, "plan", "validate", "rollback", "success", 1001, IdempotencyKey: "change-create-1"), 1001, CancellationToken.None));

            var assessed = await changes.AssessAsync(changeId, ChangeImpact.High, ChangeRisk.High, 1001, "risk reviewed", CancellationToken.None);
            Assert.Equal(ChangeStatus.Assessment, assessed.Status);
            var scheduled = await changes.ScheduleAsync(changeId, new ChangeScheduleCommand(DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(35), "approved window"), 1001, CancellationToken.None);
            Assert.Equal(ChangeStatus.Scheduled, scheduled.Status);
            var started = await changes.StartImplementationAsync(changeId, 1001, CancellationToken.None);
            Assert.Equal(ChangeStatus.Implementing, started.Status);
            var validating = await changes.CompleteImplementationAsync(changeId, new ChangeImplementationCommand("Rotation completed", "implementation complete"), 1001, CancellationToken.None);
            Assert.Equal(ChangeStatus.Validating, validating.Status);
            var resolved = await changes.ValidateAsync(changeId, new ChangeValidationCommand("Health checks passed", true), 1001, CancellationToken.None);
            Assert.Equal(ChangeStatus.Resolved, resolved.Status);
            var closed = await changes.CloseAsync(changeId, 1001, CancellationToken.None);
            Assert.Equal(ChangeStatus.Closed, closed.Status);

            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.True(await db.Changes.AnyAsync(x => x.TicketId == changeId && x.Status == "CLOSED" && x.RiskScore > 0));
            var changeExtensionId = await db.Changes.Where(x => x.TicketId == created.TicketId).Select(x => x.Id).SingleAsync();
            Assert.True(await db.ChangeImplementations.AnyAsync(x => x.ChangeId == changeExtensionId && x.Status == "COMPLETED"));
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == changeId && x.Type == "CHANGE_CREATED"));
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == changeId && x.Type == "CHANGE_VALIDATED"));
            Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "CHANGE" && x.EntityId == changeId.ToString() && x.Action == "CHANGE_CLOSED"));
        }
        finally
        {
            await CleanupAsync(provider, changeId);
        }
    }

    [Fact]
    public async Task Emergency_change_requires_rollback_plan_and_relationship_access_is_checked()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider(restricted: true);
        var changeId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var changes = scope.ServiceProvider.GetRequiredService<IChangeService>();
            await Assert.ThrowsAsync<ArgumentException>(() => changes.CreateAsync(new CreateChangeCommand("Emergency patch", null, ChangeType.Emergency, ChangeImpact.Critical, ChangeRisk.Critical, "Active exploit", null, "Patch immediately", "Verify service health", null, "No impact", 1001), 1001, CancellationToken.None));
            var created = await changes.CreateAsync(new CreateChangeCommand("Emergency patch", null, ChangeType.Emergency, ChangeImpact.Critical, ChangeRisk.Critical, "Active exploit", null, "Patch immediately", "Verify service health", "Restore previous release", "No impact", 1001, IdempotencyKey: "change-emergency-1"), 1001, CancellationToken.None);
            changeId = created.TicketId;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => changes.GetAsync(changeId, 2002, CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(provider, changeId);
        }
    }

    [Fact]
    public async Task Change_relationship_reads_require_access_to_related_tickets()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var accessState = new TicketAccessTestState();
        await using var provider = BuildProvider(accessState: accessState);
        var changeId = 0L;
        var relatedTicketId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var changes = scope.ServiceProvider.GetRequiredService<IChangeService>();
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            changeId = (await changes.CreateAsync(new CreateChangeCommand(
                "Scoped relationship change",
                null,
                ChangeType.Normal,
                ChangeImpact.Low,
                ChangeRisk.Low,
                "Test relationship read authorization",
                null,
                "Implement",
                "Validate",
                "Rollback",
                "Healthy",
                1001), 1001, CancellationToken.None)).TicketId;
            relatedTicketId = (await tickets.CreateAsync(new CreateTicketCommand(
                TicketType.Incident,
                "Restricted related incident",
                null,
                1001,
                1001), CancellationToken.None)).Id;

            await changes.RelateIncidentAsync(changeId, relatedTicketId, "related incident", 1001, CancellationToken.None);
            Assert.Single(await changes.GetRelationshipsAsync(changeId, 1001, CancellationToken.None));

            accessState.DeniedTicketId = relatedTicketId;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => changes.GetRelationshipsAsync(changeId, 1001, CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(provider, changeId);
            await CleanupAsync(provider, relatedTicketId);
        }
    }

    private static ServiceProvider BuildProvider(bool restricted = false, TicketAccessTestState? accessState = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "PostgreSQL",
            ["Relationships:Allowed:0"] = "Change:Incident:CHANGE_INCIDENT",
            ["Relationships:Allowed:1"] = "Change:Problem:CHANGE_PROBLEM"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        if (accessState is not null)
        {
            services.AddSingleton(accessState);
            services.AddScoped<ITicketAccessAuthorizer, RelatedTicketAuthorizer>();
        }
        else if (restricted) services.AddScoped<ITicketAccessAuthorizer, RestrictedAuthorizer>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class TicketAccessTestState
    {
        public long? DeniedTicketId { get; set; }
    }

    private sealed class RelatedTicketAuthorizer(TicketAccessTestState state) : ITicketAccessAuthorizer
    {
        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId, ticket);
        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId, ticket);
        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => actorUserId == 1001 ? Task.CompletedTask : throw new UnauthorizedAccessException("Ticket access denied.");
        private Task Ensure(long actorUserId, Ticket ticket) =>
            actorUserId == 1001 && ticket.Id != state.DeniedTicketId
                ? Task.CompletedTask
                : throw new UnauthorizedAccessException("Ticket access denied.");
    }

    private static async Task CleanupAsync(ServiceProvider provider, long ticketId)
    {
        if (ticketId == 0) return;
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_RELATION\" where \"N_TICKET_ID\" = {ticketId} or \"N_RELATED_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_CHANGE_IMPLEMENTATION\" where \"N_CHANGE_ID\" in (select \"N_CHANGE_ID\" from \"PA_TICKETS_CHANGE\" where \"N_TICKET_ID\" = {ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_CHANGE\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
    }

    private sealed class RestrictedAuthorizer : ITicketAccessAuthorizer
    {
        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId);
        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId);
        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => Ensure(actorUserId);
        private static Task Ensure(long actor) => actor == 1001 ? Task.CompletedTask : throw new UnauthorizedAccessException("Ticket access denied.");
    }
}
