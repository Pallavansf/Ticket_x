using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Domain.Tests;

[Collection("PostgreSQL operational metadata")]
public sealed class PostgreSqlOperationalMetadataTests
{
    private const long Administrator = 9001;
    private const long OperationsUser = 1001;
    private const long Auditor = 9999;

    [Fact]
    public async Task Status_configuration_crud_audit_chain_authorization_and_concurrency_work()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"S6_STATUS_{Guid.NewGuid():N}".ToUpperInvariant();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITicketStatusConfigurationRepository>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        Assert.True(await authorization.HasPermissionAsync(Administrator, "TICKET_ADMIN", null, CancellationToken.None));
        Assert.False(await authorization.HasPermissionAsync(OperationsUser, "TICKET_ADMIN", null, CancellationToken.None));
        Assert.False(await authorization.HasPermissionAsync(Auditor, "TICKET_ADMIN", null, CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repository.CreateStatusAsync(OperationsUser, code, "Slice 6", null, false, true, false, false, 100, null, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repository.CreateStatusAsync(Auditor, code, "Slice 6", null, false, true, false, false, 100, null, CancellationToken.None));

        var created = await repository.CreateStatusAsync(Administrator, code, "Slice 6", null, false, true, false, false, 100, "create test", CancellationToken.None);
        Assert.True(created.Active);
        var updated = await repository.UpdateStatusAsync(Administrator, created.Id, code, "Slice 6 updated", null, false, true, false, false, 101, "update test", CancellationToken.None, created.RowVersion);
        Assert.Equal(created.RowVersion + 1, updated.RowVersion);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.UpdateStatusAsync(Administrator, created.Id, code, "stale", null, false, true, false, false, 102, null, CancellationToken.None, created.RowVersion));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repository.SetStatusActiveAsync(Auditor, created.Id, false, null, CancellationToken.None));

