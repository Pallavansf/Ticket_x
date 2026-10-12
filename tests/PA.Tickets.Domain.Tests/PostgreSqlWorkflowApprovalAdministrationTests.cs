using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Approvals;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Domain.Approvals;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Domain.Workflow;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlWorkflowApprovalAdministrationTests
{
    [Fact]
    public async Task Published_workflow_and_approval_versions_flow_through_request_type_and_existing_runtimes()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var (service, category, requestTypeId, workflowId, workflowVersionId, approvalRuleId, approvalVersionId) = await BuildPublishedConfigurationAsync(provider);
        var ticketId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var created = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Pinned workflow and approval", null, 9001, 9001, requestTypeId), CancellationToken.None);
            ticketId = created.Id;
            var execution = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            var workflowInstance = await execution.WorkflowInstances.SingleAsync(x => x.TicketId == created.Id);
            Assert.Equal(workflowVersionId, workflowInstance.VersionId);
            Assert.True(await execution.WorkItems.AnyAsync(x => x.TicketId == created.Id && x.ToDepartmentId == 501 && x.ToTeamId == 601));

            var approvalInstance = await execution.ApprovalInstances.SingleAsync(x => x.TicketId == created.Id);
            Assert.Equal("SEQUENTIAL", approvalInstance.Mode);
            Assert.Equal(approvalVersionId, execution.Entry(approvalInstance).Property<long?>("ApprovalRuleVersionId").CurrentValue);
            Assert.True(execution.Entry(approvalInstance).Property<bool>("DelegationAllowed").CurrentValue);
            var approvals = await execution.Approvals.Where(x => x.ApprovalInstanceId == approvalInstance.Id).OrderBy(x => x.Level).ToListAsync();
            Assert.Equal(new long?[] { 1002, 1003 }, approvals.Select(x => x.ApproverId).ToArray());
            Assert.All(approvals, x => Assert.NotNull(x.ExpiresAt));

            var workflowAdmin = scope.ServiceProvider.GetRequiredService<IWorkflowConfigurationRepository>();
            var approvalAdmin = scope.ServiceProvider.GetRequiredService<IApprovalRuleConfigurationRepository>();
            var workflowHistory = await workflowAdmin.GetTicketRuntimeHistoryAsync(9999, created.Id, CancellationToken.None);
            var approvalHistory = await approvalAdmin.GetTicketRuntimeHistoryAsync(9999, created.Id, CancellationToken.None);
            Assert.Single(workflowHistory);
            Assert.Equal(workflowVersionId, workflowHistory.Single().WorkflowVersionId);
            Assert.Single(approvalHistory);
            Assert.Equal(approvalVersionId, approvalHistory.Single().RuleVersionId);
            var historyEntry = approvalHistory.Single().Approvals.FirstOrDefault();
            Assert.NotNull(historyEntry);
            Assert.Equal(approvalInstance.Id, historyEntry!.History.FirstOrDefault()?.ApprovalId is long approvalId
                ? await execution.Approvals.Where(x => x.Id == approvalId).Select(x => x.ApprovalInstanceId).FirstAsync() is long instanceId
                    ? (await execution.ApprovalInstances.SingleAsync(x => x.Id == instanceId)).Id
                    : 0
                : approvalInstance.Id);

            var requestType = await scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>().GetRequestTypeAsync(9001, requestTypeId, CancellationToken.None);
            Assert.Equal(workflowVersionId, requestType.WorkflowVersionId);
            Assert.Equal(approvalVersionId, requestType.ApprovalRuleVersionId);
            Assert.True(await execution.Audits.AnyAsync(x => x.EntityType == "WORKFLOW_VERSION" && x.EntityId == workflowVersionId.ToString() && x.Action == "WORKFLOW_PUBLISHED"));
            Assert.True(await execution.Audits.AnyAsync(x => x.EntityType == "APPROVAL_RULE_VERSION" && x.EntityId == approvalVersionId.ToString() && x.Action == "APPROVAL_RULE_VERSION_PUBLISHED"));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workflowAdmin.CreateWorkflowAsync(9999, "AUDITOR-DENIED", "Denied", null, "auditor-denied-workflow", null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => approvalAdmin.CreateRuleAsync(9999, "AUDITOR-DENIED", "Denied", null, "auditor-denied-approval", null, CancellationToken.None));
        }
        finally
        {
            await CleanupTicketAsync(provider, ticketId);
            await CleanupConfigurationAsync(provider, service.Id, category.Id, requestTypeId, workflowId, workflowVersionId, approvalRuleId, approvalVersionId);
        }
    }

    [Fact]
    public async Task Workflow_and_approval_configuration_validate_immutability_and_auditor_read_access()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var workflowId = 0L; var workflowVersionId = 0L; var approvalRuleId = 0L; var approvalVersionId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var workflowAdmin = scope.ServiceProvider.GetRequiredService<IWorkflowConfigurationRepository>();
            var approvalAdmin = scope.ServiceProvider.GetRequiredService<IApprovalRuleConfigurationRepository>();
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            workflowId = await workflowAdmin.CreateWorkflowAsync(9001, $"WF-{Guid.NewGuid():N}", "Administration test", null, $"wf-create-{Guid.NewGuid():N}", "workflow-test", CancellationToken.None);
            var invalidWorkflow = new WorkflowVersion(0, workflowId, 1, [new("START", "Start", WorkflowStepType.Work)], [new("BROKEN", "START", "MISSING")], now, null);
            Assert.False((await workflowAdmin.ValidateAsync(9001, workflowId, invalidWorkflow, CancellationToken.None)).IsValid);

            var workflowVersion = new WorkflowVersion(0, workflowId, 1, [new("START", "Start", WorkflowStepType.Work, DepartmentId: 501, TeamId: 601)], [], now, now.AddDays(3));
            workflowVersionId = await workflowAdmin.SaveDraftAsync(workflowId, workflowVersion, 9001, $"wf-draft-{Guid.NewGuid():N}", CancellationToken.None);
            await workflowAdmin.PublishAsync(workflowVersionId, 9001, $"wf-pub-{Guid.NewGuid():N}", CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => workflowAdmin.SaveDraftAsync(workflowId, new WorkflowVersion(workflowVersionId, workflowId, 1, [new("START", "Changed", WorkflowStepType.Work)], [], now, now.AddDays(3)), 9001, $"wf-edit-{Guid.NewGuid():N}", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => workflowAdmin.SaveDraftAsync(workflowId, new WorkflowVersion(0, workflowId, 2, [new("START", "Overlap", WorkflowStepType.Work)], [], now, now.AddDays(1)), 9001, $"wf-overlap-{Guid.NewGuid():N}", CancellationToken.None).ContinueWith(async task => { var secondId = await task; await workflowAdmin.PublishAsync(secondId, 9001, $"wf-overlap-pub-{Guid.NewGuid():N}", CancellationToken.None); }).Unwrap());

            approvalRuleId = await approvalAdmin.CreateRuleAsync(9001, $"APR-{Guid.NewGuid():N}", "Sequential approval", null, $"apr-create-{Guid.NewGuid():N}", "approval-test", CancellationToken.None);
            var invalidApproval = new ApprovalRuleVersion(0, approvalRuleId, 1, ApprovalMode.Sequential, "REJECT", 60, true, true, true, true, now, null, [new(1, ApprovalApproverType.User, 0)]);
            Assert.False(invalidApproval.Validate().IsValid);
            var ruleVersion = new ApprovalRuleVersion(0, approvalRuleId, 1, ApprovalMode.Sequential, "REJECT", 60, true, true, true, true, now, null,
                [new(1, ApprovalApproverType.User, 1002), new(2, ApprovalApproverType.User, 1003)]);
            approvalVersionId = await approvalAdmin.SaveDraftAsync(approvalRuleId, ruleVersion, 9001, $"apr-draft-{Guid.NewGuid():N}", "approval-test", CancellationToken.None);
            await approvalAdmin.PublishAsync(approvalVersionId, 9001, $"apr-pub-{Guid.NewGuid():N}", "approval-test", CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => approvalAdmin.SaveDraftAsync(approvalRuleId, new ApprovalRuleVersion(approvalVersionId, approvalRuleId, 1, ApprovalMode.AnyOne, "REJECT", null, true, true, true, true, now, null, [new(1, ApprovalApproverType.User, 1002)]), 9001, $"apr-edit-{Guid.NewGuid():N}", null, CancellationToken.None));
            Assert.Equal(2, (await approvalAdmin.GetVersionAsync(9999, approvalVersionId, CancellationToken.None)).Steps.Count);
            Assert.Single((await workflowAdmin.GetVersionAsync(9999, workflowVersionId, CancellationToken.None)).Steps);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => approvalAdmin.PublishAsync(approvalVersionId, 9999, $"auditor-publish-{Guid.NewGuid():N}", null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workflowAdmin.RetireAsync(workflowVersionId, 9999, $"auditor-retire-{Guid.NewGuid():N}", "denied", null, CancellationToken.None));

            await workflowAdmin.RetireAsync(workflowVersionId, 9001, $"wf-retire-{Guid.NewGuid():N}", "test retirement", "workflow-test", CancellationToken.None);
            await approvalAdmin.RetireAsync(approvalVersionId, 9001, $"apr-retire-{Guid.NewGuid():N}", "test retirement", "approval-test", CancellationToken.None);
            Assert.Contains(await scope.ServiceProvider.GetRequiredService<ExecutionDbContext>().Audits.AsNoTracking().Where(x => x.EntityId == workflowVersionId.ToString()).Select(x => x.Action).ToListAsync(), x => x == "WORKFLOW_VERSION_RETIRED");
            Assert.Contains(await scope.ServiceProvider.GetRequiredService<ExecutionDbContext>().Audits.AsNoTracking().Where(x => x.EntityId == approvalVersionId.ToString()).Select(x => x.Action).ToListAsync(), x => x == "APPROVAL_RULE_VERSION_RETIRED");
        }
        finally
        {
            await CleanupConfigurationAsync(provider, 0, 0, 0, workflowId, workflowVersionId, approvalRuleId, approvalVersionId);
        }
    }

    [Fact]
    public async Task Approval_instance_creation_enforces_ticket_scope_requester_identity_and_configured_approvers()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var configuration = await BuildPublishedConfigurationAsync(provider);
        var ticketId = 0L;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var lifecycle = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                ticketId = (await lifecycle.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Approval boundary", null, 9001, 9001, configuration.RequestTypeId), CancellationToken.None)).Id;
            }

            await using var approvalScope = provider.CreateAsyncScope();
            var approvals = approvalScope.ServiceProvider.GetRequiredService<IApprovalExecutionRepository>();
            var validApprovers = new[]
            {
                new DurableApprovalApprover(1002, 1, true),
                new DurableApprovalApprover(1003, 2, true)
            };
            DurableApprovalInstanceRequest Request(long requester, long creator, IReadOnlyCollection<DurableApprovalApprover> approvers) =>
                new(ticketId, null, DurableApprovalMode.Sequential, approvers, requester, creator, null, "REJECT", configuration.ApprovalVersionId, 30, true);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                approvals.CreateInstanceAsync(Request(9001, 9999, validApprovers), 9999, $"approval-boundary-auditor-{Guid.NewGuid():N}", null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                approvals.CreateInstanceAsync(Request(9001, 2001, validApprovers), 9001, $"approval-boundary-creator-{Guid.NewGuid():N}", null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                approvals.CreateInstanceAsync(Request(2001, 9001, validApprovers), 9001, $"approval-boundary-requester-{Guid.NewGuid():N}", null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                approvals.CreateInstanceAsync(Request(9001, 9001, [new DurableApprovalApprover(1004, 1, true)]), 9001, $"approval-boundary-approver-{Guid.NewGuid():N}", null, CancellationToken.None));
        }
        finally
        {
            await CleanupTicketAsync(provider, ticketId);
            await CleanupConfigurationAsync(provider, configuration.Service.Id, configuration.Category.Id, configuration.RequestTypeId, configuration.WorkflowId, configuration.WorkflowVersionId, configuration.ApprovalRuleId, configuration.ApprovalVersionId);
        }
    }

    private static async Task<(CatalogueService Service, CatalogueCategory Category, long RequestTypeId, long WorkflowId, long WorkflowVersionId, long ApprovalRuleId, long ApprovalVersionId)> BuildPublishedConfigurationAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        var workflowAdmin = scope.ServiceProvider.GetRequiredService<IWorkflowConfigurationRepository>();
        var approvalAdmin = scope.ServiceProvider.GetRequiredService<IApprovalRuleConfigurationRepository>();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
        var workflowId = await workflowAdmin.CreateWorkflowAsync(9001, $"WF-{Guid.NewGuid():N}", "Pinned workflow", null, $"wf-create-{Guid.NewGuid():N}", "catalogue-pin", CancellationToken.None);
        var workflow = new WorkflowVersion(0, workflowId, 1, [new("WORK", "Route work", WorkflowStepType.Work, DepartmentId: 501, TeamId: 601)], [], now, now.AddDays(7));
        var workflowVersionId = await workflowAdmin.SaveDraftAsync(workflowId, workflow, 9001, $"wf-draft-{Guid.NewGuid():N}", CancellationToken.None);
        await workflowAdmin.PublishAsync(workflowVersionId, 9001, $"wf-pub-{Guid.NewGuid():N}", CancellationToken.None);
        var ruleId = await approvalAdmin.CreateRuleAsync(9001, $"APR-{Guid.NewGuid():N}", "Pinned approval", null, $"apr-create-{Guid.NewGuid():N}", "catalogue-pin", CancellationToken.None);
        var ruleVersion = new ApprovalRuleVersion(0, ruleId, 1, ApprovalMode.Sequential, "REJECT", 30, true, true, true, true, now, now.AddDays(7),
            [new(1, ApprovalApproverType.User, 1002), new(2, ApprovalApproverType.User, 1003)]);
        var approvalVersionId = await approvalAdmin.SaveDraftAsync(ruleId, ruleVersion, 9001, $"apr-draft-{Guid.NewGuid():N}", "catalogue-pin", CancellationToken.None);
        await approvalAdmin.PublishAsync(approvalVersionId, 9001, $"apr-pub-{Guid.NewGuid():N}", "catalogue-pin", CancellationToken.None);
        var service = await catalogue.CreateServiceAsync(9001, $"SVC-{Guid.NewGuid():N}", "Workflow approval service", null, null, null, now, null, CancellationToken.None);
        var category = await catalogue.CreateCategoryAsync(9001, service.Id, $"CAT-{Guid.NewGuid():N}", "Workflow approval category", null, now, null, CancellationToken.None);
        var requestType = await catalogue.CreateRequestTypeAsync(9001, service.Id, category.Id, (int)TicketType.Incident, $"REQ-{Guid.NewGuid():N}", "Workflow approval request", null, null, 501, 601, workflowId, null, null, false, 0, 1, now, now.AddDays(3), CancellationToken.None, null, workflowVersionId, approvalVersionId);
        await catalogue.PublishRequestTypeAsync(9001, requestType.Id, CancellationToken.None);
        return (service, category, requestType.Id, workflowId, workflowVersionId, ruleId, approvalVersionId);
    }

    private static async Task CleanupTicketAsync(ServiceProvider provider, long ticketId)
    {
        if (ticketId == 0) return;
        await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_APPROVAL_DELEGATION\" where \"N_APPROVAL_ID\" in (select \"N_APPROVAL_ID\" from \"PA_TICKETS_APPROVAL\" where \"N_TICKET_ID\" = {ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_APPROVAL_HISTORY\" where \"N_APPROVAL_ID\" in (select \"N_APPROVAL_ID\" from \"PA_TICKETS_APPROVAL\" where \"N_TICKET_ID\" = {ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_APPROVAL\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_APPROVAL_INSTANCE\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY\" where \"N_STEP_INSTANCE_ID\" in (select s.\"N_STEP_INSTANCE_ID\" from \"PA_TICKETS_WORKFLOW_STEP_INSTANCE\" s join \"PA_TICKETS_WORKFLOW_INSTANCE\" i on i.\"N_WORKFLOW_INSTANCE_ID\" = s.\"N_WORKFLOW_INSTANCE_ID\" where i.\"N_TICKET_ID\" = {ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_ASSIGNMENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORK_ITEM\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORKFLOW_STEP_INSTANCE\" where \"N_WORKFLOW_INSTANCE_ID\" in (select \"N_WORKFLOW_INSTANCE_ID\" from \"PA_TICKETS_WORKFLOW_INSTANCE\" where \"N_TICKET_ID\" = {ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORKFLOW_INSTANCE\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
        var ticketsDb = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        await ticketsDb.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
    }

    private static async Task CleanupConfigurationAsync(ServiceProvider provider,long serviceId,long categoryId,long requestTypeId,long workflowId,long workflowVersionId,long approvalRuleId,long approvalVersionId)
    {
        await using var scope=provider.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();var catalogue=scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        if(requestTypeId>0)await catalogue.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_REQUEST_TYPE\" where \"N_REQUEST_TYPE_ID\" = {requestTypeId}");
        if(categoryId>0)await catalogue.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_CATEGORY\" where \"N_CATEGORY_ID\" = {categoryId}");
        if(serviceId>0)await catalogue.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_SERVICE\" where \"N_SERVICE_ID\" = {serviceId}");
        if(approvalVersionId>0){await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_APPROVAL_RULE_STEP\" where \"N_APPROVAL_RULE_VERSION_ID\" = {approvalVersionId}");await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_APPROVAL_RULE_VERSION\" where \"N_APPROVAL_RULE_VERSION_ID\" = {approvalVersionId}");}
        if(approvalRuleId>0)await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_APPROVAL_RULE\" where \"N_APPROVAL_RULE_ID\" = {approvalRuleId}");
        if(workflowVersionId>0){await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORKFLOW_TRANSITION\" where \"N_WORKFLOW_VERSION_ID\" = {workflowVersionId}");await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORKFLOW_STEP\" where \"N_WORKFLOW_VERSION_ID\" = {workflowVersionId}");await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORKFLOW_VERSION\" where \"N_WORKFLOW_VERSION_ID\" = {workflowVersionId}");}
        if(workflowId>0)await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_WORKFLOW\" where \"N_WORKFLOW_ID\" = {workflowId}");
    }

    private static ServiceProvider BuildProvider(){var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Database:Provider","PostgreSQL"}}).Build();var services=new ServiceCollection();services.AddSingleton<IConfiguration>(config);services.AddApplication();services.AddInfrastructure(config);return services.BuildServiceProvider(validateScopes:true);}
}
