using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlTicketCreationIdempotencyTests
{
    [Fact]
    public async Task First_create_persists_ticket_and_completes_idempotency()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();

        var key = $"idem-create-{Guid.NewGuid():N}";
        var cmd = new CreateTicketCommand(TicketType.Incident, "First Create", "Description", 9001, 9001, IdempotencyKey: key);

        var created = await tickets.CreateAsync(cmd, CancellationToken.None);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);

        var record = await db.Set<IdempotencyRow>().SingleOrDefaultAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == key);
        Assert.NotNull(record);
        Assert.Equal("COMPLETED", record.Status);
        Assert.Equal(created.Id, record.ResultTicketId);
        Assert.NotNull(record.Fingerprint);
        Assert.NotNull(record.CompletedAt);
    }

    [Fact]
    public async Task Same_key_retry_replays_exact_ticket_without_duplicate()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();

        var key = $"idem-retry-{Guid.NewGuid():N}";
        var cmd = new CreateTicketCommand(TicketType.Incident, "Retry Original", "Description", 9001, 9001, IdempotencyKey: key);

        var first = await tickets.CreateAsync(cmd, CancellationToken.None);
        var second = await tickets.CreateAsync(cmd, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Number, second.Number);
        Assert.Equal(1, await db.Tickets.CountAsync(x => x.Id == first.Id));
        Assert.Equal(1, await db.Set<IdempotencyRow>().CountAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == key));
    }

    [Fact]
    public async Task Same_key_different_payload_returns_conflict()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();

        var key = $"idem-conflict-{Guid.NewGuid():N}";
        var firstCmd = new CreateTicketCommand(TicketType.Incident, "Payload A", "Description A", 9001, 9001, IdempotencyKey: key);
        var secondCmd = new CreateTicketCommand(TicketType.Incident, "Payload B - Altered", "Description B", 9001, 9001, IdempotencyKey: key);

        await tickets.CreateAsync(firstCmd, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.CreateAsync(secondCmd, CancellationToken.None));
        Assert.Contains("different ticket request", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Concurrent_same_key_creates_exactly_one_logical_ticket()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var key = $"idem-race-{Guid.NewGuid():N}";
        var cmd = new CreateTicketCommand(TicketType.Incident, "Concurrent Race", "Description", 9001, 9001, IdempotencyKey: key);

        var tasks = Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var scope = provider.CreateAsyncScope();
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            try
            {
                var view = await tickets.CreateAsync(cmd, CancellationToken.None);
                return (Success: true, View: (TicketView?)view, Conflict: false);
            }
            catch (InvalidOperationException)
            {
                return (Success: false, View: null, Conflict: true);
            }
        });

        var results = await Task.WhenAll(tasks);
        var successes = results.Where(x => x.Success).ToList();
        var conflicts = results.Where(x => x.Conflict).ToList();

        // At least one must succeed; any that succeed concurrently must yield the identical ticket ID
        Assert.NotEmpty(successes);
        var distinctTicketIds = successes.Select(x => x.View!.Id).Distinct().ToList();
        Assert.Single(distinctTicketIds);

        await using var verifyScope = provider.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var totalTickets = await db.Tickets.CountAsync(x => x.Id == distinctTicketIds[0]);
        Assert.Equal(1, totalTickets);
    }

    [Fact]
    public async Task Failed_create_then_retry_recovers_and_creates_ticket()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var key = $"idem-fail-{Guid.NewGuid():N}";
        long requestTypeId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            var svc = await catalogue.CreateServiceAsync(9001, $"SVC-FAIL-{Guid.NewGuid():N}", "Service", null, null, null, now, null, CancellationToken.None);
            var cat = await catalogue.CreateCategoryAsync(9001, svc.Id, $"CAT-FAIL-{Guid.NewGuid():N}", "Category", null, now, null, CancellationToken.None);
            var rt = await catalogue.CreateRequestTypeAsync(9001, svc.Id, cat.Id, 1, $"REQ-FAIL-{Guid.NewGuid():N}", "Request", null, null, null, null, null, null, null, false, 0, 1, now, null, CancellationToken.None);
            requestTypeId = rt.Id;
        }

        var cmd = new CreateTicketCommand(TicketType.Incident, "Retry After Failure", "Description", 9001, 9001, RequestTypeId: requestTypeId, IdempotencyKey: key);

        // First attempt fails because the request type is draft (not published)
        await using (var scope = provider.CreateAsyncScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await Assert.ThrowsAnyAsync<Exception>(() => tickets.CreateAsync(cmd, CancellationToken.None));

            var record = await db.Set<IdempotencyRow>().SingleOrDefaultAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == key);
            Assert.NotNull(record);
            Assert.Equal("FAILED", record.Status);
        }

        // Publish request type to make it active and effective
        await using (var scope = provider.CreateAsyncScope())
        {
            var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
            await catalogue.PublishRequestTypeAsync(9001, requestTypeId, CancellationToken.None);
        }

        // Retry with the exact same command and key succeeds
        await using (var scope = provider.CreateAsyncScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var recovered = await tickets.CreateAsync(cmd, CancellationToken.None);
            Assert.NotNull(recovered);

            var record = await db.Set<IdempotencyRow>().SingleOrDefaultAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == key);
            Assert.NotNull(record);
            Assert.Equal("COMPLETED", record.Status);
            Assert.Equal(recovered.Id, record.ResultTicketId);
        }
    }

    [Fact]
    public async Task Failed_downstream_sla_start_resumes_creation_without_duplicate_work()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"IDEM-RUNTIME-{Guid.NewGuid():N}";
        long requestTypeId;
        long calendarId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
            var execution = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            var baseId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 10;
            var workflowId = baseId + 1;
            var workflowVersionId = baseId + 2;
            var workflowStepId = baseId + 3;
            var policyId = baseId + 10;
            var policyVersionId = baseId + 11;
            calendarId = baseId + 12;

            execution.WorkflowDefinitions.Add(new WorkflowDefinitionRow { Id = workflowId, Code = $"WF-{code}", Name = "Idem WF" });
            execution.WorkflowVersions.Add(new WorkflowVersionRow { Id = workflowVersionId, WorkflowId = workflowId, VersionNo = 1, Status = "PUBLISHED", Definition = $"{{\"effectiveFrom\":\"{now:O}\",\"effectiveTo\":null}}" });
            execution.WorkflowStepDefinitions.Add(new WorkflowStepDefinitionRow { Id = workflowStepId, VersionId = workflowVersionId, StepNo = 1, Code = "WORK", Name = "Work", Type = "WORK", ExecutionMode = "SEQUENTIAL" });
            execution.SlaCalendars.Add(new SlaCalendarRow { Id = calendarId, Code = $"CAL-{code}", Active = true, TimeZoneId = "UTC", WorkingHoursJson = "{\"Monday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Tuesday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Wednesday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Thursday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Friday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Saturday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Sunday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"}}", HolidaysJson = "[]" });
            execution.SlaPolicies.Add(new SlaPolicyRow { Id = policyVersionId, PolicyId = policyId, Code = $"POL-{code}", VersionNo = 1, Active = false, Status = "DRAFT", EffectiveFrom = now, CalendarCode = $"CAL-{code}", ResponseTargetMinutes = 60, ResolutionTargetMinutes = 120 });
            await execution.SaveChangesAsync();

            var service = await catalogue.CreateServiceAsync(9001, code, "Idem Service", null, null, null, now, null, CancellationToken.None);
            var category = await catalogue.CreateCategoryAsync(9001, service.Id, $"CAT-{code}", "Idem Category", null, now, null, CancellationToken.None);
            var rt = await catalogue.CreateRequestTypeAsync(9001, service.Id, category.Id, 1, $"REQ-{code}", "Idem Request", null, null, 501, 601, workflowId, policyId, 1, false, 0, 1, now, null, CancellationToken.None);
            await catalogue.PublishRequestTypeAsync(9001, rt.Id, CancellationToken.None);
            var policy = await execution.SlaPolicies.SingleAsync(x => x.Id == policyVersionId);
            policy.ServiceId = service.Id;
            policy.RequestTypeId = rt.Id;
            policy.Active = true;
            policy.Status = "PUBLISHED";
            await execution.SaveChangesAsync();
            requestTypeId = rt.Id;
        }

        var key = $"idem-downstream-{Guid.NewGuid():N}";
        var cmd = new CreateTicketCommand(TicketType.Incident, "Downstream Safety", "Desc", 9001, 9001, RequestTypeId: requestTypeId, IdempotencyKey: key);

        await using (var scope = provider.CreateAsyncScope())
        {
            var execution = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            await execution.SlaCalendars.Where(x => x.Id == calendarId).ExecuteDeleteAsync();
        }

        long persistedTicketId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => tickets.CreateAsync(cmd, CancellationToken.None));

            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var record = await db.Set<IdempotencyRow>().SingleAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == key);
            Assert.Equal("FAILED", record.Status);
            Assert.NotNull(record.ResultTicketId);
            persistedTicketId = record.ResultTicketId.Value;
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var execution = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            execution.SlaCalendars.Add(new SlaCalendarRow
            {
                Id = calendarId,
                Code = $"CAL-{code}",
                Active = true,
                TimeZoneId = "UTC",
                WorkingHoursJson = "{\"Monday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Tuesday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Wednesday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Thursday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Friday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Saturday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Sunday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"}}",
                HolidaysJson = "[]"
            });
            await execution.SaveChangesAsync();
        }

        TicketView retried;
        await using (var scope = provider.CreateAsyncScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            retried = await tickets.CreateAsync(cmd, CancellationToken.None);
        }

        Assert.Equal(persistedTicketId, retried.Id);

        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var execution = verifyScope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            var tickets = verifyScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var workflowCount = await execution.WorkflowInstances.CountAsync(x => x.TicketId == persistedTicketId);
            var slaCount = await execution.Slas.CountAsync(x => x.TicketId == persistedTicketId);
            var workItemCount = await execution.WorkItems.CountAsync(x => x.TicketId == persistedTicketId);
            var slaStartedEventCount = await execution.Events.CountAsync(x => x.TicketId == persistedTicketId && x.Type == "SLA_STARTED");
            var idempotencyRecord = await tickets.Set<IdempotencyRow>().SingleAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == key);

            Assert.Equal(1, workflowCount);
            Assert.Equal(1, slaCount);
            Assert.Equal(1, workItemCount);
            Assert.Equal(1, slaStartedEventCount);
            Assert.Equal("COMPLETED", idempotencyRecord.Status);
            Assert.Equal(persistedTicketId, idempotencyRecord.ResultTicketId);
        }
    }

    [Fact]
    public async Task Caller_isolation_allows_independent_keys_and_prevents_leakage_or_replay()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var sharedKey = $"shared-key-{Guid.NewGuid():N}";

        var cmdUserA = new CreateTicketCommand(TicketType.Incident, "User A Ticket", "Desc A", 9001, 9001, IdempotencyKey: sharedKey);
        var cmdUserB = new CreateTicketCommand(TicketType.Incident, "User B Ticket", "Desc B", 9002, 9002, IdempotencyKey: sharedKey);

        TicketView viewA;
        await using (var scope = provider.CreateAsyncScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            viewA = await tickets.CreateAsync(cmdUserA, CancellationToken.None);
        }

        TicketView viewB;
        await using (var scope = provider.CreateAsyncScope())
        {
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            // User B using the same key creates User B's own ticket independently without collision or leakage
            viewB = await tickets.CreateAsync(cmdUserB, CancellationToken.None);
        }

        Assert.NotEqual(viewA.Id, viewB.Id);

        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var records = await db.Set<IdempotencyRow>().Where(x => x.Scope == "TICKET_CREATE" && x.Key == sharedKey).ToListAsync();
            Assert.Equal(2, records.Count);
            Assert.Contains(records, r => r.ActorId == 9001 && r.ResultTicketId == viewA.Id);
            Assert.Contains(records, r => r.ActorId == 9002 && r.ResultTicketId == viewB.Id);
        }
    }

    [Fact]
    public async Task Ambiguous_expired_processing_record_without_ticket_fails_safely()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var fakeTime = new TestTimeProvider(DateTimeOffset.UtcNow);

        await using var provider = BuildProvider(fakeTime);
        var key = $"idem-ambiguous-{Guid.NewGuid():N}";

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            db.Set<IdempotencyRow>().Add(new IdempotencyRow
            {
                Scope = "TICKET_CREATE",
                Key = key,
                ActorId = 9001,
                Fingerprint = "test-fingerprint",
                Status = "PROCESSING",
                CreatedAt = fakeTime.GetUtcNow(),
                ResultTicketId = null
            });
            await db.SaveChangesAsync();
        }

        // Advance time past the 5-minute timeout window
        fakeTime.Advance(TimeSpan.FromMinutes(10));

        await using (var scope = provider.CreateAsyncScope())
        {
            var idempotencyStore = scope.ServiceProvider.GetRequiredService<ITicketCreationIdempotencyStore>();
            // Ambiguous state must fail safely with 409 Conflict (InvalidOperationException)
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                idempotencyStore.ClaimAsync(9001, key, "test-fingerprint", CancellationToken.None));
            Assert.Contains("ambiguous state", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Serialization_transient_failure_retries_transparently()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var key = $"idem-retry-transient-{Guid.NewGuid():N}";

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var retry = scope.ServiceProvider.GetRequiredService<ProviderTransactionRetry>();

        var attempts = 0;
        var result = await retry.ExecuteAsync(db, () =>
        {
            attempts++;
            if (attempts < 3)
            {
                // Simulate Npgsql transient serialization failure (SQLSTATE 40001)
                throw new Npgsql.PostgresException("could not serialize access due to read/write dependencies among transactions", "ERROR", "ERROR", "40001");
            }
            return Task.FromResult("SUCCESS");
        }, CancellationToken.None);

        Assert.Equal("SUCCESS", result);
        Assert.Equal(3, attempts);
    }


    private static ServiceProvider BuildProvider(TimeProvider? clock = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        if (clock is not null) services.AddSingleton(clock);
        services.AddApplication();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class TestTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _utcNow = initial;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
