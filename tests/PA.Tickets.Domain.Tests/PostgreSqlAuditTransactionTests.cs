using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlAuditTransactionTests
{
    [Fact]
    public async Task Operational_audit_append_shares_callers_transaction_with_mutation()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        long ticketId = 0;

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var ticket = await service.CreateAsync(
                new CreateTicketCommand(TicketType.Request, $"Audit rollback {Guid.NewGuid():N}", "audit transaction regression", 1001, 1001),
                CancellationToken.None);
            ticketId = ticket.Id;

            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var auditWriter = scope.ServiceProvider.GetRequiredService<OperationalMetadataAuditWriter>();
            var ticketRow = await db.Tickets.SingleAsync(x => x.Id == ticketId);
            var originalSubject = ticketRow.Subject;
            var auditCount = await db.Audits.CountAsync(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString());
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                ticketRow.Subject = $"{originalSubject} rolled back";
                await auditWriter.AppendAsync(
                    "TICKET",
                    ticketId.ToString(),
                    "ROLLBACK_REGRESSION",
                    1001,
                    originalSubject,
                    ticketRow.Subject,
                    "transaction rollback",
                    CancellationToken.None);

                Assert.Equal(auditCount + 1, await db.Audits.CountAsync(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString()));
                Assert.Equal($"{originalSubject} rolled back", await db.Tickets.Where(x => x.Id == ticketId).Select(x => x.Subject).SingleAsync());
                await transaction.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            Assert.Equal(originalSubject, await db.Tickets.Where(x => x.Id == ticketId).Select(x => x.Subject).SingleAsync());
            Assert.Equal(auditCount, await db.Audits.CountAsync(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString()));
        }
        finally
        {
            await CleanupTicketAsync(provider, ticketId);
        }
    }

    [Fact]
    public async Task Concurrent_operational_audit_appends_preserve_one_ticket_chain()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        long ticketId = 0;

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                ticketId = (await service.CreateAsync(
                    new CreateTicketCommand(TicketType.Request, $"Audit concurrency {Guid.NewGuid():N}", "audit transaction regression", 1001, 1001),
                    CancellationToken.None)).Id;
            }

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writes = Enumerable.Range(0, 12).Select(async index =>
            {
                await using var scope = provider.CreateAsyncScope();
                var writer = scope.ServiceProvider.GetRequiredService<OperationalMetadataAuditWriter>();
                await start.Task;
                await writer.AppendAsync(
                    "TICKET",
                    ticketId.ToString(),
                    $"CONCURRENT_{index}",
                    1001,
                    null,
                    index.ToString(),
                    "concurrent audit regression",
                    CancellationToken.None);
            }).ToArray();
            start.SetResult();
            await Task.WhenAll(writes);

            await using var verifyScope = provider.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var rows = await db.Audits.AsNoTracking()
                .Where(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString())
                .OrderBy(x => x.Id)
                .ToArrayAsync();
            var result = AuditChainVerifier.Verify(rows, "TICKET", ticketId.ToString());
            Assert.True(result.IsValid, $"{result.Failure} at audit {result.FailedAuditId}.");
            Assert.Equal(13, result.RecordsVerified);
        }
        finally
        {
            await CleanupTicketAsync(provider, ticketId);
        }
    }

    private static async Task CleanupTicketAsync(ServiceProvider provider, long ticketId)
    {
        if (ticketId == 0)
            return;

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ASSIGNMENT"" where ""N_TICKET_ID"" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORK_ITEM"" where ""N_TICKET_ID"" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
