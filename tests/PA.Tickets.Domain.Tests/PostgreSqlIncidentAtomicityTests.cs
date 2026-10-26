using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Application;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Tickets;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlIncidentAtomicityTests
{
    [Fact]
    public async Task Incident_creation_rolls_back_base_ticket_after_injected_post_save_failure()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var subject = $"Atomicity rollback {Guid.NewGuid():N}";
        var key = $"incident-atomicity-rollback-{Guid.NewGuid():N}";
        var correlation = $"incident-atomicity-correlation-{Guid.NewGuid():N}";
        var interceptor = new ThrowAfterSaveInterceptor(2);
        await using var provider = BuildProvider(interceptor);
        await EnsureIncidentSchemaAsync(provider);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IIncidentService>();
            await Assert.ThrowsAsync<InjectedIncidentFailureException>(() => service.CreateAsync(
                new CreateIncidentCommand(
                    subject,
                    "Injected failure after base-ticket save",
                    1002,
                    null,
                    null,
                    null,
                    null,
                    IncidentImpact.High,
                    IncidentUrgency.High,
                    null,
                    key,
                    correlation),
                1001,
                CancellationToken.None));

            Assert.Equal(2, interceptor.SaveCallbacks);
            Assert.True(interceptor.SawActiveTransaction);

            await using var verifyScope = provider.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var ticketIds = await db.Tickets.Where(x => x.Subject == subject).Select(x => x.Id).ToArrayAsync();
            Assert.Empty(ticketIds);
            Assert.Empty(await db.Incidents.Where(x => x.IdempotencyKey == key).ToArrayAsync());
            Assert.Empty(await db.Events.Where(x => x.Reason == correlation).ToArrayAsync());
            Assert.Empty(await db.Audits.Where(x => x.Reason == correlation).ToArrayAsync());
        }
        finally
        {
            await CleanupAsync(provider, subject, key, correlation);
        }
    }

    [Fact]
    public async Task Incident_creation_persists_ticket_incident_event_history_and_audit_together()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var subject = $"Atomicity success {Guid.NewGuid():N}";
        var key = $"incident-atomicity-success-{Guid.NewGuid():N}";
        var correlation = $"incident-atomicity-success-correlation-{Guid.NewGuid():N}";
        await using var provider = BuildProvider();
        await EnsureIncidentSchemaAsync(provider);

        try
        {
            long ticketId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IIncidentService>();
                var created = await service.CreateAsync(
                    new CreateIncidentCommand(
                        subject,
                        "Successful atomicity case",
                        1002,
                        null,
                        null,
                        null,
                        null,
                        IncidentImpact.Medium,
                        IncidentUrgency.High,
                        null,
                        key,
                        correlation),
                    1001,
                    CancellationToken.None);
                ticketId = created.TicketId;
                Assert.Equal(subject, created.Subject);
            }

            await using (var verifyScope = provider.CreateAsyncScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.True(await db.Tickets.AnyAsync(x => x.Id == ticketId && x.Subject == subject));
                Assert.True(await db.Incidents.AnyAsync(x => x.TicketId == ticketId && x.IdempotencyKey == key));
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "INCIDENT_CREATED" && x.Reason == correlation));
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "TICKET_CREATED"));
                Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "INCIDENT" && x.EntityId == ticketId.ToString() && x.Action == "INCIDENT_CREATED" && x.Reason == correlation));
                Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString() && x.Action == "TICKET_CREATED"));
            }
        }
        finally
        {
            await CleanupAsync(provider, subject, key, correlation);
        }
    }

    private static ServiceProvider BuildProvider(ThrowAfterSaveInterceptor? interceptor = null)
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var connection = Environment.GetEnvironmentVariable("PA_TICKETS_TEST_CONNECTION_STRING")!;
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
        if (interceptor is not null)
        {
            services.RemoveAll<TicketsDbContext>();
            services.RemoveAll<DbContextOptions<TicketsDbContext>>();
            services.AddScoped(_ =>
            {
                var options = new DbContextOptionsBuilder<TicketsDbContext>()
                    .UseNpgsql(connection)
                    .AddInterceptors(interceptor)
                    .Options;
                return new TicketsDbContext(options);
            });
        }
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task CleanupAsync(ServiceProvider provider, string subject, string key, string correlation)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var ids = await db.Tickets.Where(x => x.Subject == subject).Select(x => x.Id).ToArrayAsync();
        foreach (var id in ids)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_INCIDENT\" where \"N_TICKET_ID\" = {id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"select pa_tickets_maintenance_delete_audit_for_ticket({id})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {id}");
        }
        await db.Set<IdempotencyRow>().Where(x => x.Key == key && x.ActorId == 1001).ExecuteDeleteAsync();
        await db.Events.Where(x => x.Reason == correlation).ExecuteDeleteAsync();
        await db.Audits.Where(x => x.Reason == correlation).ExecuteDeleteAsync();
    }

    private static async Task EnsureIncidentSchemaAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var table = await db.Database.SqlQueryRaw<string?>("select to_regclass('\"PA_TICKETS_INCIDENT\"')::text as \"Value\"").SingleAsync();
        if (table is null)
            throw new InvalidOperationException("Incident atomicity provider test requires PA_TICKETS_INCIDENT; no schema changes are authorized by this test.");
    }

    private sealed class ThrowAfterSaveInterceptor(int throwOnCallback) : SaveChangesInterceptor
    {
        public int SaveCallbacks { get; private set; }
        public bool SawActiveTransaction { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            SaveCallbacks++;
            if (SaveCallbacks == throwOnCallback)
            {
                SawActiveTransaction = eventData.Context?.Database.CurrentTransaction is not null;
                throw new InjectedIncidentFailureException();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class InjectedIncidentFailureException() : InvalidOperationException("Injected incident atomicity failure.");
}
