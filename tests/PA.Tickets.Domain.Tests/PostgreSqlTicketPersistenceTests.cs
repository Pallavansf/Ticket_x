using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;
using PA.Tickets.Infrastructure.Tickets;
using Xunit.Abstractions;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlTicketPersistenceTests
{
    private readonly ITestOutputHelper _output;

    public PostgreSqlTicketPersistenceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ticketId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(1, 999);
        var number = $"PERSIST-{ticketId}";
        var now = DateTimeOffset.UtcNow;
        var ticket = Ticket.Create(ticketId, number, TicketType.Request, "Repository persistence proof", "provider-backed repository test", 1001, 1001, now);
        var ticketPersisted = false;
        _output.WriteLine($"FixtureTicketId={ticketId}; FixtureTicketNumber={number}");

        // A collision must fail before writes; the cleanup below is enabled only
        // after this exact fixture has been successfully persisted.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var ticketIdText = ticketId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var identityOccupied = await db.Tickets.AsNoTracking().AnyAsync(x => x.Id == ticketId || x.Number == number)
                || await db.WorkItems.AsNoTracking().AnyAsync(x => x.TicketId == ticketId)
                || await db.Assignments.AsNoTracking().AnyAsync(x => x.TicketId == ticketId)
                || await db.Events.AsNoTracking().AnyAsync(x => x.TicketId == ticketId)
                || await db.Audits.AsNoTracking().AnyAsync(x => x.EntityType == "TICKET" && x.EntityId == ticketIdText)
                || await db.AuditChainLocks.AsNoTracking().AnyAsync(x => x.EntityType == "TICKET" && x.EntityId == ticketIdText);
            Assert.False(identityOccupied,
                "The generated fixture identity already exists; stop without cleanup or retry using a new explicit run.");
        }

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
                await repository.AddAsync(ticket, CancellationToken.None);
                ticketPersisted = true;
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
                var reloaded = await repository.GetAsync(ticketId, CancellationToken.None);
                Assert.NotNull(reloaded);
                Assert.Equal(number, reloaded!.Number);
                Assert.Equal("Repository persistence proof", reloaded.Subject);
                Assert.Equal(TicketType.Request, reloaded.Type);
                Assert.Equal(TicketStatus.New, reloaded.Status);
                Assert.Equal(1001, reloaded.RequestedBy);
                var audit = await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Audits
                    .AsNoTracking()
                    .Where(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString())
                    .ToArrayAsync();
                Assert.Single(audit);
                Assert.Equal("TICKET_CREATED", audit[0].Action);
                var history = await repository.GetHistoryAsync(ticketId, CancellationToken.None);
                Assert.Contains(history, entry => entry.Action == "TICKET_CREATED" && entry.NewValue == "New");
            }
        }
        finally
        {
            if (ticketPersisted)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await using var cleanup = await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ASSIGNMENT"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORK_ITEM"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_AUDIT_CHAIN_LOCK"" where ""S_ENTITY_TYPE"" = 'TICKET' and ""S_ENTITY_ID"" = {ticketId.ToString()}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
                await cleanup.CommitAsync();
            }
        }
    }

    [Fact]
    public async Task Ticket_lifecycle_service_persists_and_reloads_core_ticket_flow_in_postgresql()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();

        await using var provider = BuildProvider();
        var createdIds = new List<long>();
        LifecycleAuthorizationFixture? fixture = null;

        try
        {
            var classification = await RequireActiveClassificationReferenceAsync(provider);
            fixture = await CreateLifecycleAuthorizationFixtureAsync(provider);
            _output.WriteLine($"FixtureActorId={fixture.ActorId}; FixtureAssigneeId={fixture.AssigneeId}; FixtureDepartmentId={fixture.DepartmentId}; FixtureTeamId={fixture.TeamId}; FixtureRoleId={fixture.RoleId}");
            foreach (var type in Enum.GetValues<TicketType>())
            {
                await using var scope = provider.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                var created = await service.CreateAsync(new CreateTicketCommand(type, $"PostgreSQL E2E {type} {Guid.NewGuid():N}", "provider boundary regression", fixture.ActorId, fixture.ActorId), CancellationToken.None);
                createdIds.Add(created.Id);
                await AddExactTicketScopeAsync(scope.ServiceProvider.GetRequiredService<TicketsDbContext>(), fixture, created.Id);

                var classified = await service.ClassifyAsync(created.Id, classification.ServiceId, classification.CategoryId,
                    classification.RequestTypeId, 2, fixture.ActorId, CancellationToken.None);
                Assert.Equal(TicketStatus.Classified, classified.Status);
                Assert.StartsWith(Prefix(type), classified.Number, StringComparison.Ordinal);
                var auditRows = await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Audits
                    .AsNoTracking()
                    .Where(x => x.EntityType == "TICKET" && x.EntityId == created.Id.ToString())
                    .OrderBy(x => x.Id)
                    .ToArrayAsync();
                var integrity = AuditChainVerifier.Verify(auditRows, "TICKET", created.Id.ToString());
                Assert.True(integrity.IsValid, $"Ticket {created.Id}: {integrity.Failure} at audit {integrity.FailedAuditId}.");
            }

            long routedWorkItemId;
            {
                await using var scope = provider.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                var routed = await service.RouteAsync(createdIds[0], fixture.DepartmentId, fixture.TeamId, fixture.ActorId, CancellationToken.None);
                Assert.Equal(TicketStatus.Routed, routed.Status);
                var workItem = Assert.Single(routed.WorkItems);
                routedWorkItemId = workItem.Id;
                Assert.EndsWith("-WI-001", workItem.Number, StringComparison.Ordinal);
                var assigned = await service.ReassignAsync(createdIds[0], workItem.Id, fixture.AssigneeId, fixture.ActorId, "M1 exact-scope fixture", CancellationToken.None);
                Assert.Equal(fixture.AssigneeId, Assert.Single(assigned.WorkItems).AssignedToUserId);
            }

            {
                await using var scope = provider.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                var reloaded = await service.GetAsync(createdIds[0], fixture.ActorId, CancellationToken.None);
                Assert.Equal(TicketStatus.Routed, reloaded.Status);
                Assert.Equal(fixture.DepartmentId, reloaded.CurrentDepartmentId);
                Assert.Equal(fixture.TeamId, reloaded.CurrentTeamId);
                Assert.Contains(reloaded.WorkItems, x => x.Id == routedWorkItemId && x.Status == "ASSIGNED" && x.AssignedToUserId == fixture.AssigneeId);
            }
        }
        finally
        {
            if (fixture is not null)
            {
                await using var cleanupScope = provider.CreateAsyncScope();
                var db = cleanupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await using var cleanup = await db.Database.BeginTransactionAsync();
                foreach (var id in createdIds.Distinct())
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ASSIGNMENT"" where ""N_TICKET_ID"" = {id}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORK_ITEM"" where ""N_TICKET_ID"" = {id}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {id}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({id})");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_AUDIT_CHAIN_LOCK"" where ""S_ENTITY_TYPE"" = 'TICKET' and ""S_ENTITY_ID"" = {id.ToString()}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {id}");
                }
                await DeleteLifecycleAuthorizationFixtureAsync(db, fixture);
                await cleanup.CommitAsync();
            }
        }
    }

    [Fact]
    public async Task Concurrent_ticket_lifecycle_saves_allow_only_one_transition()
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
                    new CreateTicketCommand(TicketType.Request, $"Concurrent audit {Guid.NewGuid():N}", "audit chain concurrency", 1001, 1001),
                    CancellationToken.None)).Id;
            }

            using var start = new ManualResetEventSlim(false);
            using var ready = new CountdownEvent(12);
            var writes = Enumerable.Range(0, 12).Select(async index =>
            {
                await using var scope = provider.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
                Ticket ticket;
                try
                {
                    ticket = await repository.GetAsync(ticketId, CancellationToken.None) ?? throw new InvalidOperationException("Ticket fixture was not found.");
                    ticket.TransitionSpecializedStatus(TicketStatus.Resolved, 9001, DateTimeOffset.UtcNow, $"parallel-{index}");
                }
                finally
                {
                    ready.Signal();
                }
                start.Wait();
                try
                {
                    await repository.SaveAsync(ticket, CancellationToken.None);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }).ToArray();
            var snapshotsReady = ready.Wait(TimeSpan.FromSeconds(30));
            start.Set();
            var results = await Task.WhenAll(writes);
            Assert.True(snapshotsReady, "Concurrent ticket snapshots were not all prepared within 30 seconds.");
            Assert.Equal(1, results.Count(x => x));
            Assert.Equal(11, results.Count(x => !x));

            await using var verifyScope = provider.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var rows = await db.Audits.AsNoTracking()
                .Where(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString())
                .OrderBy(x => x.Id)
                .ToArrayAsync();
            var result = AuditChainVerifier.Verify(rows, "TICKET", ticketId.ToString());
            Assert.True(result.IsValid, $"{result.Failure} at audit {result.FailedAuditId}.");
            Assert.Equal(2, result.RecordsVerified);
            Assert.Equal((int)TicketStatus.Resolved, await db.Tickets.Where(x => x.Id == ticketId).Select(x => x.StatusId).SingleAsync());
        }
        finally
        {
            if (ticketId != 0)
            {
                await using var cleanupScope = provider.CreateAsyncScope();
                var db = cleanupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
            }
        }
    }

    [Fact]
    public async Task Legacy_approval_persists_designated_approver_and_decision_actor()
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
                    new CreateTicketCommand(TicketType.Request, $"Approval authority {Guid.NewGuid():N}", "approval authority", 1001, 1001),
                    CancellationToken.None)).Id;

                var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
                var ticket = await repository.GetAsync(ticketId, CancellationToken.None) ?? throw new InvalidOperationException("Ticket fixture was not found.");
                ticket.RequestApproval(2001, 1001, DateTimeOffset.UtcNow);
                await repository.SaveAsync(ticket, CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
                var ticket = await repository.GetAsync(ticketId, CancellationToken.None) ?? throw new InvalidOperationException("Ticket fixture was not found.");
                Assert.Equal(2001L, ticket.ApprovalApproverUserId);
                Assert.Throws<InvalidOperationException>(() => ticket.Approve(2002, DateTimeOffset.UtcNow));
                Assert.Equal(TicketStatus.PendingApproval, ticket.Status);
                ticket.Approve(2001, DateTimeOffset.UtcNow);
                await repository.SaveAsync(ticket, CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var row = await db.Tickets.AsNoTracking().SingleAsync(x => x.Id == ticketId);
                Assert.Equal(2001L, row.ApprovalApproverUserId);
                Assert.Equal(1, row.Approved);
                Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString() && x.Action == "APPROVAL_APPROVED" && x.ActorId == 2001));
            }
        }
        finally
        {
            if (ticketId != 0)
            {
                await using var cleanupScope = provider.CreateAsyncScope();
                var db = cleanupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
            }
        }
    }

    [Fact]
    public async Task Ticket_lifecycle_service_persists_wait_resume_resolution_close_reopen_and_cancel()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();

        await using var provider = BuildProvider();
        var createdIds = new List<long>();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var created = await service.CreateAsync(new CreateTicketCommand(TicketType.Request, $"PostgreSQL lifecycle {Guid.NewGuid():N}", "full lifecycle", 1001, 1001), CancellationToken.None);
            createdIds.Add(created.Id);
            var executionDb = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            var slaId = created.Id + 900000000;
            await executionDb.Database.ExecuteSqlInterpolatedAsync($@"insert into ""PA_TICKETS_SLA"" (""N_SLA_ID"",""N_TICKET_ID"",""S_STATUS"") values ({slaId},{created.Id},'RUNNING')");
            await service.ClassifyAsync(created.Id, 2001, 3001, 4001, 2, 1001, CancellationToken.None);
            var routed = await service.RouteAsync(created.Id, 501, 601, 1001, CancellationToken.None);
            var workItem = Assert.Single(routed.WorkItems);

            await service.ExecuteAsync(created.Id, workItem.Id, 7100, 1001, CancellationToken.None);
            var waiting = await service.WaitAsync(created.Id, workItem.Id, new WaitingRequest("WAITING_CUSTOMER", null, null, null), 7100, CancellationToken.None);
            Assert.Equal(TicketStatus.Waiting, waiting.Status);
            Assert.Equal("WAITING_CUSTOMER", Assert.Single(waiting.WorkItems).WaitingFor);
            Assert.Equal("PAUSED", await executionDb.Slas.Where(x => x.Id == slaId).Select(x => x.Status).SingleAsync());

            var resumed = await service.ResumeAsync(created.Id, workItem.Id, 7100, CancellationToken.None);
            Assert.Equal(TicketStatus.InProgress, resumed.Status);
            Assert.Equal("RUNNING", await executionDb.Slas.Where(x => x.Id == slaId).Select(x => x.Status).SingleAsync());
            await service.WaitAsync(created.Id, workItem.Id, new WaitingRequest("WAITING_CUSTOMER", null, null, null), 7100, CancellationToken.None);
            await service.ResumeAsync(created.Id, workItem.Id, 7100, CancellationToken.None);
            Assert.Equal(2, await executionDb.SlaPauses.CountAsync(x => x.SlaId == slaId && x.EndedAt != null));
            await service.AddEvidenceAsync(created.Id, 7100, CancellationToken.None);
            await service.CompleteWorkAsync(created.Id, workItem.Id, 7100, CancellationToken.None);
            var resolved = await service.ResolveAsync(created.Id, 1001, CancellationToken.None);
            Assert.Equal(TicketStatus.Resolved, resolved.Status);
            var closed = await service.CloseAsync(created.Id, 1001, CancellationToken.None);
            Assert.Equal(TicketStatus.Closed, closed.Status);

            var reopened = await service.ReopenAsync(created.Id, "Additional customer evidence", 1001, CancellationToken.None);
            Assert.Equal(TicketStatus.Reopened, reopened.Status);
            var cancelled = await service.CancelAsync(created.Id, "Request withdrawn", 1001, CancellationToken.None);
            Assert.Equal(TicketStatus.Cancelled, cancelled.Status);

            await using var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var row = await db.Tickets.SingleAsync(x => x.Id == created.Id);
            Assert.Equal((int)TicketStatus.Cancelled, row.StatusId);
            var persistedWork = await db.WorkItems.SingleAsync(x => x.Id == workItem.Id);
            Assert.Equal("COMPLETED", persistedWork.Status);
            Assert.Null(persistedWork.WaitingFor);
            Assert.True(persistedWork.WaitingMinutes >= 0);
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == created.Id && x.Type == "WORK_ITEM_WAITING" && x.Reason == "WAITING_CUSTOMER"));
            Assert.Contains(await db.Events.Where(x => x.TicketId == created.Id).Select(x => x.Type).ToListAsync(), x => x == "WORK_ITEM_RESUMED");
            Assert.Contains(await db.Events.Where(x => x.TicketId == created.Id).Select(x => x.Type).ToListAsync(), x => x == "TICKET_CLOSED");
            Assert.Contains(await db.Events.Where(x => x.TicketId == created.Id).Select(x => x.Type).ToListAsync(), x => x == "TICKET_REOPENED");
            Assert.Contains(await db.Events.Where(x => x.TicketId == created.Id).Select(x => x.Type).ToListAsync(), x => x == "TICKET_CANCELLED");
        }
        finally
        {
            if (createdIds.Count > 0)
            {
                await using var cleanupScope = provider.CreateAsyncScope();
                var db = cleanupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                foreach (var id in createdIds)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ASSIGNMENT"" where ""N_TICKET_ID"" = {id}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORK_ITEM"" where ""N_TICKET_ID"" = {id}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {id}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_PAUSE"" where ""N_SLA_ID"" = {id + 900000000}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA"" where ""N_SLA_ID"" = {id + 900000000}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({id})");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {id}");
                }
            }
        }
    }

    [Fact]
    public async Task Waiting_reason_configuration_enforces_effective_status_and_required_metadata()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();

        await using var provider = BuildProvider();
        var createdIds = new List<long>();
        var reasonId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try
        {
            await using (var seedScope = provider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                db.WaitingReasons.Add(new WaitingReasonRow
                {
                    Id = reasonId,
                    Code = "WAITING_EVIDENCE",
                    Active = true,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1),
                    PauseSla = true,
                    RequireComment = true,
                    RequireEvidence = true,
                    RequireExpectedDate = true,
                    AllowedFromStatuses = "InProgress"
                });
                db.WaitingReasons.Add(new WaitingReasonRow
                {
                    Id = reasonId + 1,
                    Code = "WAITING_INACTIVE",
                    Active = false,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1),
                    PauseSla = true,
                    AllowedFromStatuses = "InProgress"
                });
                await db.SaveChangesAsync();
            }

            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var created = await service.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Waiting rules {Guid.NewGuid():N}", "waiting rules", 1001, 1001), CancellationToken.None);
            createdIds.Add(created.Id);
            await service.ClassifyAsync(created.Id, 2001, 3001, 4001, 2, 1001, CancellationToken.None);
            var routed = await service.RouteAsync(created.Id, 501, 601, 1001, CancellationToken.None);
            var workItem = Assert.Single(routed.WorkItems);
            await service.ExecuteAsync(created.Id, workItem.Id, 7100, 1001, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.WaitAsync(created.Id, workItem.Id, new WaitingRequest("UNCONFIGURED", null, null, null), 7100, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => service.WaitAsync(created.Id, workItem.Id, new WaitingRequest("WAITING_EVIDENCE", null, null, null), 7100, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.WaitAsync(created.Id, workItem.Id, new WaitingRequest("WAITING_INACTIVE", null, null, null), 7100, CancellationToken.None));

            var waiting = await service.WaitAsync(created.Id, workItem.Id, new WaitingRequest("WAITING_EVIDENCE", "Need customer log", "evidence-123", DateTimeOffset.UtcNow.AddDays(2)), 7100, CancellationToken.None);
            var persisted = Assert.Single(waiting.WorkItems);
            Assert.Equal("WAITING_EVIDENCE", persisted.WaitingFor);
            Assert.Equal("Need customer log", persisted.WaitingComment);
            Assert.Equal("evidence-123", persisted.WaitingEvidenceReference);
            Assert.NotNull(persisted.WaitingExpectedDate);
        }
        finally
        {
            await using var cleanupScope = provider.CreateAsyncScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WAITING_REASON"" where ""N_WAITING_REASON_ID"" in ({reasonId}, {reasonId + 1})");
            foreach (var id in createdIds)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ASSIGNMENT"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORK_ITEM"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {id}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({id})");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {id}");
            }
        }
    }

    [Fact]
    public async Task PostgreSql_audit_rows_reject_direct_update_delete_and_log_controlled_disposal()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();

        await using var provider = BuildProvider();
        long ticketId = 0;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            ticketId = (await service.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Audit protection {Guid.NewGuid():N}", "audit protection", 1001, 1001), CancellationToken.None)).Id;
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var audit = await db.Audits.Where(x => x.EntityType == "TICKET" && x.EntityId == ticketId.ToString()).OrderBy(x => x.Id).FirstAsync();
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"update ""PA_TICKETS_AUDIT"" set ""S_REASON"" = 'tampered' where ""N_AUDIT_ID"" = {audit.Id}"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_AUDIT"" where ""N_AUDIT_ID"" = {audit.Id}"));
            await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
            Assert.False(await db.Audits.AnyAsync(x => x.Id == audit.Id));
            Assert.Equal(1, await db.Database.SqlQuery<int>($@"select count(*)::integer as ""Value"" from ""PA_TICKETS_AUDIT_MAINTENANCE"" where ""N_TICKET_ID"" = {ticketId}").SingleAsync());
        }
        finally
        {
            if (ticketId != 0)
            {
                await using var cleanupScope = provider.CreateAsyncScope();
                var db = cleanupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
            }
        }
    }

    private sealed record LifecycleAuthorizationFixture(
        long ActorId, long AssigneeId, long DepartmentId, long TeamId, long RoleId,
        long MembershipId, long RuleId, string Provenance);

    private sealed record ClassificationReference(long ServiceId, long CategoryId, long RequestTypeId);

    private static async Task<ClassificationReference> RequireActiveClassificationReferenceAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var reference = await (from requestType in db.CatalogueRequestTypes.AsNoTracking()
                               join service in db.CatalogueServices.AsNoTracking() on requestType.ServiceId equals service.Id
                               join category in db.CatalogueCategories.AsNoTracking()
                                   on new { requestType.ServiceId, requestType.CategoryId }
                                   equals new { category.ServiceId, CategoryId = category.Id }
                               where requestType.Active && requestType.Status == "PUBLISHED" &&
                                     requestType.EffectiveFrom <= now && (requestType.EffectiveTo == null || requestType.EffectiveTo > now) &&
                                     service.Active && service.EffectiveFrom <= now && (service.EffectiveTo == null || service.EffectiveTo > now) &&
                                     category.Active && category.EffectiveFrom <= now && (category.EffectiveTo == null || category.EffectiveTo > now)
                               orderby requestType.Id
                               select new ClassificationReference(service.Id, category.Id, requestType.Id))
            .FirstOrDefaultAsync();
        return reference ?? throw new InvalidOperationException(
            "No active, published, effective request type with active service/category exists; no test fixture writes were started.");
    }

    private static async Task<LifecycleAuthorizationFixture> CreateLifecycleAuthorizationFixtureAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var provenance = $"M1-LIFECYCLE-TEST:{suffix}";

        var permissions = await db.AdministrationPermissions.AsNoTracking()
            .Where(x => x.Code == "TICKET_READ" || x.Code == "TICKET_UPDATE" || x.Code == "TICKET_ASSIGN_TEAM")
            .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal);
        foreach (var code in new[] { "TICKET_READ", "TICKET_UPDATE", "TICKET_ASSIGN_TEAM" })
        {
            if (!permissions.TryGetValue(code, out var permission) || !permission.Active ||
                permission.ModuleCode != "TICKETS" || permission.ScopeClass != "RECORD" ||
                (code == "TICKET_READ" && (permission.ActionCode != "VIEW" || permission.SecurityClass != "READ_ONLY" || !permission.ReadOnly)) ||
                (code == "TICKET_UPDATE" && (permission.ActionCode != "EDIT" || permission.SecurityClass != "BUSINESS" || permission.ReadOnly)) ||
                (code == "TICKET_ASSIGN_TEAM" && (permission.ActionCode != "ASSIGN" || permission.SecurityClass != "BUSINESS" || permission.ReadOnly)))
                throw new InvalidOperationException($"Required canonical permission metadata is absent or mismatched: {code}.");
        }

        var actorId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_USER_SEQ", id => db.AdministrationUsers.AnyAsync(x => x.Id >= id));
        var actor = new AdministrationUserRow
        {
            Id = actorId, UserCode = $"M1_ACTOR_{suffix}", LoginName = $"m1_actor_{suffix}",
            DisplayName = "M1 lifecycle test actor", DepartmentId = null, TeamId = null,
            Active = true, CreatedAt = now, CreatedBy = actorId
        };
        db.AdministrationUsers.Add(actor);
        await db.SaveChangesAsync();

        var departmentId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_DEPARTMENT_SEQ", id => db.AdministrationDepartments.AnyAsync(x => x.Id >= id));
        var teamId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_TEAM_SEQ", id => db.AdministrationTeams.AnyAsync(x => x.Id >= id));
        var department = new AdministrationDepartmentRow
        {
            Id = departmentId, Code = $"M1_{suffix}", Name = "M1 lifecycle test department",
            Active = true, CreatedAt = now, CreatedBy = actorId
        };
        db.AdministrationDepartments.Add(department);
        await db.SaveChangesAsync();
        var team = new AdministrationTeamRow
        {
            Id = teamId, DepartmentId = departmentId, Code = $"M1_{suffix}", Name = "M1 lifecycle test team",
            Active = true, CreatedAt = now, CreatedBy = actorId
        };
        db.AdministrationTeams.Add(team);
        await db.SaveChangesAsync();

        var assigneeId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_USER_SEQ", id => db.AdministrationUsers.AnyAsync(x => x.Id >= id));
        var assignee = new AdministrationUserRow
        {
            Id = assigneeId, UserCode = $"M1_ASSIGNEE_{suffix}", LoginName = $"m1_assignee_{suffix}",
            DisplayName = "M1 lifecycle test assignee", DepartmentId = departmentId, TeamId = teamId,
            Active = true, CreatedAt = now, CreatedBy = actorId
        };
        db.AdministrationUsers.Add(assignee);
        actor.DepartmentId = departmentId;
        actor.TeamId = teamId;
        await db.SaveChangesAsync();

        var roleId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_ROLE_SEQ", id => db.AdministrationRoles.AnyAsync(x => x.Id >= id));
        var role = new AdministrationRoleRow
        {
            Id = roleId, Code = $"M1_TEST_{suffix}", Name = "M1 lifecycle test role",
            SystemRole = false, Active = true
        };
        db.AdministrationRoles.Add(role);
        await db.SaveChangesAsync();

        var membershipId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_USER_ROLE_SEQ", id => db.AdministrationUserRoles.AnyAsync(x => x.Id >= id));
        db.AdministrationUserRoles.Add(new AdministrationUserRoleRow
        {
            Id = membershipId, UserId = actorId, RoleId = roleId, ValidFrom = now.AddMinutes(-1),
            Active = true, CreatedAt = now, CreatedBy = actorId
        });
        await db.SaveChangesAsync();

        foreach (var code in new[] { "TICKET_READ", "TICKET_UPDATE", "TICKET_ASSIGN_TEAM" })
        {
            var permissionId = permissions[code].Id;
            var grantId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_ROLE_PERMISSION_SEQ", id => db.AdministrationRolePermissions.AnyAsync(x => x.Id >= id));
            db.AdministrationRolePermissions.Add(new AdministrationRolePermissionRow
            {
                Id = grantId, RoleId = roleId, PermissionId = permissionId,
                ValidFrom = now.AddMinutes(-1), EffectiveStatus = EffectiveGrantAuthorization.Active,
                ProvenanceRef = provenance, Active = true, UpdatedAt = now, UpdatedBy = actorId
            });
        }

        var teamScopeId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_ROLE_SCOPE_SEQ", id => db.AdministrationUserRoleScopes.AnyAsync(x => x.Id >= id));
        db.AdministrationUserRoleScopes.Add(new AdministrationUserRoleScopeRow
        {
            Id = teamScopeId, UserRoleId = membershipId, ScopeType = "TEAM", DepartmentId = departmentId,
            TeamId = teamId, ValidFrom = now.AddMinutes(-1), Active = true, CreatedAt = now, CreatedBy = actorId
        });
        var ruleId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_SEQ", id => db.AssignmentAuthorityRules.AnyAsync(x => x.Id >= id));
        db.AssignmentAuthorityRules.Add(new Fr021AssignmentAuthorityRuleRow
        {
            Id = ruleId, PermissionCode = "TICKET_ASSIGN_TEAM", TargetRelation = "OWN_TEAM", Mode = "ASSIGN",
            Enabled = true, NotifyTargetManager = false, RequireManagerAcceptance = false,
            ValidFrom = now.AddMinutes(-1), Version = 1, CreatedAt = now, CreatedBy = actorId
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new LifecycleAuthorizationFixture(actorId, assigneeId, departmentId, teamId, roleId, membershipId, ruleId, provenance);
    }

    private static async Task AddExactTicketScopeAsync(TicketsDbContext db, LifecycleAuthorizationFixture fixture, long ticketId)
    {
        var scopeId = await NextSafeSequenceIdAsync(db, "PA_TICKETS_ROLE_SCOPE_SEQ", id => db.AdministrationUserRoleScopes.AnyAsync(x => x.Id >= id));
        db.AdministrationUserRoleScopes.Add(new AdministrationUserRoleScopeRow
        {
            Id = scopeId, UserRoleId = fixture.MembershipId, ScopeType = "RECORD", ResourceType = "TICKET",
            ResourceId = ticketId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValidFrom = DateTimeOffset.UtcNow.AddMinutes(-1), Active = true,
            CreatedAt = DateTimeOffset.UtcNow, CreatedBy = fixture.ActorId
        });
        await db.SaveChangesAsync();
    }

    private static async Task DeleteLifecycleAuthorizationFixtureAsync(TicketsDbContext db, LifecycleAuthorizationFixture fixture)
    {
        await db.AssignmentAuthorityRules.Where(x => x.Id == fixture.RuleId).ExecuteDeleteAsync();
        await db.AdministrationUserRoleScopes.Where(x => x.UserRoleId == fixture.MembershipId).ExecuteDeleteAsync();
        await db.AdministrationRolePermissions.Where(x => x.RoleId == fixture.RoleId).ExecuteDeleteAsync();
        await db.AdministrationUserRoles.Where(x => x.Id == fixture.MembershipId).ExecuteDeleteAsync();
        await db.AdministrationRoles.Where(x => x.Id == fixture.RoleId).ExecuteDeleteAsync();
        await db.AdministrationUsers.Where(x => x.Id == fixture.AssigneeId || x.Id == fixture.ActorId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DepartmentId, (long?)null).SetProperty(x => x.TeamId, (long?)null));
        await db.AdministrationUsers.Where(x => x.Id == fixture.AssigneeId).ExecuteDeleteAsync();
        await db.AdministrationTeams.Where(x => x.Id == fixture.TeamId).ExecuteDeleteAsync();
        await db.AdministrationDepartments.Where(x => x.Id == fixture.DepartmentId).ExecuteDeleteAsync();
        await db.AdministrationUsers.Where(x => x.Id == fixture.ActorId).ExecuteDeleteAsync();
    }

    private static async Task<long> NextSafeSequenceIdAsync(TicketsDbContext db, string sequenceName, Func<long, Task<bool>> collisionCheck)
    {
        var sql = sequenceName switch
        {
            "PA_TICKETS_USER_SEQ" => "SELECT nextval('public.\"PA_TICKETS_USER_SEQ\"'::regclass) AS \"Value\"",
            "PA_TICKETS_DEPARTMENT_SEQ" => "SELECT nextval('public.\"PA_TICKETS_DEPARTMENT_SEQ\"'::regclass) AS \"Value\"",
            "PA_TICKETS_TEAM_SEQ" => "SELECT nextval('public.\"PA_TICKETS_TEAM_SEQ\"'::regclass) AS \"Value\"",
            "PA_TICKETS_ROLE_SEQ" => "SELECT nextval('public.\"PA_TICKETS_ROLE_SEQ\"'::regclass) AS \"Value\"",
            "PA_TICKETS_USER_ROLE_SEQ" => "SELECT nextval('public.\"PA_TICKETS_USER_ROLE_SEQ\"'::regclass) AS \"Value\"",
            "PA_TICKETS_ROLE_PERMISSION_SEQ" => "SELECT nextval('public.\"PA_TICKETS_ROLE_PERMISSION_SEQ\"'::regclass) AS \"Value\"",
            "PA_TICKETS_ROLE_SCOPE_SEQ" => "SELECT nextval('public.\"PA_TICKETS_ROLE_SCOPE_SEQ\"'::regclass) AS \"Value\"",
            "PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_SEQ" => "SELECT nextval('public.\"PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_SEQ\"'::regclass) AS \"Value\"",
            _ => throw new InvalidOperationException("Unapproved fixture sequence.")
        };
        var id = await db.Database.SqlQueryRaw<long>(sql).SingleAsync();
        if (await collisionCheck(id))
            throw new InvalidOperationException($"Fixture sequence {sequenceName} returned an ID at or below an existing key; stop without retry. The sequence value was consumed.");
        return id;
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

    private static string Prefix(TicketType type) => type switch
    {
        TicketType.Request => "REQ-",
        TicketType.Incident => "INC-",
        TicketType.Problem => "PRB-",
        TicketType.Task => "TSK-",
        TicketType.Change => "CHG-",
        _ => "TKT-"
    };
}