        var audit = await repository.GetStatusAuditAsync(Auditor, created.Id, CancellationToken.None);
        Assert.Equal(2, audit.Count);
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var persisted = await db.Audits
            .AsNoTracking()
            .Where(x => x.EntityType == "STATUS_DEFINITION" && x.EntityId == created.Id.ToString())
            .OrderBy(x => x.Id)
            .ToArrayAsync();
        Assert.Equal(persisted[0].Hash, persisted[1].PreviousHash);
        Assert.Null(persisted[0].PreviousHash);
        Assert.All(persisted, x => Assert.True(VerifyHash(x, "STATUS_DEFINITION", created.Id.ToString())));
        Assert.True(VerifyHash(persisted[1], "STATUS_DEFINITION", created.Id.ToString()));
        AssertPersistedChainAndTamperOutcomes(persisted, "STATUS_DEFINITION", created.Id.ToString());
        Assert.Equal(persisted.Select(x => x.Hash), await db.Audits.AsNoTracking()
            .Where(x => x.EntityType == "STATUS_DEFINITION" && x.EntityId == created.Id.ToString())
            .OrderBy(x => x.Id)
            .Select(x => x.Hash)
            .ToArrayAsync());
    }

    [Fact]
    public async Task Transition_configuration_is_normalized_fail_closed_and_row_versioned()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITicketStatusConfigurationRepository>();
        Assert.True(await repository.CanTransitionAsync(OperationsUser, "NEW", "TRIAGE", CancellationToken.None));
        Assert.False(await repository.CanTransitionAsync(OperationsUser, "TRIAGE", "NEW", CancellationToken.None));
        Assert.True(await repository.CanTransitionAsync(OperationsUser, "new", "triage", CancellationToken.None));

        var changed = await repository.SetTransitionAsync(Administrator, "TRIAGE", "NEW", null, true, "add transition", CancellationToken.None);
        Assert.True(changed.Allowed);
        Assert.True(changed.RowVersion > 0);
        Assert.True(await repository.CanTransitionAsync(OperationsUser, "TRIAGE", "NEW", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SetTransitionAsync(Administrator, "TRIAGE", "NEW", null, false, "stale update", CancellationToken.None, changed.RowVersion - 1));
        var denied = await repository.SetTransitionAsync(Administrator, "TRIAGE", "NEW", null, false, "deny transition", CancellationToken.None, changed.RowVersion);
        Assert.False(denied.Allowed);
        Assert.False(await repository.CanTransitionAsync(OperationsUser, "TRIAGE", "NEW", CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repository.SetTransitionAsync(Auditor, "TRIAGE", "NEW", null, true, null, CancellationToken.None));

    }

    [Fact]
    public async Task Priority_defaults_inactive_selection_history_and_audit_chain_work()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"S6P_{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        await using var scope = provider.CreateAsyncScope();
        var priorities = scope.ServiceProvider.GetRequiredService<ITicketPriorityConfigurationRepository>();
        var created = await priorities.CreatePriorityAsync(Administrator, code, "Slice 6 test", null, 7, 90, false, "create", CancellationToken.None);
        var selected = await priorities.ResolvePriorityForSelectionAsync(OperationsUser, null, CancellationToken.None);
        Assert.Equal("MEDIUM", selected.Code);

        var defaultPriority = await priorities.SetDefaultPriorityAsync(Administrator, created.Id, "select default", CancellationToken.None);
        Assert.True(defaultPriority.IsDefault);
        Assert.Equal(created.Id, (await priorities.ResolvePriorityForSelectionAsync(OperationsUser, null, CancellationToken.None)).Id);

        var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
        var ticket = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Slice6 priority {Guid.NewGuid():N}", "configured default", OperationsUser, OperationsUser), CancellationToken.None);
        Assert.Equal(created.Id, ticket.PriorityId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            priorities.UpdatePriorityAsync(Administrator, created.Id, code, "changed level", null, 8, 90, false, null, CancellationToken.None, defaultPriority.RowVersion));
        await priorities.SetDefaultPriorityAsync(Administrator, 2, "restore default", CancellationToken.None);
        await priorities.SetPriorityActiveAsync(Administrator, created.Id, false, "deactivate", CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            priorities.ResolvePriorityForSelectionAsync(OperationsUser, created.Id, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            priorities.SetPriorityActiveAsync(Auditor, created.Id, true, null, CancellationToken.None));

        var audit = await priorities.GetPriorityAuditAsync(Auditor, created.Id, CancellationToken.None);
        Assert.True(audit.Count >= 3);
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var persisted = await db.Audits
            .AsNoTracking()
            .Where(x => x.EntityType == "PRIORITY_DEFINITION" && x.EntityId == created.Id.ToString())
            .OrderBy(x => x.Id)
            .ToArrayAsync();
        Assert.All(persisted, x => Assert.True(VerifyHash(x, "PRIORITY_DEFINITION", created.Id.ToString())));
        Assert.Equal(persisted[^2].Hash, persisted[^1].PreviousHash);
        AssertPersistedChainAndTamperOutcomes(persisted, "PRIORITY_DEFINITION", created.Id.ToString());
        Assert.Equal(persisted.Select(x => x.Hash), await db.Audits.AsNoTracking()
            .Where(x => x.EntityType == "PRIORITY_DEFINITION" && x.EntityId == created.Id.ToString())
            .OrderBy(x => x.Id)
            .Select(x => x.Hash)
            .ToArrayAsync());
    }

    [Fact]
    public async Task Specialized_creation_uses_configured_priority_and_rejects_inactive_or_unknown_ids()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var priorityCode = $"CUSTOM_{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var ticketIds = new List<long>();
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var priorities = scope.ServiceProvider.GetRequiredService<ITicketPriorityConfigurationRepository>();
            var priority = await priorities.CreatePriorityAsync(Administrator, priorityCode, "Custom notification priority", null, 77, 77, false, "runtime test", CancellationToken.None);
            await priorities.SetPriorityActiveAsync(Administrator, priority.Id, false, "verify inactive rejection", CancellationToken.None);

            var incidents = scope.ServiceProvider.GetRequiredService<IIncidentService>();
            var problems = scope.ServiceProvider.GetRequiredService<IProblemService>();
            var changes = scope.ServiceProvider.GetRequiredService<IChangeService>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => incidents.CreateAsync(
                new CreateIncidentCommand("inactive incident", null, OperationsUser, null, null, null, priority.Id, IncidentImpact.High, IncidentUrgency.High, null),
                Administrator, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => problems.CreateAsync(
                new CreateProblemCommand("inactive problem", null, "statement", OperationsUser, PriorityId: priority.Id),
                Administrator, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => changes.CreateAsync(
                new CreateChangeCommand("inactive change", null, ChangeType.Standard, ChangeImpact.Low, ChangeRisk.Low, "reason", null,
                    "implementation", "validation", null, null, OperationsUser, PriorityId: priority.Id),
                Administrator, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => incidents.CreateAsync(
                new CreateIncidentCommand("unknown priority", null, OperationsUser, null, null, null, long.MaxValue, IncidentImpact.High, IncidentUrgency.High, null),
                Administrator, CancellationToken.None));

            await priorities.SetPriorityActiveAsync(Administrator, priority.Id, true, "activate for selection", CancellationToken.None);
            var incident = await incidents.CreateAsync(
                new CreateIncidentCommand("custom incident", null, OperationsUser, null, null, null, priority.Id, IncidentImpact.High, IncidentUrgency.High, null),
                Administrator, CancellationToken.None);
            ticketIds.Add(incident.TicketId);
            var problem = await problems.CreateAsync(
                new CreateProblemCommand("custom problem", null, "statement", OperationsUser, PriorityId: priority.Id),
                Administrator, CancellationToken.None);
            ticketIds.Add(problem.TicketId);
            var change = await changes.CreateAsync(
                new CreateChangeCommand("custom change", null, ChangeType.Standard, ChangeImpact.Low, ChangeRisk.Low, "reason", null,
                    "implementation", "validation", null, null, OperationsUser, PriorityId: priority.Id),
                Administrator, CancellationToken.None);
            ticketIds.Add(change.TicketId);
            var defaultProblem = await problems.CreateAsync(
                new CreateProblemCommand("default problem", null, "statement", OperationsUser),
                Administrator, CancellationToken.None);
            ticketIds.Add(defaultProblem.TicketId);

            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            foreach (var ticketId in ticketIds.Take(3))
                Assert.Equal(priority.Id, await db.Tickets.AsNoTracking().Where(x => x.Id == ticketId).Select(x => x.PriorityId).SingleAsync());
            Assert.Equal(2, await db.Tickets.AsNoTracking().Where(x => x.Id == defaultProblem.TicketId).Select(x => x.PriorityId).SingleAsync());
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            foreach (var ticketId in ticketIds)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_INCIDENT\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_PROBLEM\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_CHANGE\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
            }
        }
    }

    [Fact]
    public async Task Incident_and_problem_status_changes_cannot_bypass_configured_transitions()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        long incidentId = 0;
        long problemId = 0;
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<ITicketStatusConfigurationRepository>();
        var prior = (await configuration.ListTransitionsAsync(Administrator, CancellationToken.None))
            .SingleOrDefault(x => x.FromCode == "NEW" && x.ToCode == "RESOLVED");
        try
        {
            await configuration.SetTransitionAsync(Administrator, "NEW", "RESOLVED", null, false,
                "specialized bypass regression", CancellationToken.None, prior?.RowVersion);
            var incidents = scope.ServiceProvider.GetRequiredService<IIncidentService>();
            incidentId = (await incidents.CreateAsync(
                new CreateIncidentCommand("transition guard incident", null, OperationsUser, null, null, null, null, IncidentImpact.High, IncidentUrgency.High, null),
                Administrator, CancellationToken.None)).TicketId;
            await Assert.ThrowsAsync<InvalidOperationException>(() => incidents.ResolveAsync(
                incidentId, null, null, "should not resolve", Administrator, CancellationToken.None));
            Assert.Equal(1, await db.Tickets.AsNoTracking().Where(x => x.Id == incidentId).Select(x => x.StatusId).SingleAsync());

            var problems = scope.ServiceProvider.GetRequiredService<IProblemService>();
            problemId = (await problems.CreateAsync(new CreateProblemCommand("transition guard problem", null, "statement", OperationsUser),
                Administrator, CancellationToken.None)).TicketId;
            await problems.StartInvestigationAsync(problemId, Administrator, CancellationToken.None);
            await problems.UpdateRootCauseAsync(problemId, "root cause", Administrator, CancellationToken.None);
            await problems.UpdatePermanentFixAsync(problemId, "permanent fix", Administrator, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => problems.ResolveAsync(problemId, "should not resolve", Administrator, CancellationToken.None));
            Assert.Equal(1, await db.Tickets.AsNoTracking().Where(x => x.Id == problemId).Select(x => x.StatusId).SingleAsync());
            Assert.Null(await db.Problems.AsNoTracking().Where(x => x.TicketId == problemId).Select(x => x.ResolvedAt).SingleAsync());
        }
        finally
        {
            if (prior is null)
                await db.Database.ExecuteSqlRawAsync("delete from \"PA_TICKETS_STATUS_TRANSITION\" where \"S_FROM_STATUS_CODE\" = 'NEW' and \"S_TO_STATUS_CODE\" = 'RESOLVED'");
            else
                await configuration.SetTransitionAsync(Administrator, prior.FromCode, prior.ToCode, prior.Condition,
                    prior.Allowed, "restore test configuration", CancellationToken.None);
            foreach (var ticketId in new[] { incidentId, problemId }.Where(x => x != 0))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_INCIDENT\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_PROBLEM\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
            }
        }
    }

    private static bool VerifyHash(AuditRow audit, string entityType, string entityId)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            entityType,
            entityId,
            action = audit.Action,
            actorUserId = audit.ActorId,
            auditAtUtc = audit.At.UtcDateTime,
            oldValue = audit.OldValue,
            newValue = audit.NewValue,
            reason = audit.Reason,
            previousHash = audit.PreviousHash
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return string.Equals(hash, audit.Hash, StringComparison.Ordinal);
    }

    private static void AssertPersistedChainAndTamperOutcomes(AuditRow[] persisted, string entityType, string entityId)
    {
        var originalHashes = persisted.Select(x => x.Hash).ToArray();
        var valid = AuditChainVerifier.Verify(persisted, entityType, entityId);
        Assert.True(valid.IsValid, $"{valid.Failure} at audit {valid.FailedAuditId}.");
        Assert.Equal(persisted.Length, valid.RecordsVerified);

        var payloadTamper = persisted.Select(Clone).ToArray();
        payloadTamper[0].Reason = "tampered payload";
        Assert.Equal("HASH_MISMATCH", AuditChainVerifier.Verify(payloadTamper, entityType, entityId).Failure);

        var hashTamper = persisted.Select(Clone).ToArray();
        hashTamper[0].Hash = new string('0', 64);
        Assert.Equal("HASH_MISMATCH", AuditChainVerifier.Verify(hashTamper, entityType, entityId).Failure);

        var predecessorTamper = persisted.Select(Clone).ToArray();
        predecessorTamper[^1].PreviousHash = new string('0', 64);
        Assert.Equal("PREDECESSOR_MISMATCH", AuditChainVerifier.Verify(predecessorTamper, entityType, entityId).Failure);

        var malformed = persisted.Select(Clone).ToArray();
        malformed[0].Hash = "not-a-hash";
        Assert.Equal("HASH_MISMATCH", AuditChainVerifier.Verify(malformed, entityType, entityId).Failure);
        Assert.Equal("NO_AUDIT_RECORDS", AuditChainVerifier.Verify([], entityType, entityId).Failure);

        Assert.Equal(originalHashes, persisted.Select(x => x.Hash));
    }

    private static AuditRow Clone(AuditRow row) => new()
    {
        Id = row.Id,
        EntityType = row.EntityType,
        EntityId = row.EntityId,
        Action = row.Action,
        ActorId = row.ActorId,
        At = row.At,
        OldValue = row.OldValue,
        NewValue = row.NewValue,
        Reason = row.Reason,
        Hash = row.Hash,
        PreviousHash = row.PreviousHash
    };


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
