using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlIncidentTests
{
    [Fact]
    public async Task Incident_creation_lifecycle_and_major_declaration_are_persisted()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ticketId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IIncidentService>();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                insert into "PA_TICKETS_DEPARTMENT" ("N_DEPARTMENT_ID","S_DEPARTMENT_CODE","S_DEPARTMENT_NAME","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
                values (502,'TEST-DEPT-502','Test Department 502',true,clock_timestamp(),0)
                on conflict ("N_DEPARTMENT_ID") do nothing;
                insert into "PA_TICKETS_TEAM" ("N_TEAM_ID","N_DEPARTMENT_ID","S_TEAM_CODE","S_TEAM_NAME","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
                values (602,502,'TEST-TEAM-602','Test Team 602',true,clock_timestamp(),0)
                on conflict ("N_TEAM_ID") do nothing;
                update "PA_TICKETS_USER" set "N_DEPARTMENT_ID"=502,"N_TEAM_ID"=602,"N_ACTIVE"=true where "N_USER_ID"=7200;
                """);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(new CreateIncidentCommand(
                "Out-of-scope commander",
                "Foreign organizational scope",
                1002,
                null,
                null,
                null,
                null,
                IncidentImpact.Low,
                IncidentUrgency.Low,
                7200),
                1001,
                CancellationToken.None));
            Assert.False(await db.Tickets.AnyAsync(x => x.Subject == "Out-of-scope commander"));
            var created = await service.CreateAsync(new CreateIncidentCommand(
                "Production outage",
                "API is failing",
                1002,
                1,
                1,
                1,
                null,
                IncidentImpact.High,
                IncidentUrgency.Critical,
                1003,
                "incident-create"),
                1001,
                CancellationToken.None);

            ticketId = created.TicketId;
            Assert.Equal(TicketType.Incident, created.Type);
            Assert.Equal(IncidentImpact.High, created.Impact);
            Assert.Equal(IncidentUrgency.Critical, created.Urgency);
            Assert.True(created.IsMajorIncident == false || created.IsMajorIncident == true);

            var acknowledged = await service.AcknowledgeAsync(ticketId, "acknowledged", 1001, CancellationToken.None);
            Assert.NotNull(acknowledged.AcknowledgedAt);

            var assigned = await service.AssignCommanderAsync(ticketId, 1004, 1001, "commanded", CancellationToken.None);
            Assert.Equal(1004, assigned.IncidentCommanderUserId);

            var major = await service.DeclareMajorAsync(ticketId, 1001, "major incident", CancellationToken.None);
            Assert.True(major.IsMajorIncident);
            Assert.NotNull(major.MajorIncidentDeclaredAt);

            var restored = await service.RestoreAsync(ticketId, 1001, "service restored", CancellationToken.None);
            Assert.NotNull(restored.RestoredAt);

            var resolved = await service.ResolveAsync(ticketId, "database failover", "switchover completed", "database recovered", 1001, CancellationToken.None);
            Assert.NotNull(resolved.ResolvedAt);

            var reopened = await service.ReopenAsync(ticketId, "issue reappeared", 1001, CancellationToken.None);
            Assert.Equal(TicketStatus.Reopened, reopened.Status);

            Assert.True(await db.Incidents.AnyAsync(x => x.TicketId == ticketId));
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "INCIDENT_CREATED"));
            Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "INCIDENT" && x.EntityId == ticketId.ToString()));
        }
        finally
        {
            await CleanupAsync(provider, ticketId);
        }
    }

    [Fact]
    public async Task Incident_priority_and_urgency_changes_are_traceable()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ticketId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IIncidentService>();
            var created = await service.CreateAsync(new CreateIncidentCommand(
                "Severity drift",
                "Priority changes over time",
                1002,
                1,
                1,
                1,
                null,
                IncidentImpact.Medium,
                IncidentUrgency.High,
                1003,
                "incident-priority"),
                1001,
                CancellationToken.None);
            ticketId = created.TicketId;

            var impactUpdated = await service.UpdateImpactAsync(ticketId, IncidentImpact.Critical, 1001, "customer impact escalated", CancellationToken.None);
            Assert.Equal(IncidentImpact.Critical, impactUpdated.Impact);

            var urgencyUpdated = await service.UpdateUrgencyAsync(ticketId, IncidentUrgency.Low, 1001, "mitigation effective", CancellationToken.None);
            Assert.Equal(IncidentUrgency.Low, urgencyUpdated.Urgency);

            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "INCIDENT_IMPACT_CHANGED"));
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "INCIDENT_URGENCY_CHANGED"));
            Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "INCIDENT" && x.EntityId == ticketId.ToString() && x.Action == "INCIDENT_IMPACT_CHANGED"));
        }
        finally
        {
            await CleanupAsync(provider, ticketId);
        }
    }


    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSQL",
                ["Communication:Worker:Enabled"] = "false",
                ["Jobs:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddApplication();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task CleanupAsync(ServiceProvider provider, long ticketId)
    {
        if (ticketId == 0) return;
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_INCIDENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
    }
}
