using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Sla;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Domain.Sla;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlProblemTests
{
    [Fact]
    public async Task Problem_lifecycle_analysis_known_error_resolution_and_reopen_are_persisted()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var problemId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IProblemService>();
            var created = await service.CreateAsync(new CreateProblemCommand("Database instability", "Recurring failures", "Database failover is unreliable", 1001, 1, 2, 3, 4, "problem-create", "problem-create-1"), 1001, CancellationToken.None);
            problemId = created.TicketId;
            Assert.Equal(TicketType.Problem, created.Type);
            Assert.StartsWith("PRB-", created.Number);
            Assert.Equal(ProblemStatus.Open, created.Status);

            var investigating = await service.StartInvestigationAsync(problemId, 1001, CancellationToken.None);
            Assert.Equal(ProblemStatus.Investigating, investigating.Status);
            await service.UpdateRootCauseAsync(problemId, "Failover quorum is undersized", 1001, CancellationToken.None);
            await service.UpdateWorkaroundAsync(problemId, "Route traffic to the standby region", 1001, CancellationToken.None);
            var knownError = await service.DeclareKnownErrorAsync(problemId, 1001, CancellationToken.None);
            Assert.True(knownError.IsKnownError);
            await service.UpdatePermanentFixAsync(problemId, "Increase quorum and automate failover validation", 1001, CancellationToken.None);
            var resolved = await service.ResolveAsync(problemId, "Permanent failover fix deployed", 1001, CancellationToken.None);
            Assert.Equal(ProblemStatus.Resolved, resolved.Status);
            var closed = await service.CloseAsync(problemId, 1001, CancellationToken.None);
            Assert.Equal(ProblemStatus.Closed, closed.Status);
            var reopened = await service.ReopenAsync(problemId, "Regression detected", 1001, CancellationToken.None);
            Assert.Equal(ProblemStatus.Reopened, reopened.Status);

            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.True(await db.Problems.AnyAsync(x => x.TicketId == problemId && x.IsKnownError));
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == problemId && x.Type == "PROBLEM_ROOT_CAUSE_UPDATED"));
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == problemId && x.Type == "PROBLEM_DECLARED_KNOWN_ERROR"));
            Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "PROBLEM" && x.EntityId == problemId.ToString() && x.Action == "PROBLEM_RESOLVED"));
        }
        finally { await CleanupAsync(provider, problemId); }
    }

    [Fact]
    public async Task Problem_creation_is_idempotent_and_relationships_are_authorized_and_unique()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        var accessState = new TicketAccessTestState();
        await using var provider = BuildProvider(accessState: accessState);
        var problemId = 0L;
        var incidentId = 0L;
        var changeId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var problems = scope.ServiceProvider.GetRequiredService<IProblemService>();
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var command = new CreateProblemCommand("Shared outage", null, "Multiple incidents share one cause", 1001, IdempotencyKey: "problem-idempotent-1");
            var created = await problems.CreateAsync(command, 1001, CancellationToken.None);
            problemId = created.TicketId;
            await Assert.ThrowsAsync<InvalidOperationException>(() => problems.CreateAsync(command, 1001, CancellationToken.None));
            incidentId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Related incident", null, 1001, 1001), CancellationToken.None)).Id;
            changeId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Change, "Future fix", null, 1001, 1001), CancellationToken.None)).Id;

            var incidentRelation = await problems.RelateIncidentAsync(problemId, incidentId, "Incident caused by known problem", 1001, CancellationToken.None);
            Assert.Equal("INCIDENT_PROBLEM", incidentRelation.RelationType);
            await Assert.ThrowsAsync<InvalidOperationException>(() => problems.RelateIncidentAsync(problemId, incidentId, "Duplicate", 1001, CancellationToken.None));
            var changeRelation = await problems.RelateChangeAsync(problemId, changeId, "PROBLEM_CHANGE", "Fix will be delivered by Change", 1001, CancellationToken.None);
            Assert.Equal("PROBLEM_CHANGE", changeRelation.RelationType);
            Assert.Single(await problems.GetIncidentRelationshipsAsync(problemId, 1001, CancellationToken.None));
            accessState.DeniedTicketId = incidentId;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => problems.GetIncidentRelationshipsAsync(problemId, 1001, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => problems.GetAsync(problemId, 0, CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(provider, problemId);
            await CleanupAsync(provider, incidentId);
            await CleanupAsync(provider, changeId);
        }
    }

    [Fact]
    public async Task Problem_ticket_uses_effective_sla_and_workflow_instances_without_changing_domain_logic()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var problemId = 0L;
        var slaId = 0L;
        var policyId = 0L;
        var calendarId = 0L;
        var workflowId = 0L;
        var versionId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var problems = scope.ServiceProvider.GetRequiredService<IProblemService>();
            var problem = await problems.CreateAsync(new CreateProblemCommand("Database failover drift", "Recurring failover stops", "Quorum is undersized in the failover cluster", 1001, ServiceId: 1, CategoryId: 2, PriorityId: 4, CorrelationId: "problem-sla-create", IdempotencyKey: "problem-sla-key"), 1001, CancellationToken.None);
            problemId = problem.TicketId;
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();

            policyId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 9000;
            calendarId = policyId + 10;
            var businessHours = Enum.GetValues<DayOfWeek>().Where(day => day is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                .ToDictionary(day => day, _ => new PersistentSlaCalendarStore.TimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(17)));
            db.SlaCalendars.Add(new SlaCalendarRow { Id = calendarId, Code = "PROBLEM_BUSINESS", Active = true, TimeZoneId = "UTC", WorkingHoursJson = JsonSerializer.Serialize(businessHours), HolidaysJson = "[]" });
            db.SlaPolicies.Add(new SlaPolicyRow
            {
                Id = policyId + 1,
                PolicyId = policyId,
                Code = "PROBLEM-P1",
                VersionNo = 1,
                Active = true,
                Status = "PUBLISHED",
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-7),
                ServiceId = 1,
                TicketType = (int)TicketType.Problem,
                PriorityId = 4,
                CalendarCode = "PROBLEM_BUSINESS",
                ResponseTargetMinutes = 30,
                ResolutionTargetMinutes = 120,
                EscalationRules = "{\"level\":1}"
            });
            await db.SaveChangesAsync();

            var orchestration = scope.ServiceProvider.GetRequiredService<ISlaOrchestrationService>();
            slaId = policyId + 99;
            var sla = await orchestration.StartAsync(slaId, problemId, new SlaEvaluationContext(1, null, 3, (int)TicketType.Problem, 4, null, null, null), 1001, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(policyId, sla.PolicyId);
            Assert.NotNull(sla.ResponseDueAt);
            Assert.NotNull(sla.ResolutionDueAt);
            Assert.Equal("PROBLEM_BUSINESS", sla.CalendarCode);

            workflowId = policyId + 200;
            versionId = workflowId + 1;
            db.WorkflowDefinitions.Add(new WorkflowDefinitionRow { Id = workflowId, Code = "PROB" + workflowId, Name = "Problem workflow" });
            db.WorkflowVersions.Add(new WorkflowVersionRow { Id = versionId, WorkflowId = workflowId, VersionNo = 1, Status = "PUBLISHED", Definition = JsonSerializer.Serialize(new { effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }) });
            db.WorkflowStepDefinitions.Add(new WorkflowStepDefinitionRow
            {
                Id = workflowId + 1,
                VersionId = versionId,
                StepNo = 1,
                Code = "PROBLEM-START",
                Name = "Problem start",
                Type = "WORK",
                ExecutionMode = "ALLREQUIRED"
            });
            await db.SaveChangesAsync();

            var creation = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceCreationService>();
            var workflow = await creation.CreateAsync(problemId, workflowId, 1001, $"problem-create-{problemId}", "problem-workflow", CancellationToken.None);
            Assert.Equal(versionId, workflow.WorkflowVersionId);
            Assert.True(workflow.InstanceId > 0);

            var execution = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
            var activated = await execution.ActivateInitialStepAsync(workflow.InstanceId, 1001, $"problem-activate-{workflow.InstanceId}", "problem-workflow", CancellationToken.None);
            Assert.True(activated.Applied);
            Assert.True(await db.WorkItems.AnyAsync(x => x.TicketId == problemId));
        }
        finally
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
                if (problemId != 0)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ASSIGNMENT"" where ""N_TICKET_ID"" = {problemId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORK_ITEM"" where ""N_TICKET_ID"" = {problemId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_STEP_INSTANCE"" where ""N_WORKFLOW_INSTANCE_ID"" in (select ""N_WORKFLOW_INSTANCE_ID"" from ""PA_TICKETS_WORKFLOW_INSTANCE"" where ""N_TICKET_ID"" = {problemId})");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_INSTANCE"" where ""N_TICKET_ID"" = {problemId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA"" where ""N_SLA_ID"" = {slaId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_POLICY"" where ""N_POLICY_ID"" = {policyId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_SLA_CALENDAR"" where ""N_SLA_CALENDAR_ID"" = {calendarId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW_VERSION"" where ""N_WORKFLOW_VERSION_ID"" = {versionId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_WORKFLOW"" where ""N_WORKFLOW_ID"" = {workflowId}");
                }
            }
            await CleanupAsync(provider, problemId);
        }
    }

    [Fact]
    public async Task Problem_evidence_and_notification_journey_is_authorized_and_traceable()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider(restricted: true);
        var problemId = 0L;
        var attachmentId = 0L;
        var evidenceId = 0L;
        var templateId = 0L;
        var ruleId = 0L;
        var ruleVersionId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var problems = scope.ServiceProvider.GetRequiredService<IProblemService>();
            var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var created = await problems.CreateAsync(new CreateProblemCommand("Incident root cause", "Production issue", "Database cluster failover is not deterministic", 1001, 1, 2, 3, 4, "problem-evidence-create", "problem-evidence-key"), 1001, CancellationToken.None);
            problemId = created.TicketId;

            var attachment = await attachments.CreateAsync(new CreateAttachmentCommand(problemId, null, "EVIDENCE", "evidence.txt", "text/plain", "INTERNAL", "supporting evidence", new MemoryStream("problem evidence"u8.ToArray()), "problem-attachment-1", "problem-attachment-correlation"), 1001, CancellationToken.None);
            attachmentId = attachment.Attachment.Id;

            var evidence = await attachments.CreateEvidenceAsync(new CreateEvidenceCommand(problemId, null, attachmentId, "EV-PROBLEM-1", "TEST_RESULT", "Root cause validation", "CTRL-99", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(30), "problem-evidence-1", "problem-evidence-correlation"), 1001, CancellationToken.None);
            evidenceId = evidence.Id;

            Assert.True(await db.Events.AnyAsync(x => x.TicketId == problemId && x.Type == "EVIDENCE_CREATED"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => attachments.GetAsync(attachmentId, 2002, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => attachments.ReviewEvidenceAsync(evidenceId, new ReviewEvidenceCommand("UNDER_REVIEW", "manual review"), 2002, CancellationToken.None));

            templateId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000;
            ruleId = templateId + 100;
            ruleVersionId = ruleId + 1;
            db.CommunicationTemplates.Add(new CommunicationTemplateRow
            {
                Id = templateId,
                Code = "PROBLEM_CREATED",
                Channel = "EMAIL",
                Version = 1,
                Subject = "Problem {{TicketNumber}} created",
                Body = "Problem {{TicketNumber}} created.",
                Status = "ACTIVE",
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
            });
            db.NotificationRules.Add(new NotificationRuleRow
            {
                Id = ruleId,
                Code = $"PROBLEM_CREATED_{ruleId}",
                Name = "Problem created notification",
                Description = "Problem created via rule",
                EventType = "PROBLEM_CREATED",
                Enabled = true,
                Priority = 100,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            db.NotificationRuleVersions.Add(new NotificationRuleVersionRow
            {
                Id = ruleVersionId,
                RuleId = ruleId,
                VersionNo = 1,
                Condition = "{\"all\":[{\"field\":\"Status\",\"op\":\"Equals\",\"value\":\"OPEN\"}]}",
                RecipientDefinition = "REQUESTER",
                ChannelDefinition = "EMAIL",
                TemplateCode = "PROBLEM_CREATED",
                DeduplicationScope = "PER_EVENT",
                Enabled = true,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedBy = 1001,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            var eventId = await db.Events.Where(x => x.TicketId == problemId && x.Type == "PROBLEM_CREATED").OrderByDescending(x => x.Id).Select(x => x.Id).SingleAsync();
            var orchestrator = scope.ServiceProvider.GetRequiredService<INotificationOrchestrator>();
            var notification = await orchestrator.ProcessAsync(new NotificationEventEnvelope(eventId, problemId, null, "PROBLEM_CREATED", "PROBLEM", "Problem created", DateTimeOffset.UtcNow, "problem-notify", null), 1001, CancellationToken.None);
            Assert.True(notification.Matched);
            Assert.Equal(1, notification.CreatedCommunicationCount);
            Assert.True(await db.NotificationExecutions.AnyAsync(x => x.EventId == eventId && x.RuleVersionId == ruleVersionId));
        }
        finally
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                if (problemId != 0)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_COMMUNICATION"" where ""N_TICKET_ID"" = {problemId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_NOTIFICATION_EXECUTION"" where ""N_TICKET_ID"" = {problemId} or ""N_RULE_ID"" = {ruleId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_NOTIFICATION_RULE_VERSION"" where ""N_RULE_ID"" = {ruleId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_NOTIFICATION_RULE"" where ""N_NOTIFICATION_RULE_ID"" = {ruleId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_COMMUNICATION_TEMPLATE"" where ""N_TEMPLATE_VERSION_ID"" = {templateId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_EVIDENCE"" where ""N_TICKET_ID"" = {problemId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ATTACHMENT_VERSION"" where ""N_ATTACHMENT_ID"" = {attachmentId}");
                    await db.Database.ExecuteSqlInterpolatedAsync($@"delete from ""PA_TICKETS_ATTACHMENT"" where ""N_TICKET_ID"" = {problemId}");
                }
            }
            await CleanupAsync(provider, problemId);
        }
    }

    [Fact]
    public async Task Problem_state_guards_reject_invalid_transitions_and_dont_mutate_closed_tickets()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var problemId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var problems = scope.ServiceProvider.GetRequiredService<IProblemService>();
            var problem = await problems.CreateAsync(new CreateProblemCommand("Managed escalation", "Recurrent issue", "Storage saturation on primary node", 1001, 1, 2, 3, 4, "problem-guard-create", "problem-guard-key"), 1001, CancellationToken.None);
            problemId = problem.TicketId;

            await Assert.ThrowsAsync<InvalidOperationException>(() => problems.ResolveAsync(problemId, "summary", 1001, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => problems.CloseAsync(problemId, 1001, CancellationToken.None));

            await problems.StartInvestigationAsync(problemId, 1001, CancellationToken.None);
            await problems.UpdateRootCauseAsync(problemId, "Primary node is saturating storage", 1001, CancellationToken.None);
            await problems.DeclareKnownErrorAsync(problemId, 1001, CancellationToken.None);
            await problems.UpdatePermanentFixAsync(problemId, "Rebalance storage and add a failover threshold", 1001, CancellationToken.None);
            var resolved = await problems.ResolveAsync(problemId, "Capacity rebalanced and failover threshold restored", 1001, CancellationToken.None);
            Assert.Equal(ProblemStatus.Resolved, resolved.Status);
            var closed = await problems.CloseAsync(problemId, 1001, CancellationToken.None);
            Assert.Equal(ProblemStatus.Closed, closed.Status);

            await Assert.ThrowsAsync<InvalidOperationException>(() => problems.UpdateRootCauseAsync(problemId, "New cause after close", 1001, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => problems.ReopenAsync(problemId, "", 1001, CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(provider, problemId);
        }
    }


    private static ServiceProvider BuildProvider(bool restricted = false, TicketAccessTestState? accessState = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "PostgreSQL",
            ["Relationships:Allowed:0"] = "Incident:Problem:INCIDENT_PROBLEM",
            ["Relationships:Allowed:1"] = "Problem:Change:PROBLEM_CHANGE",
            ["WorkflowRouting:Triage:RuleCode"] = "PROBLEM-TRIAGE",
            ["WorkflowRouting:Triage:DepartmentId"] = "1",
            ["WorkflowRouting:Triage:TeamId"] = "2"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<IContentScanner>(_ => new DeterministicContentScanner());
        if (accessState is not null)
        {
            services.AddSingleton(accessState);
            services.AddScoped<ITicketAccessAuthorizer, TestTicketAccessAuthorizer>();
        }
        else if (restricted) services.AddScoped<ITicketAccessAuthorizer, TestTicketAccessAuthorizer>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class TicketAccessTestState
    {
        public long? DeniedTicketId { get; set; }
    }

    private sealed class TestTicketAccessAuthorizer(TicketAccessTestState? state = null) : ITicketAccessAuthorizer
    {
        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId, ticket);
        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId, ticket);
        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => actorUserId == 1001 ? Task.CompletedTask : throw new UnauthorizedAccessException("Ticket access denied.");
        private Task Ensure(long actorUserId, Ticket ticket) =>
            actorUserId == 1001 && ticket.Id != state?.DeniedTicketId
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
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_PROBLEM\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
    }
}