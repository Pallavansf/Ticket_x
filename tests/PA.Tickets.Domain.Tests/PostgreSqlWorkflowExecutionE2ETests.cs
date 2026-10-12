using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using PA.Tickets.Application;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Sla;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Domain.Sla;
using PA.Tickets.Domain.Workflow;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlWorkflowExecutionE2ETests
{
    [Fact]
    public async Task Workflow_and_approval_instance_boundaries_enforce_ticket_scope_and_step_ownership()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker();
        var baseId = NewBaseId();

        try
        {
            long firstTicket;
            await using (var scope = provider.CreateAsyncScope())
            {
                var lifecycle = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                firstTicket = (await lifecycle.CreateAsync(new CreateTicketCommand(TicketType.Request, "Workflow authorization boundary", null, 1001, 1001), CancellationToken.None)).Id;
                tracker.TicketIds.Add(firstTicket);
            }
            var secondTicket = await CreateClassifiedTicketAsync(provider, TicketType.Request, 4, tracker);
            await SeedWorkflowAsync(provider, tracker, baseId, firstTicket, "BOUNDARY1", new[] { (1L, "A") }, Array.Empty<(long, long, WorkflowCondition?)>());
            await SeedWorkflowAsync(provider, tracker, baseId + 10000, secondTicket, "BOUNDARY2", new[] { (1L, "A") }, Array.Empty<(long, long, WorkflowCondition?)>());

            await using (var scope = provider.CreateAsyncScope())
            {
                var instances = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceCreationService>();
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    instances.CreateAsync(firstTicket, baseId + 100, 9999, $"{baseId}-activate", null, CancellationToken.None));
                var created = await instances.CreateAsync(firstTicket, baseId + 100, 1001, $"{baseId}-activate", null, CancellationToken.None);
                tracker.WorkflowInstanceIds.Add(created.InstanceId);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    instances.CreateAsync(firstTicket, baseId + 100, 9999, $"{baseId}-activate", null, CancellationToken.None));
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var execution = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    execution.ActivateInitialStepAsync(baseId + 500, 9999, $"{baseId}-activate", null, CancellationToken.None));
                await execution.ActivateInitialStepAsync(baseId + 10500, 9001, $"{baseId + 10000}-activate", null, CancellationToken.None);
            }

            long foreignStepId;
            string foreignPublicWorkItemId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                foreignStepId = await db.WorkflowSteps.Where(x => x.WorkflowInstanceId == baseId + 10500).Select(x => x.Id).SingleAsync();
                var work = await db.WorkItems.SingleAsync(x => x.WorkflowStepInstanceId == foreignStepId);
                foreignPublicWorkItemId = work.PublicId;
                work.TicketId = firstTicket;
                await db.SaveChangesAsync();
                Assert.Empty(await db.WorkflowSteps.Where(x => x.WorkflowInstanceId == baseId + 500).ToListAsync());
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var execution = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    execution.CompleteWorkItemAsync(foreignPublicWorkItemId, 7100, $"{baseId}-complete-wrong-work", null, CancellationToken.None));

                var approvals = scope.ServiceProvider.GetRequiredService<IApprovalExecutionRepository>();
                var request = new DurableApprovalInstanceRequest(firstTicket, foreignStepId, DurableApprovalMode.AnyOne,
                    [new DurableApprovalApprover(7100)], 1001, 9001, null);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    approvals.CreateInstanceAsync(request, 9001, $"{baseId}-foreign-step", null, CancellationToken.None));
            }
        }
        finally { await CleanupAsync(provider, tracker); }
    }

    [Fact]
    public async Task Sequential_workflow_creates_routes_assigns_reassigns_completes_and_preserves_idempotency()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await EnsureWorkflowAssigneeScopesAsync(provider);
        var tracker = new CleanupTracker();
        var baseId = NewBaseId();

        try
        {
            var ticketId = await CreateClassifiedTicketAsync(provider, TicketType.Request, priorityId: 4, tracker);
            await SeedWorkflowAsync(provider, tracker, baseId, ticketId, "SEQ", new[] { (1L, "A"), (2L, "B"), (3L, "C") }, new[] { (1L, 2L, (WorkflowCondition?)null), (2L, 3L, null) });

            await using (var scope = provider.CreateAsyncScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
                var activated = await repo.ActivateInitialStepAsync(baseId + 500, 9001, $"{baseId}-activate", "seq", CancellationToken.None);
                Assert.True(activated.Applied);
            }

            string publicA;
            long workA;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var active = await db.WorkflowSteps.SingleAsync(x => x.WorkflowInstanceId == baseId + 500 && x.WorkflowStepId == baseId + 1);
                var work = await db.WorkItems.SingleAsync(x => x.WorkflowStepInstanceId == active.Id);
                Assert.Equal("PENDING", work.Status);
                Assert.Equal(501, work.ToDepartmentId);
                Assert.Equal(601, work.ToTeamId);
                Assert.Equal(7100, work.AssignedToUserId);
                Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{16}$", work.PublicId);
                publicA = work.PublicId;
                workA = work.Id;
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    repo.ReassignWorkItemAsync(publicA, 9999, 501, 601, 7150, "cross-scope attempt", $"{baseId}-reassign-denied", "seq", CancellationToken.None));
                var reassigned = await repo.ReassignWorkItemAsync(publicA, 9001, 501, 601, 7150, "load balance", $"{baseId}-reassign", "seq", CancellationToken.None);
                Assert.True(reassigned.Applied);

                var completed = await repo.CompleteWorkItemAsync(publicA, 7150, $"{baseId}-complete-a", "seq", CancellationToken.None);
                Assert.True(completed.Applied);

                var retry = await repo.CompleteWorkItemAsync(publicA, 7150, $"{baseId}-complete-a", "seq", CancellationToken.None);
                Assert.True(retry.AlreadyProcessed);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var b = await db.WorkItems.SingleAsync(x => x.TicketId == ticketId && x.WorkflowStepId == baseId + 2);
                Assert.Equal(7200, b.AssignedToUserId);
                var activeAssignments = await db.Assignments.CountAsync(x => x.WorkItemId == workA && x.UnassignedAt == null);
                Assert.Equal(1, activeAssignments);
                var audits = await db.Audits.Where(x => x.EntityType == "WORK_ITEM" && x.EntityId == workA.ToString()).OrderBy(x => x.Id).ToListAsync();
                Assert.True(audits.Count >= 3);
                Assert.NotNull(audits.Last().PreviousHash);
            }

            await CompleteOnlyWorkItemForStepAsync(provider, ticketId, baseId + 2, 7200, $"{baseId}-complete-b");
            await CompleteOnlyWorkItemForStepAsync(provider, ticketId, baseId + 3, 7300, $"{baseId}-complete-c");

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var instance = await db.WorkflowInstances.SingleAsync(x => x.Id == baseId + 500);
                var ticket = await db.Tickets.SingleAsync(x => x.Id == ticketId);
                Assert.Equal("COMPLETED", instance.Status);
                Assert.Equal((int)TicketStatus.Resolved, ticket.StatusId);
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "WORKFLOW_STEP_COMPLETED"));
            }
        }
        finally { await CleanupAsync(provider, tracker); }
    }

    [Fact]
    public async Task Parallel_join_and_conditional_transitions_use_persisted_ticket_context()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await EnsureWorkflowAssigneeScopesAsync(provider);
        var tracker = new CleanupTracker();
        var parallelBase = NewBaseId();
        var conditionalBase = parallelBase + 10000;

        try
        {
            var parallelTicket = await CreateClassifiedTicketAsync(provider, TicketType.Incident, priorityId: 4, tracker);
            await SeedWorkflowAsync(provider, tracker, parallelBase, parallelTicket, "PAR", new[] { (1L, "A"), (2L, "B"), (3L, "C"), (4L, "D") }, new[] { (1L, 2L, (WorkflowCondition?)null), (1L, 3L, null), (2L, 4L, null), (3L, 4L, null) });
            await ActivateAndCompleteInitialAsync(provider, parallelBase, actor: 7100);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.True(await db.WorkItems.AnyAsync(x => x.TicketId == parallelTicket && x.WorkflowStepId == parallelBase + 2));
                Assert.True(await db.WorkItems.AnyAsync(x => x.TicketId == parallelTicket && x.WorkflowStepId == parallelBase + 3));
                Assert.False(await db.WorkItems.AnyAsync(x => x.TicketId == parallelTicket && x.WorkflowStepId == parallelBase + 4));
            }

            await CompleteOnlyWorkItemForStepAsync(provider, parallelTicket, parallelBase + 2, 7200, $"{parallelBase}-complete-b");
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.False(await db.WorkItems.AnyAsync(x => x.TicketId == parallelTicket && x.WorkflowStepId == parallelBase + 4));
            }

            await CompleteOnlyWorkItemForStepAsync(provider, parallelTicket, parallelBase + 3, 7300, $"{parallelBase}-complete-c");
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.Equal(1, await db.WorkItems.CountAsync(x => x.TicketId == parallelTicket && x.WorkflowStepId == parallelBase + 4));
            }

            var conditionalTicket = await CreateClassifiedTicketAsync(provider, TicketType.Request, priorityId: 4, tracker);
            await SeedWorkflowAsync(provider, tracker, conditionalBase, conditionalTicket, "COND", new[] { (1L, "A"), (2L, "B"), (3L, "C") }, new (long, long, WorkflowCondition?)[] { (1L, 2L, new WorkflowCondition("Priority", "EQUALS", "4")), (1L, 3L, new WorkflowCondition("Priority", "NOT_EQUALS", "4")) });
            await ActivateAndCompleteInitialAsync(provider, conditionalBase, actor: 7100);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.True(await db.WorkItems.AnyAsync(x => x.TicketId == conditionalTicket && x.WorkflowStepId == conditionalBase + 2));
                Assert.False(await db.WorkItems.AnyAsync(x => x.TicketId == conditionalTicket && x.WorkflowStepId == conditionalBase + 3));
            }
        }
        finally { await CleanupAsync(provider, tracker); }
    }

    [Fact]
    public async Task Approval_delegation_sla_and_durable_jobs_persist_idempotently()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker();
        var baseId = NewBaseId();

        try
        {
            var ticketId = await CreateClassifiedTicketAsync(provider, TicketType.Change, priorityId: 4, tracker);
            var approvalId = baseId + 700;
            var rejectedApprovalId = baseId + 701;
            var slaId = baseId + 800;
            tracker.ApprovalIds.Add(approvalId);
            tracker.ApprovalIds.Add(rejectedApprovalId);
            tracker.SlaIds.Add(slaId);
            tracker.JobDedupePrefix = $"pg-e2e-{baseId}";

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                db.Approvals.Add(new ApprovalExecutionRow { Id = approvalId, TicketId = ticketId, Status = "PENDING", ApproverId = 8100 });
                db.Approvals.Add(new ApprovalExecutionRow { Id = rejectedApprovalId, TicketId = ticketId, Status = "PENDING", ApproverId = 8300 });
                db.Slas.Add(new SlaExecutionRow { Id = slaId, TicketId = ticketId, Status = "RUNNING" });
                await db.SaveChangesAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var approvals = scope.ServiceProvider.GetRequiredService<IApprovalExecutionRepository>();
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => approvals.DecideAsync(approvalId, 8200, DurableApprovalDecision.Approved, null, $"{baseId}-bad-approval", "appr", CancellationToken.None));
                var delegated = await approvals.DelegateAsync(approvalId, 8100, 8200, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), "coverage", 8100, $"{baseId}-delegate", "appr", CancellationToken.None);
                Assert.True(delegated.Applied);
                var approved = await approvals.DecideAsync(approvalId, 8200, DurableApprovalDecision.Approved, "approved by delegate", $"{baseId}-approve", "appr", CancellationToken.None);
                Assert.True(approved.Applied);
                var approvalRetry = await approvals.DecideAsync(approvalId, 8200, DurableApprovalDecision.Approved, "approved by delegate", $"{baseId}-approve", "appr", CancellationToken.None);
                Assert.True(approvalRetry.AlreadyProcessed);
                var rejected = await approvals.DecideAsync(rejectedApprovalId, 8300, DurableApprovalDecision.Rejected, "reject path", $"{baseId}-reject", "appr", CancellationToken.None);
                Assert.Equal("REJECTED", rejected.Message);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var sla = scope.ServiceProvider.GetRequiredService<ISlaExecutionRepository>();
                var pause = await sla.ApplyAsync(slaId, DurableSlaOperation.Pause, "CLIENT", 9001, $"{baseId}-sla-pause", "sla", CancellationToken.None);
                Assert.Equal("PAUSED", pause.Message);
                var pauseRetry = await sla.ApplyAsync(slaId, DurableSlaOperation.Pause, "CLIENT", 9001, $"{baseId}-sla-pause", "sla", CancellationToken.None);
                Assert.True(pauseRetry.AlreadyProcessed);
                var resume = await sla.ApplyAsync(slaId, DurableSlaOperation.Resume, "CLIENT", 9001, $"{baseId}-sla-resume", "sla", CancellationToken.None);
                Assert.Equal("RUNNING", resume.Message);
                var breach = await sla.ApplyAsync(slaId, DurableSlaOperation.Breach, "elapsed", 9001, $"{baseId}-sla-breach", "sla", CancellationToken.None);
                Assert.Equal("BREACHED", breach.Message);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                Assert.True(await jobs.EnqueueAsync("TEST", tracker.JobDedupePrefix + "-1", "{}", DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None));
                Assert.False(await jobs.EnqueueAsync("TEST", tracker.JobDedupePrefix + "-1", "{}", DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None));
                var leased = await jobs.LeaseReadyAsync("worker-a", 10, CancellationToken.None);
                var job = Assert.Single(leased, x => x.DedupeKey == tracker.JobDedupePrefix + "-1");
                await jobs.CompleteAsync(job.Id, CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.Equal("APPROVED", (await db.Approvals.SingleAsync(x => x.Id == approvalId)).Status);
                Assert.Equal("REJECTED", (await db.Approvals.SingleAsync(x => x.Id == rejectedApprovalId)).Status);
                Assert.True(await db.ApprovalDelegations.AnyAsync(x => x.ApprovalId == approvalId && x.DelegateUser == 8200));
                Assert.Equal("BREACHED", (await db.Slas.SingleAsync(x => x.Id == slaId)).Status);
                Assert.True(await db.SlaPauses.AnyAsync(x => x.SlaId == slaId && x.EndedAt != null));
                Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Category == "SLA"));
                Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "APPROVAL" && x.EntityId == approvalId.ToString() && x.PreviousHash != null));
                Assert.True(await db.Jobs.AnyAsync(x => x.DedupeKey == tracker.JobDedupePrefix + "-1" && x.Status == "COMPLETED"));
            }
        }
        finally { await CleanupAsync(provider, tracker); }
    }

    [Fact]
    public async Task Durable_jobs_dedupe_concurrent_leasing_and_retry_backoff_are_postgresql_safe()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker { JobDedupePrefix = $"pg-jobs-{NewBaseId()}" };

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                Assert.True(await jobs.EnqueueAsync("TEST", tracker.JobDedupePrefix + "-race", "{}", DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None));
                Assert.False(await jobs.EnqueueAsync("TEST", tracker.JobDedupePrefix + "-race", "{}", DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None));
            }

            var leases = await Task.WhenAll(Enumerable.Range(0, 4).Select(async worker =>
            {
                await using var scope = provider.CreateAsyncScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                return await jobs.LeaseReadyAsync($"worker-{worker}", 10, CancellationToken.None);
            }));
            Assert.Equal(1, leases.SelectMany(x => x).Count(x => x.DedupeKey == tracker.JobDedupePrefix + "-race"));

            long retryJobId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                Assert.True(await jobs.EnqueueAsync("TEST", tracker.JobDedupePrefix + "-retry", "{}", DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None));
                var retry = Assert.Single(await jobs.LeaseReadyAsync("retry-worker", 10, CancellationToken.None), x => x.DedupeKey == tracker.JobDedupePrefix + "-retry");
                retryJobId = retry.Id;
                await jobs.FailAsync(retry.Id, "temporary failure", exhausted: false, CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                Assert.DoesNotContain((await jobs.LeaseReadyAsync("restart-worker", 10, CancellationToken.None)), x => x.Id == retryJobId);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var stale = await db.Jobs.SingleAsync(x => x.Id == retryJobId);
                stale.Status = "PROCESSING";
                stale.LockedAt = DateTimeOffset.UtcNow.AddMinutes(-11);
                stale.LockedBy = "crashed-worker";
                await db.SaveChangesAsync();
            }

            long recoveredJobId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                var recovered = Assert.Single(await jobs.LeaseReadyAsync("recovery-worker", 10, CancellationToken.None), x => x.Id == retryJobId);
                recoveredJobId = recovered.Id;
                await jobs.CompleteAsync(recovered.Id, CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                Assert.DoesNotContain((await jobs.LeaseReadyAsync("post-completion-worker", 10, CancellationToken.None)), x => x.Id == recoveredJobId);
                Assert.True(await jobs.EnqueueAsync("TEST", tracker.JobDedupePrefix + "-exhausted", "{}", DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None));
                var finalAttempt = Assert.Single(await jobs.LeaseReadyAsync("terminal-worker", 10, CancellationToken.None), x => x.DedupeKey == tracker.JobDedupePrefix + "-exhausted");
                var trackedDb = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var row = await trackedDb.Jobs.SingleAsync(x => x.Id == finalAttempt.Id);
                row.MaxAttempts = 1;
                await trackedDb.SaveChangesAsync();
                await jobs.FailAsync(finalAttempt.Id, "temporary final attempt", exhausted: false, CancellationToken.None);
                await using (var verifyScope = provider.CreateAsyncScope())
                {
                    var db = verifyScope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                    var terminal = await db.Jobs.SingleAsync(x => x.Id == finalAttempt.Id);
                    Assert.Equal("EXHAUSTED", terminal.Status);
                    Assert.Equal(1, terminal.Attempts);
                    Assert.NotNull(terminal.FailedAt);
                    var newWorker = verifyScope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                    Assert.DoesNotContain(await newWorker.LeaseReadyAsync("after-exhaustion", 10, CancellationToken.None), x => x.Id == finalAttempt.Id);
                }
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_BACKGROUND_JOB"" where ""S_DEDUPE_KEY"" like {tracker.JobDedupePrefix + "%"}");
        }
    }

    [Fact]
    public async Task Sla_policy_resolution_persists_effective_version_calendar_holiday_and_due_times()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var baseId = NewBaseId();
        var ticketId = baseId + 1;
        var slaId = baseId + 2;
        var genericVersionId = baseId + 3;
        var selectedVersionId = baseId + 4;
        var ambiguousVersionId = baseId + 5;
        var calendarId = baseId + 6;
        try
        {
            var workingHours = Enum.GetValues<DayOfWeek>().Where(day => day is not DayOfWeek.Sunday and not DayOfWeek.Saturday)
                .ToDictionary(day => day, _ => new PersistentSlaCalendarStore.TimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(17)));
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync($@"insert into ""PA_TICKETS"" (""N_TICKET_ID"",""S_TICKET_NO"",""N_TICKET_TYPE_ID"",""N_SERVICE_ID"",""N_REQUESTED_BY"",""S_SUBJECT"",""N_PRIORITY_ID"",""N_STATUS_ID"",""D_CREATED_AT"",""N_CREATED_BY"") values ({ticketId},{"CHG-" + ticketId},{(int)TicketType.Change},2001,1001,{"SLA policy resolution"},4,2,{DateTimeOffset.UtcNow},1001)");
                db.SlaCalendars.Add(new SlaCalendarRow { Id = calendarId, Code = "BUSINESS", Active = true, TimeZoneId = "UTC", WorkingHoursJson = JsonSerializer.Serialize(workingHours), HolidaysJson = JsonSerializer.Serialize(new[] { new DateOnly(2026, 9, 15) }) });
                await db.SaveChangesAsync();
            }

            var startedAt = new DateTimeOffset(2026, 9, 14, 16, 45, 0, TimeSpan.Zero);
            var effectiveFrom = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
            var selectedEffectiveFrom = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                db.SlaPolicies.Add(new SlaPolicyRow { Id = genericVersionId, PolicyId = 10, Code = "DEFAULT", VersionNo = 1, Active = true, Status = "PUBLISHED", EffectiveFrom = effectiveFrom, CalendarCode = "BUSINESS", ResponseTargetMinutes = 60, ResolutionTargetMinutes = 120 });
                db.SlaPolicies.Add(new SlaPolicyRow { Id = selectedVersionId, PolicyId = 11, Code = "CHANGE-P1", VersionNo = 2, Active = true, Status = "PUBLISHED", EffectiveFrom = selectedEffectiveFrom, ServiceId = 2001, PriorityId = 4, CalendarCode = "BUSINESS", ResponseTargetMinutes = 30, ResolutionTargetMinutes = 90, EscalationRules = "{\"level\":1}" });
                await db.SaveChangesAsync();
                var orchestration = scope.ServiceProvider.GetRequiredService<ISlaOrchestrationService>();
                var result = await orchestration.StartAsync(slaId, ticketId, new(2001, null, null, (int)TicketType.Change, 4, null, null, null), 9001, startedAt, CancellationToken.None);
                Assert.Equal(11, result.PolicyId);
                Assert.Equal(2, result.PolicyVersion);
                Assert.Equal(new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero).Date, result.ResponseDueAt!.Value.Date);
                Assert.True(result.ResolutionDueAt > result.ResponseDueAt);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var sla = await db.Slas.SingleAsync(x => x.Id == slaId);
                Assert.Equal(11, sla.PolicyId);
                Assert.Equal(2, sla.PolicyVersion);
                Assert.Equal("BUSINESS", sla.CalendarCode);
                Assert.NotNull(sla.ResponseDueAt);
                Assert.NotNull(sla.ResolutionDueAt);
                db.SlaPolicies.Add(new SlaPolicyRow { Id = ambiguousVersionId, PolicyId = 12, Code = "AMBIGUOUS", VersionNo = 1, Active = true, Status = "PUBLISHED", EffectiveFrom = selectedEffectiveFrom, ServiceId = 2001, PriorityId = 4, CalendarCode = "BUSINESS", ResponseTargetMinutes = 30, ResolutionTargetMinutes = 90 });
                await db.SaveChangesAsync();
                var resolver = scope.ServiceProvider.GetRequiredService<ISlaPolicyResolver>();
                await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(new(2001, null, null, (int)TicketType.Change, 4, null, null, null), startedAt, CancellationToken.None));
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA"" where ""N_SLA_ID"" = {slaId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_POLICY"" where ""N_SLA_POLICY_VERSION_ID"" in ({genericVersionId}, {selectedVersionId}, {ambiguousVersionId})");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_CALENDAR"" where ""N_SLA_CALENDAR_ID"" = {calendarId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
        }
    }

    [Fact]
    public async Task Workflow_version_selection_uses_effective_published_version_and_rejects_ambiguity()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var workflowId = NewBaseId();
        var firstId = workflowId + 1;
        var selectedId = workflowId + 2;
        var ambiguousId = workflowId + 3;
        var now = DateTimeOffset.UtcNow;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                db.WorkflowDefinitions.Add(new WorkflowDefinitionRow { Id = workflowId, Code = "EFFECTIVE" + workflowId, Name = "Effective workflow" });
                db.WorkflowVersions.Add(new WorkflowVersionRow { Id = firstId, WorkflowId = workflowId, VersionNo = 1, Status = "PUBLISHED", Definition = JsonSerializer.Serialize(new { effectiveFrom = now.AddDays(-5), effectiveTo = now.AddDays(-1) }) });
                db.WorkflowVersions.Add(new WorkflowVersionRow { Id = selectedId, WorkflowId = workflowId, VersionNo = 2, Status = "PUBLISHED", Definition = JsonSerializer.Serialize(new { effectiveFrom = now.AddDays(-1) }) });
                await db.SaveChangesAsync();
                var selector = scope.ServiceProvider.GetRequiredService<IWorkflowVersionSelector>();
                var selected = await selector.ResolveAsync(workflowId, now, CancellationToken.None);
                Assert.Equal(selectedId, selected.VersionId);
                db.WorkflowVersions.Add(new WorkflowVersionRow { Id = ambiguousId, WorkflowId = workflowId, VersionNo = 3, Status = "PUBLISHED", Definition = JsonSerializer.Serialize(new { effectiveFrom = now.AddDays(-1) }) });
                await db.SaveChangesAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(() => selector.ResolveAsync(workflowId, now, CancellationToken.None));
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_VERSION"" where ""N_WORKFLOW_VERSION_ID"" in ({firstId}, {selectedId}, {ambiguousId})");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW"" where ""N_WORKFLOW_ID"" = {workflowId}");
        }
    }

    [Fact]
    public async Task Concurrent_same_entity_audit_writes_preserve_one_postgresql_hash_chain()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker();
        var baseId = NewBaseId();
        var approvalId = baseId + 1;
        try
        {
            await EnsureActiveApprovalUsersAsync(provider, 8201, 8202);
            var ticketId = await CreateClassifiedTicketAsync(provider, TicketType.Change, 4, tracker);
            tracker.ApprovalIds.Add(approvalId);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                db.Approvals.Add(new ApprovalExecutionRow { Id = approvalId, TicketId = ticketId, Status = "PENDING", ApproverId = 8100 });
                await db.SaveChangesAsync();
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
            {
                await using var scope = provider.CreateAsyncScope();
                var approvals = scope.ServiceProvider.GetRequiredService<IApprovalExecutionRepository>();
                return await approvals.DelegateAsync(approvalId, 8100, 8200 + index, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), $"coverage-{index}", 8100, $"{baseId}-delegate-{index}", "audit-race", CancellationToken.None);
            }));
            Assert.All(results, result => Assert.True(result.Applied));

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var audits = await db.Audits.Where(x => x.EntityType == "APPROVAL" && x.EntityId == approvalId.ToString()).OrderBy(x => x.Id).ToListAsync();
                Assert.Equal(2, audits.Count);
                Assert.Null(audits[0].PreviousHash);
                Assert.Equal(audits[0].Hash, audits[1].PreviousHash);
                Assert.NotEqual(audits[0].Hash, audits[1].Hash);
            }
        }
        finally
        {
            await CleanupAsync(provider, tracker);
        }
    }

    [Fact]
    public async Task Workflow_instance_creation_persists_selected_version_and_never_switches_existing_instances()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker();
        var baseId = NewBaseId();
        var workflowId = baseId + 100;
        var versionOne = baseId + 101;
        var versionTwo = baseId + 102;
        try
        {
            var ticketId = await CreateClassifiedTicketAsync(provider, TicketType.Request, 2, tracker);
            tracker.WorkflowIds.Add(workflowId);
            tracker.WorkflowVersionIds.Add(versionOne);
            tracker.WorkflowVersionIds.Add(versionTwo);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var now = DateTimeOffset.UtcNow;
                db.WorkflowDefinitions.Add(new WorkflowDefinitionRow { Id = workflowId, Code = "CREATE" + workflowId, Name = "Creation workflow" });
                db.WorkflowVersions.Add(new WorkflowVersionRow { Id = versionOne, WorkflowId = workflowId, VersionNo = 1, Status = "PUBLISHED", Definition = JsonSerializer.Serialize(new { effectiveFrom = now.AddDays(-2), effectiveTo = now.AddDays(-1) }) });
                db.WorkflowVersions.Add(new WorkflowVersionRow { Id = versionTwo, WorkflowId = workflowId, VersionNo = 2, Status = "PUBLISHED", Definition = JsonSerializer.Serialize(new { effectiveFrom = now.AddDays(-1) }) });
                await db.SaveChangesAsync();
                var creation = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceCreationService>();
                var first = await creation.CreateAsync(ticketId, workflowId, 9001, $"{baseId}-create-1", "create", CancellationToken.None, versionOne);
                tracker.WorkflowInstanceIds.Add(first.InstanceId);
                Assert.Equal(versionOne, first.WorkflowVersionId);

                var retry = await creation.CreateAsync(ticketId, workflowId, 9001, $"{baseId}-create-1", "create", CancellationToken.None, versionOne);
                Assert.Equal(first.InstanceId, retry.InstanceId);
                Assert.Equal(versionOne, retry.WorkflowVersionId);

                var second = await creation.CreateAsync(ticketId, workflowId, 9001, $"{baseId}-create-2", "create", CancellationToken.None, versionTwo);
                tracker.WorkflowInstanceIds.Add(second.InstanceId);
                Assert.Equal(versionTwo, second.WorkflowVersionId);
                Assert.Equal(versionOne, await db.WorkflowInstances.Where(x => x.Id == first.InstanceId).Select(x => x.VersionId).SingleAsync());
            }
        }
        finally
        {
            await CleanupAsync(provider, tracker);
        }
    }

    [Fact]
    public async Task Background_worker_detects_both_sla_deadlines_once_and_escalates_after_restart()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker();
        var slaId = NewBaseId();
        var policyId = slaId + 1;
        var calendarCode = $"SLA_MONITOR_{slaId}";
        var jobPrefix = $"SLA:{slaId}:";
        tracker.SlaIds.Add(slaId);
        tracker.JobDedupePrefix = jobPrefix;
        try
        {
            var ticketId = await CreateClassifiedTicketAsync(provider, TicketType.Request, 4, tracker);
            var now = DateTimeOffset.UtcNow;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var hours = Enum.GetValues<DayOfWeek>().ToDictionary(
                    day => day,
                    _ => new PersistentSlaCalendarStore.TimeWindow(TimeSpan.Zero, TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59))));
                db.SlaCalendars.Add(new SlaCalendarRow
                {
                    Id = policyId + 2,
                    Code = calendarCode,
                    Active = true,
                    TimeZoneId = "UTC",
                    WorkingHoursJson = JsonSerializer.Serialize(hours),
                    HolidaysJson = "[]",
                    EffectiveFrom = now.AddDays(-1)
                });
                db.SlaPolicies.Add(new SlaPolicyRow
                {
                    Id = policyId,
                    PolicyId = policyId,
                    Code = $"SLA_MONITOR_{policyId}",
                    VersionNo = 1,
                    Active = true,
                    Status = "PUBLISHED",
                    CreatedAt = now.AddDays(-1),
                    CreatedBy = 9001,
                    EffectiveFrom = now.AddDays(-1),
                    ResponseTargetMinutes = 30,
                    ResolutionTargetMinutes = 60,
                    CalendarCode = calendarCode,
                    EscalationRules = "{\"level\":1}"
                });
                db.Slas.Add(new SlaExecutionRow
                {
                    Id = slaId,
                    TicketId = ticketId,
                    Status = "RUNNING",
                    PolicyId = policyId,
                    PolicyVersion = 1,
                    CalendarCode = calendarCode,
                    StartedAt = now.AddHours(-2),
                    ResponseDueAt = now.AddMinutes(-45),
                    ResolutionDueAt = now.AddMinutes(-15)
                });
                await db.SaveChangesAsync();
            }

            async Task RunWorkerAfterRestartAsync()
            {
                var worker = new PersistentBackgroundWorker(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<PersistentBackgroundWorker>.Instance,
                    provider.GetRequiredService<WorkerActivityRegistry>(),
                    TimeProvider.System);
                await worker.ProcessOnceAsync(CancellationToken.None);
            }
            await RunWorkerAfterRestartAsync();
            await RunWorkerAfterRestartAsync();

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.Equal("BREACHED", (await db.Slas.SingleAsync(x => x.Id == slaId)).Status);
                Assert.Equal(1, await db.Events.CountAsync(x => x.TicketId == ticketId && x.Type == "SLA_RESPONSE_BREACH"));
                Assert.Equal(1, await db.Events.CountAsync(x => x.TicketId == ticketId && x.Type == "SLA_RESOLUTION_BREACH"));
                Assert.Equal(1, await db.Events.CountAsync(x => x.TicketId == ticketId && x.Type == "SLA_ESCALATED"));
                Assert.Single(await db.Jobs.Where(x => x.DedupeKey == $"{jobPrefix}BREACH").ToArrayAsync());
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_BACKGROUND_JOB"" where ""S_DEDUPE_KEY"" like {jobPrefix + "%"}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_IDEMPOTENCY"" where ""S_IDEMPOTENCY_KEY"" like {jobPrefix + "%"}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA"" where ""N_SLA_ID"" = {slaId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_POLICY"" where ""N_POLICY_ID"" = {policyId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_CALENDAR"" where ""S_CALENDAR_CODE"" = {calendarCode}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_entity('SLA', {slaId.ToString()})");
            await CleanupAsync(provider, tracker);
        }
    }

    [Fact]
    public async Task Sla_deadline_monitor_applies_pause_extensions_and_completes_at_terminal_resolution()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker();
        var baseId = NewBaseId();
        var pausedSlaId = baseId;
        var terminalSlaId = baseId + 1;
        var policyId = baseId + 2;
        var closedSlaId = baseId + 3;
        var cancelledSlaId = baseId + 4;
        var calendarCode = $"SLA_TERMINAL_{baseId}";
        tracker.SlaIds.AddRange([pausedSlaId, terminalSlaId, closedSlaId, cancelledSlaId]);
        try
        {
            var pausedTicketId = await CreateClassifiedTicketAsync(provider, TicketType.Request, 4, tracker);
            var terminalTicketId = await CreateClassifiedTicketAsync(provider, TicketType.Request, 4, tracker);
            var closedTicketId = await CreateClassifiedTicketAsync(provider, TicketType.Request, 4, tracker);
            var cancelledTicketId = await CreateClassifiedTicketAsync(provider, TicketType.Request, 4, tracker);
            var now = DateTimeOffset.UtcNow;
            var terminalAt = now.AddMinutes(-10);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                var hours = Enum.GetValues<DayOfWeek>().ToDictionary(
                    day => day,
                    _ => new PersistentSlaCalendarStore.TimeWindow(TimeSpan.Zero, TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59))));
                db.SlaCalendars.Add(new SlaCalendarRow
                {
                    Id = policyId + 2,
                    Code = calendarCode,
                    Active = true,
                    TimeZoneId = "UTC",
                    WorkingHoursJson = JsonSerializer.Serialize(hours),
                    HolidaysJson = "[]",
                    EffectiveFrom = now.AddDays(-1)
                });
                db.SlaPolicies.Add(new SlaPolicyRow
                {
                    Id = policyId,
                    PolicyId = policyId,
                    Code = $"SLA_TERMINAL_{policyId}",
                    VersionNo = 1,
                    Active = true,
                    Status = "PUBLISHED",
                    CreatedAt = now.AddDays(-1),
                    CreatedBy = 9001,
                    EffectiveFrom = now.AddDays(-1),
                    ResponseTargetMinutes = 30,
                    ResolutionTargetMinutes = 60,
                    CalendarCode = calendarCode
                });
                db.Slas.AddRange(
                    new SlaExecutionRow
                    {
                        Id = pausedSlaId,
                        TicketId = pausedTicketId,
                        Status = "RUNNING",
                        PolicyId = policyId,
                        PolicyVersion = 1,
                        CalendarCode = calendarCode,
                        StartedAt = now.AddHours(-2),
                        ResponseDueAt = now.AddMinutes(-20),
                        ResolutionDueAt = now.AddHours(2)
                    },
                    new SlaExecutionRow
                    {
                        Id = terminalSlaId,
                        TicketId = terminalTicketId,
                        Status = "RUNNING",
                        PolicyId = policyId,
                        PolicyVersion = 1,
                        CalendarCode = calendarCode,
                        StartedAt = now.AddHours(-2),
                        ResponseDueAt = now.AddHours(-1),
                        ResolutionDueAt = now.AddMinutes(-30)
                    },
                    new SlaExecutionRow
                    {
                        Id = closedSlaId,
                        TicketId = closedTicketId,
                        Status = "RUNNING",
                        PolicyId = policyId,
                        PolicyVersion = 1,
                        CalendarCode = calendarCode,
                        StartedAt = now.AddHours(-2),
                        ResponseDueAt = now.AddHours(2),
                        ResolutionDueAt = now.AddHours(3)
                    },
                    new SlaExecutionRow
                    {
                        Id = cancelledSlaId,
                        TicketId = cancelledTicketId,
                        Status = "RUNNING",
                        PolicyId = policyId,
                        PolicyVersion = 1,
                        CalendarCode = calendarCode,
                        StartedAt = now.AddHours(-2),
                        ResponseDueAt = now.AddHours(2),
                        ResolutionDueAt = now.AddHours(3)
                    });
                db.SlaPauses.Add(new SlaPauseExecutionRow
                {
                    SlaId = pausedSlaId,
                    ReasonCode = "CLIENT",
                    StartedAt = now.AddMinutes(-65),
                    EndedAt = now.AddMinutes(-5),
                    StartedBy = 9001,
                    EndedBy = 9001
                });
                db.Events.Add(new EventRow
                {
                    TicketId = terminalTicketId,
                    Type = "TICKET_STATUS_CHANGED",
                    Category = "LIFECYCLE",
                    ActorId = 9001,
                    NewValue = "RESOLVED",
                    At = terminalAt
                });
                db.Events.AddRange(
                    new EventRow
                    {
                        TicketId = closedTicketId,
                        Type = "TICKET_STATUS_CHANGED",
                        Category = "LIFECYCLE",
                        ActorId = 9001,
                        NewValue = "CLOSED",
                        At = terminalAt
                    },
                    new EventRow
                    {
                        TicketId = cancelledTicketId,
                        Type = "TICKET_CANCELLED",
                        Category = "LIFECYCLE",
                        ActorId = 9001,
                        At = terminalAt
                    });
                await db.SaveChangesAsync();
                var ticketDb = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var resolvedTicket = await ticketDb.Tickets.SingleAsync(x => x.Id == terminalTicketId);
                resolvedTicket.StatusId = (int)TicketStatus.Resolved;
                resolvedTicket.ResolvedAt = terminalAt;
                var closedTicket = await ticketDb.Tickets.SingleAsync(x => x.Id == closedTicketId);
                closedTicket.StatusId = (int)TicketStatus.Closed;
                closedTicket.ClosedAt = terminalAt;
                (await ticketDb.Tickets.SingleAsync(x => x.Id == cancelledTicketId)).StatusId = (int)TicketStatus.Cancelled;
                await ticketDb.SaveChangesAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<SlaDeadlineMonitor>().ProcessDueAsync(CancellationToken.None);
            await using (var scope = provider.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<SlaDeadlineMonitor>().ProcessDueAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                Assert.Equal("RUNNING", (await db.Slas.SingleAsync(x => x.Id == pausedSlaId)).Status);
                Assert.Equal("COMPLETED", (await db.Slas.SingleAsync(x => x.Id == terminalSlaId)).Status);
                Assert.Equal(terminalAt.ToUnixTimeMilliseconds(), (await db.Slas.SingleAsync(x => x.Id == terminalSlaId)).CompletedAt?.ToUnixTimeMilliseconds());
                Assert.Equal("COMPLETED", (await db.Slas.SingleAsync(x => x.Id == closedSlaId)).Status);
                Assert.Equal("COMPLETED", (await db.Slas.SingleAsync(x => x.Id == cancelledSlaId)).Status);
                Assert.Equal(1, await db.Events.CountAsync(x => x.TicketId == terminalTicketId && x.Type == "SLA_RESPONSE_BREACH"));
                Assert.Equal(1, await db.Events.CountAsync(x => x.TicketId == terminalTicketId && x.Type == "SLA_RESOLUTION_BREACH"));
                Assert.Equal(0, await db.Events.CountAsync(x => (x.TicketId == closedTicketId || x.TicketId == cancelledTicketId) && (x.Type == "SLA_RESPONSE_BREACH" || x.Type == "SLA_RESOLUTION_BREACH")));
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            foreach (var slaId in tracker.SlaIds)
            {
                var idempotencyPrefix = $"SLA:{slaId}:%";
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_IDEMPOTENCY"" where ""S_SCOPE"" in ('SLA_DEADLINE','SLA_OPERATION') and ""S_IDEMPOTENCY_KEY"" like {idempotencyPrefix}");
            }
            foreach (var slaId in tracker.SlaIds)
                await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_PAUSE"" where ""N_SLA_ID"" = {slaId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA"" where ""N_SLA_ID"" in ({pausedSlaId}, {terminalSlaId}, {closedSlaId}, {cancelledSlaId})");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_POLICY"" where ""N_POLICY_ID"" = {policyId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_CALENDAR"" where ""S_CALENDAR_CODE"" = {calendarCode}");
            foreach (var slaId in tracker.SlaIds)
                await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_entity('SLA', {slaId.ToString()})");
            await CleanupAsync(provider, tracker);
        }
    }

    [Fact]
    public async Task Sla_administration_persists_version_and_breach_enqueues_configured_escalation()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var tracker = new CleanupTracker { JobDedupePrefix = $"SLA:{NewBaseId()}:BREACH" };
        var policyId = NewBaseId();
        var slaId = policyId + 1;
        try
        {
            var ticketId = await CreateClassifiedTicketAsync(provider, TicketType.Change, 4, tracker);
            tracker.SlaIds.Add(slaId);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                db.Slas.Add(new SlaExecutionRow { Id = slaId, TicketId = ticketId, Status = "RUNNING", PolicyId = policyId, PolicyVersion = 1 });
                await db.SaveChangesAsync();
                var configuration = scope.ServiceProvider.GetRequiredService<ISlaConfigurationService>();
                var policy = new SlaPolicyVersion(policyId, "ESCALATING", 1, DateTimeOffset.UtcNow.AddMinutes(-1), null, 30, 90, "ADMIN_CALENDAR", null, null, "{\"level\":1}", null, null, ServiceId: 2001, PriorityId: 4);
                await configuration.CreateCalendarAsync("ADMIN_CALENDAR", new BusinessCalendar("UTC", new Dictionary<DayOfWeek, (TimeSpan Start, TimeSpan End)> { [DayOfWeek.Monday] = (TimeSpan.FromHours(9), TimeSpan.FromHours(17)) }, new HashSet<DateOnly>()), 9001, "phase 4 calendar administration", CancellationToken.None);
                await configuration.CreateVersionAsync(policy, 9001, "phase 4 administration", CancellationToken.None);
                await configuration.ActivateAsync(policyId, 1, 9001, "phase 4 activation", CancellationToken.None);
                var sla = scope.ServiceProvider.GetRequiredService<ISlaExecutionRepository>();
                var breached = await sla.ApplyAsync(slaId, DurableSlaOperation.Breach, "resolution elapsed", 9001, $"{policyId}-breach", "sla-admin", CancellationToken.None);
                Assert.Equal("BREACHED", breached.Message);
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                var job = Assert.Single(await jobs.LeaseReadyAsync("escalation-worker", 10, CancellationToken.None));
                var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().Single(x => x.CanHandle(job.Type));
                await handler.HandleAsync(job, CancellationToken.None);
                await jobs.CompleteAsync(job.Id, CancellationToken.None);
                Assert.Equal(1, await db.Events.CountAsync(x => x.TicketId == ticketId && x.Type == "SLA_ESCALATED"));
                Assert.Equal(1, await db.Audits.CountAsync(x => x.EntityType == "SLA" && x.EntityId == slaId.ToString() && x.Action == "SLA_ESCALATED"));
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_BACKGROUND_JOB"" where ""S_DEDUPE_KEY"" = {tracker.JobDedupePrefix}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_IDEMPOTENCY"" where ""S_SCOPE"" = 'SLA_ESCALATION' and ""S_IDEMPOTENCY_KEY"" = {tracker.JobDedupePrefix}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA"" where ""N_SLA_ID"" = {slaId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_POLICY"" where ""N_POLICY_ID"" = {policyId}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_CALENDAR"" where ""S_CALENDAR_CODE"" = {"ADMIN_CALENDAR"}");
            await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_entity('SLA_POLICY', {(policyId * 1000 + 1).ToString()})");
            await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_entity('SLA', {slaId.ToString()})");
            await CleanupAsync(provider, tracker);
        }
    }


    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSQL",
                ["Security:AssignmentActors:0"] = "9001",
                ["Security:AssignmentActors:1"] = "7100",
                ["Security:AssignmentActors:2"] = "7150",
                ["Security:AssignmentActors:3"] = "7200",
                ["Security:AssignmentActors:4"] = "7300",
                ["Security:AssignmentActors:5"] = "9999",
                ["Security:AllowedAssignmentDepartments:0"] = "501",
                ["Security:AllowedAssignmentDepartments:1"] = "502",
                ["Security:AllowedAssignmentDepartments:2"] = "503",
                ["Security:AllowedAssignmentDepartments:3"] = "504",
                ["Security:AllowedAssignmentTeams:0"] = "601",
                ["Security:AllowedAssignmentTeams:1"] = "602",
                ["Security:AllowedAssignmentTeams:2"] = "603",
                ["Security:AllowedAssignmentTeams:3"] = "604",
                ["WorkflowRouting:Rules:0:RuleCode"] = "STEP_A",
                ["WorkflowRouting:Rules:0:StepCode"] = "A",
                ["WorkflowRouting:Rules:0:DepartmentId"] = "501",
                ["WorkflowRouting:Rules:0:TeamId"] = "601",
                ["WorkflowRouting:Rules:0:UserId"] = "7100",
                ["WorkflowRouting:Rules:1:RuleCode"] = "STEP_B",
                ["WorkflowRouting:Rules:1:StepCode"] = "B",
                ["WorkflowRouting:Rules:1:DepartmentId"] = "502",
                ["WorkflowRouting:Rules:1:TeamId"] = "602",
                ["WorkflowRouting:Rules:1:UserId"] = "7200",
                ["WorkflowRouting:Rules:2:RuleCode"] = "STEP_C",
                ["WorkflowRouting:Rules:2:StepCode"] = "C",
                ["WorkflowRouting:Rules:2:DepartmentId"] = "503",
                ["WorkflowRouting:Rules:2:TeamId"] = "603",
                ["WorkflowRouting:Rules:2:UserId"] = "7300",
                ["WorkflowRouting:Rules:3:RuleCode"] = "STEP_D",
                ["WorkflowRouting:Rules:3:StepCode"] = "D",
                ["WorkflowRouting:Rules:3:DepartmentId"] = "504",
                ["WorkflowRouting:Rules:3:TeamId"] = "604",
                ["WorkflowRouting:Rules:3:UserId"] = "7400",
                ["WorkflowRouting:Triage:RuleCode"] = "TRIAGE",
                ["WorkflowRouting:Triage:DepartmentId"] = "501",
                ["WorkflowRouting:Triage:TeamId"] = "601"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task EnsureWorkflowAssigneeScopesAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        foreach (var id in new[] { 502L, 503L, 504L })
        {
            if (!await db.AdministrationDepartments.AnyAsync(x => x.Id == id))
                db.AdministrationDepartments.Add(new AdministrationDepartmentRow { Id = id, Code = $"TEST-DEPT-{id}", Name = $"Test Department {id}", Active = true, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = 0 });
        }
        await db.SaveChangesAsync();
        foreach (var (teamId, departmentId) in new[] { (602L, 502L), (603L, 503L), (604L, 504L) })
        {
            if (!await db.AdministrationTeams.AnyAsync(x => x.Id == teamId))
                db.AdministrationTeams.Add(new AdministrationTeamRow { Id = teamId, DepartmentId = departmentId, Code = $"TEST-TEAM-{teamId}", Name = $"Test Team {teamId}", Active = true, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = 0 });
        }
        await db.SaveChangesAsync();
        foreach (var (userId, departmentId, teamId) in new[]
        {
            (7200L, 502L, 602L),
            (7300L, 503L, 603L),
            (7400L, 504L, 604L)
        })
        {
            var user = await db.AdministrationUsers.SingleAsync(x => x.Id == userId);
            user.DepartmentId = departmentId;
            user.TeamId = teamId;
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureActiveApprovalUsersAsync(ServiceProvider provider, params long[] userIds)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        foreach (var userId in userIds)
        {
            var user = await db.AdministrationUsers.SingleOrDefaultAsync(x => x.Id == userId);
            if (user is null)
            {
                user = new AdministrationUserRow
                {
                    Id = userId,
                    UserCode = $"TEST-APPROVAL-{userId}",
                    LoginName = $"test-approval-{userId}",
                    DisplayName = $"Test Approval User {userId}",
                    DepartmentId = 501,
                    TeamId = 601,
                    Active = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = 0
                };
                db.AdministrationUsers.Add(user);
            }
            else
            {
                user.DepartmentId = 501;
                user.TeamId = 601;
                user.Active = true;
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task<long> CreateClassifiedTicketAsync(ServiceProvider provider, TicketType type, long priorityId, CleanupTracker tracker)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
        var created = await service.CreateAsync(new CreateTicketCommand(type, $"PostgreSQL workflow E2E {Guid.NewGuid():N}", "workflow execution coverage", 1001, 1001), CancellationToken.None);
        tracker.TicketIds.Add(created.Id);
        await service.ClassifyAsync(created.Id, 2001, 3001, 4001, priorityId, 1001, CancellationToken.None);
        return created.Id;
    }

    private static async Task SeedWorkflowAsync(ServiceProvider provider, CleanupTracker tracker, long baseId, long ticketId, string code, IReadOnlyCollection<(long Offset, string Code)> steps, IReadOnlyCollection<(long FromOffset, long ToOffset, WorkflowCondition? Condition)> transitions)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        var workflowId = baseId + 100;
        var versionId = baseId + 200;
        var instanceId = baseId + 500;
        tracker.WorkflowIds.Add(workflowId);
        tracker.WorkflowVersionIds.Add(versionId);
        tracker.WorkflowInstanceIds.Add(instanceId);
        db.WorkflowDefinitions.Add(new WorkflowDefinitionRow { Id = workflowId, Code = code + baseId, Name = code + " workflow" });
        db.WorkflowVersions.Add(new WorkflowVersionRow { Id = versionId, WorkflowId = workflowId, VersionNo = 1, Status = "PUBLISHED" });
        foreach (var step in steps.OrderBy(x => x.Offset))
        {
            db.WorkflowStepDefinitions.Add(new WorkflowStepDefinitionRow { Id = baseId + step.Offset, VersionId = versionId, StepNo = (int)step.Offset, Code = step.Code, Name = step.Code, Type = "WORK", ExecutionMode = "ALLREQUIRED" });
        }
        var transitionNo = 1;
        foreach (var transition in transitions)
        {
            db.WorkflowTransitions.Add(new WorkflowTransitionRow { Id = baseId + 300 + transitionNo++, VersionId = versionId, FromStepId = baseId + transition.FromOffset, ToStepId = baseId + transition.ToOffset, Code = $"T{transitionNo}", Condition = transition.Condition is null ? null : System.Text.Json.JsonSerializer.Serialize(transition.Condition) });
        }
        db.WorkflowInstances.Add(new WorkflowInstanceRow { Id = instanceId, TicketId = ticketId, VersionId = versionId, Status = "RUNNING", RowVersion = 0 });
        await db.SaveChangesAsync();
    }

    private static async Task ActivateAndCompleteInitialAsync(ServiceProvider provider, long baseId, long actor)
    {
        await using var scope = provider.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
        await repo.ActivateInitialStepAsync(baseId + 500, 9001, $"{baseId}-activate", "flow", CancellationToken.None);
        await using var lookupScope = provider.CreateAsyncScope();
        var db = lookupScope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        var work = await db.WorkItems.SingleAsync(x => x.WorkflowStepId == baseId + 1);
        await repo.CompleteWorkItemAsync(work.PublicId, actor, $"{baseId}-complete-a", "flow", CancellationToken.None);
    }

    private static async Task CompleteOnlyWorkItemForStepAsync(ServiceProvider provider, long ticketId, long workflowStepId, long actor, string key)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        var work = await db.WorkItems.SingleAsync(x => x.TicketId == ticketId && x.WorkflowStepId == workflowStepId);
        var repo = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
        var completed = await repo.CompleteWorkItemAsync(work.PublicId, actor, key, "flow", CancellationToken.None);
        Assert.True(completed.Applied || completed.AlreadyProcessed);
    }

    private static async Task CleanupAsync(ServiceProvider provider, CleanupTracker tracker)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        if (tracker.JobDedupePrefix is not null) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_BACKGROUND_JOB"" where ""S_DEDUPE_KEY"" like {tracker.JobDedupePrefix + "%"}");
        foreach (var slaId in tracker.SlaIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_PAUSE"" where ""N_SLA_ID"" = {slaId}");
        foreach (var slaId in tracker.SlaIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA"" where ""N_SLA_ID"" = {slaId}");
        foreach (var approvalId in tracker.ApprovalIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_APPROVAL_DELEGATION"" where ""N_APPROVAL_ID"" = {approvalId}");
        foreach (var approvalId in tracker.ApprovalIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_APPROVAL_HISTORY"" where ""N_APPROVAL_ID"" = {approvalId}");
        foreach (var approvalId in tracker.ApprovalIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_APPROVAL"" where ""N_APPROVAL_ID"" = {approvalId}");
        foreach (var instanceId in tracker.WorkflowInstanceIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY"" where ""N_STEP_INSTANCE_ID"" in (select ""N_STEP_INSTANCE_ID"" from ""PA_TICKETS_WORKFLOW_STEP_INSTANCE"" where ""N_WORKFLOW_INSTANCE_ID"" = {instanceId})");
        foreach (var ticketId in tracker.TicketIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ASSIGNMENT"" where ""N_TICKET_ID"" = {ticketId}");
        foreach (var ticketId in tracker.TicketIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVENT"" where ""N_TICKET_ID"" = {ticketId}");
        foreach (var ticketId in tracker.TicketIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORK_ITEM"" where ""N_TICKET_ID"" = {ticketId}");
        foreach (var instanceId in tracker.WorkflowInstanceIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_STEP_INSTANCE"" where ""N_WORKFLOW_INSTANCE_ID"" = {instanceId}");
        foreach (var instanceId in tracker.WorkflowInstanceIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_INSTANCE"" where ""N_WORKFLOW_INSTANCE_ID"" = {instanceId}");
        foreach (var versionId in tracker.WorkflowVersionIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_TRANSITION"" where ""N_WORKFLOW_VERSION_ID"" = {versionId}");
        foreach (var versionId in tracker.WorkflowVersionIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_STEP"" where ""N_WORKFLOW_VERSION_ID"" = {versionId}");
        foreach (var versionId in tracker.WorkflowVersionIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_VERSION"" where ""N_WORKFLOW_VERSION_ID"" = {versionId}");
        foreach (var workflowId in tracker.WorkflowIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW"" where ""N_WORKFLOW_ID"" = {workflowId}");
        await db.Database.ExecuteSqlRawAsync("delete from \"PA_TICKETS_IDEMPOTENCY\" where \"S_IDEMPOTENCY_KEY\" like '%-activate' or \"S_IDEMPOTENCY_KEY\" like '%-complete-%' or \"S_IDEMPOTENCY_KEY\" like '%-reassign' or \"S_IDEMPOTENCY_KEY\" like '%-delegate' or \"S_IDEMPOTENCY_KEY\" like '%-approve' or \"S_IDEMPOTENCY_KEY\" like '%-reject' or \"S_IDEMPOTENCY_KEY\" like '%-sla-%'");
        foreach (var ticketId in tracker.TicketIds) await db.Database.ExecuteSqlInterpolatedAsync($@"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
        foreach (var ticketId in tracker.TicketIds) await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS"" where ""N_TICKET_ID"" = {ticketId}");
    }

    private static long NewBaseId() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(1, 500);

    private sealed class CleanupTracker
    {
        public List<long> TicketIds { get; } = [];
        public List<long> WorkflowIds { get; } = [];
        public List<long> WorkflowVersionIds { get; } = [];
        public List<long> WorkflowInstanceIds { get; } = [];
        public List<long> ApprovalIds { get; } = [];
        public List<long> SlaIds { get; } = [];
        public string? JobDedupePrefix { get; set; }
    }
}