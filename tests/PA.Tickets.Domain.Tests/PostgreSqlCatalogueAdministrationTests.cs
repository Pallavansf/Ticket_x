using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlCatalogueAdministrationTests
{
    [Fact]
    public async Task Catalogue_hierarchy_fields_publication_and_audit_persist_in_postgresql()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"SVC-{Guid.NewGuid():N}";
        var categoryCode = $"CAT-{Guid.NewGuid():N}";
        var requestCode = $"REQ-{Guid.NewGuid():N}";
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            var createdService = await service.CreateServiceAsync(9001, code, "Catalogue Service", null, 501, 601, now, null, CancellationToken.None);
            var category = await service.CreateCategoryAsync(9001, createdService.Id, categoryCode, "Catalogue Category", null, now, null, CancellationToken.None);
            var requestType = await service.CreateRequestTypeAsync(9001, createdService.Id, category.Id, 1, requestCode, "Catalogue Request", null, null, 501, 601, null, null, null, true, 1, 1, now, now.AddHours(1), CancellationToken.None);
            var field = await service.CreateFieldAsync(9001, $"FIELD-{Guid.NewGuid():N}", "Environment", "SELECT", null, now, null, CancellationToken.None);
            var option = await service.AddFieldOptionAsync(9001, field.Id, "PROD", "Production", 1, CancellationToken.None);
            var association = await service.AssociateFieldAsync(9001, requestType.Id, field.Id, true, 1, CancellationToken.None);
            var numberField = await service.CreateFieldAsync(9001, $"FIELD-{Guid.NewGuid():N}", "Units", "NUMBER", null, now, null, CancellationToken.None);
            await service.AssociateFieldAsync(9001, requestType.Id, numberField.Id, true, 2, CancellationToken.None);

            Assert.Equal(field.Id, option.FieldId);
            Assert.True(association.Required);
            Assert.Equal(category.Id, (await service.GetRequestTypeAsync(9001, requestType.Id, CancellationToken.None)).CategoryId);

            var published = await service.PublishRequestTypeAsync(9001, requestType.Id, CancellationToken.None);
            Assert.Equal("PUBLISHED", published.Status);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRequestTypeAsync(9001, requestType.Id, createdService.Id, category.Id, 1, requestCode, "Changed", null, null, 501, 601, null, null, null, true, 1, now, null, CancellationToken.None));

            var runtime = scope.ServiceProvider.GetRequiredService<ITicketCatalogueRuntime>();
            await Assert.ThrowsAsync<ArgumentException>(() => runtime.ResolveForCreationAsync(9001, null, now,
                new[] { new TicketFieldValue(field.Id, "PROD", null, null, null) }, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => runtime.ResolveForCreationAsync(9001, requestType.Id, now,
                new[]
                {
                    new TicketFieldValue(field.Id, "PROD", null, null, null),
                    new TicketFieldValue(numberField.Id, "42", null, null, null)
                }, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => runtime.ResolveForCreationAsync(9001, requestType.Id, now,
                new[]
                {
                    new TicketFieldValue(field.Id, "UNKNOWN", null, null, null),
                    new TicketFieldValue(numberField.Id, null, 42, null, null)
                }, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => runtime.ResolveForCreationAsync(9001, requestType.Id, now,
                new[]
                {
                    new TicketFieldValue(field.Id, "PROD", null, null, null),
                    new TicketFieldValue(numberField.Id, null, 42, null, "unexpected")
                }, CancellationToken.None));

            var tickets = scope.ServiceProvider.GetRequiredService<PA.Tickets.Application.Tickets.ITicketLifecycleService>();
            await Assert.ThrowsAsync<ArgumentException>(() => tickets.CreateAsync(new PA.Tickets.Application.Tickets.CreateTicketCommand(PA.Tickets.Domain.Tickets.TicketType.Incident, "Missing field", null, 9001, 9001, requestType.Id), CancellationToken.None));
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var ticketsBeforeInvalidSubmission = await db.Tickets.CountAsync();
            var noRequestTypeKey = $"catalogue-no-type-{Guid.NewGuid():N}";
            await Assert.ThrowsAsync<ArgumentException>(() => tickets.CreateAsync(new PA.Tickets.Application.Tickets.CreateTicketCommand(PA.Tickets.Domain.Tickets.TicketType.Incident, "Field without request type", null, 9001, 9001, FieldValues: new[] { new TicketFieldValue(field.Id, "PROD", null, null, null) }, IdempotencyKey: noRequestTypeKey), CancellationToken.None));
            Assert.Equal(ticketsBeforeInvalidSubmission, await db.Tickets.CountAsync());
            var failedNoTypeRecord = await db.Set<PA.Tickets.Infrastructure.Execution.IdempotencyRow>().SingleAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == noRequestTypeKey);
            Assert.Equal("FAILED", failedNoTypeRecord.Status);
            Assert.Null(failedNoTypeRecord.ResultTicketId);

            var wrongTypeKey = $"catalogue-wrong-type-{Guid.NewGuid():N}";
            await Assert.ThrowsAsync<ArgumentException>(() => tickets.CreateAsync(new PA.Tickets.Application.Tickets.CreateTicketCommand(PA.Tickets.Domain.Tickets.TicketType.Incident, "Number as text", null, 9001, 9001, requestType.Id, new[]
            {
                new TicketFieldValue(field.Id, "PROD", null, null, null),
                new TicketFieldValue(numberField.Id, "42", null, null, null)
            }, wrongTypeKey), CancellationToken.None));
            Assert.Equal(ticketsBeforeInvalidSubmission, await db.Tickets.CountAsync());
            var failedWrongTypeRecord = await db.Set<PA.Tickets.Infrastructure.Execution.IdempotencyRow>().SingleAsync(x => x.Scope == "TICKET_CREATE" && x.ActorId == 9001 && x.Key == wrongTypeKey);
            Assert.Equal("FAILED", failedWrongTypeRecord.Status);
            Assert.Null(failedWrongTypeRecord.ResultTicketId);

            var createdTicket = await tickets.CreateAsync(new PA.Tickets.Application.Tickets.CreateTicketCommand(PA.Tickets.Domain.Tickets.TicketType.Incident, "Configured ticket", null, 9001, 9001, requestType.Id, new[]
            {
                new TicketFieldValue(field.Id, "PROD", null, null, null),
                new TicketFieldValue(numberField.Id, null, 42, null, null)
            }), CancellationToken.None);
            var ticketRow = await db.Tickets.SingleAsync(x => x.Id == createdTicket.Id);
            Assert.Equal(1, ticketRow.TypeId);
            Assert.Equal(createdService.Id, ticketRow.ServiceId);
            Assert.Equal(requestType.Id, ticketRow.RequestTypeId);
            Assert.Equal(1, db.Entry(ticketRow).Property<int?>("RequestTypeVersion").CurrentValue);
            Assert.True(await db.Set<PA.Tickets.Infrastructure.Foundations.TicketFieldValueRow>().AnyAsync(x => x.TicketId == createdTicket.Id && x.FieldId == field.Id && x.StringValue == "PROD"));
            Assert.True(await db.Set<PA.Tickets.Infrastructure.Foundations.TicketFieldValueRow>().AnyAsync(x => x.TicketId == createdTicket.Id && x.FieldId == numberField.Id && x.NumberValue == 42));
            var versionTwo = await service.CreateRequestTypeAsync(9001, createdService.Id, category.Id, 1, requestCode, "Catalogue Request v2", null, null, 501, 601, null, null, null, false, 0, 2, now.AddHours(1), null, CancellationToken.None);
            await service.PublishRequestTypeAsync(9001, versionTwo.Id, CancellationToken.None);
            db.ChangeTracker.Clear();
            var historicalTicket = await db.Tickets.SingleAsync(x => x.Id == createdTicket.Id);
            Assert.Equal(1, db.Entry(historicalTicket).Property<int?>("RequestTypeVersion").CurrentValue);
            Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "CATALOGUE" && x.EntityId == requestType.Id.ToString() && x.Action == "REQUEST_TYPE_PUBLISHED"));
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_REQUEST_FIELD\" where \"N_REQUEST_TYPE_ID\" in (select \"N_REQUEST_TYPE_ID\" from \"PA_TICKETS_REQUEST_TYPE\" where \"S_REQUEST_TYPE_CODE\" = {requestCode})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_FIELD_OPTION\" where \"N_FIELD_ID\" in (select \"N_FIELD_ID\" from \"PA_TICKETS_FIELD\" where \"S_FIELD_CODE\" like 'FIELD-%')");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_REQUEST_TYPE\" where \"S_REQUEST_TYPE_CODE\" = {requestCode}");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_CATEGORY\" where \"S_CATEGORY_CODE\" = {categoryCode}");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_SERVICE\" where \"S_SERVICE_CODE\" = {code}");
        }
    }

    [Fact]
    public async Task Catalogue_permissions_are_centralized_and_publication_is_denied_without_permission()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListServicesAsync(1001, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateServiceAsync(1001, "DENIED", "Denied", null, null, null, DateTimeOffset.UtcNow, null, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateServiceAsync(9999, "AUDITOR-DENIED", "Denied", null, null, null, DateTimeOffset.UtcNow, null, CancellationToken.None));
        var tickets = scope.ServiceProvider.GetRequiredService<PA.Tickets.Application.Tickets.ITicketLifecycleService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tickets.CreateAsync(new PA.Tickets.Application.Tickets.CreateTicketCommand(PA.Tickets.Domain.Tickets.TicketType.Request, "Denied ticket", null, 1001, 1001, 999999), CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_service_creation_preserves_unique_configuration()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"CONCURRENT-{Guid.NewGuid():N}";
        var tasks = Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
            try { return (Success: true, Value: await service.CreateServiceAsync(9001, code, "Concurrent", null, null, null, DateTimeOffset.UtcNow, null, CancellationToken.None)); }
            catch (Exception) { return (Success: false, Value: (CatalogueService?)null); }
        });
        var results = await Task.WhenAll(tasks);
        Assert.Single(results, x => x.Success);
        Assert.Single(results, x => !x.Success);
    }

    [Fact]
    public async Task Request_type_executes_existing_workflow_and_sla_runtimes()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"RUNTIME-{Guid.NewGuid():N}";
        var workflowCode = $"WF-{Guid.NewGuid():N}";
        var calendarCode = $"CAL-{Guid.NewGuid():N}";
        var policyCode = $"POL-{Guid.NewGuid():N}";
        await using var scope = provider.CreateAsyncScope();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
        var execution = scope.ServiceProvider.GetRequiredService<PA.Tickets.Infrastructure.Execution.ExecutionDbContext>();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var runtimeBaseId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 10;
        var workflowId = runtimeBaseId + 1;
        var workflowVersionId = runtimeBaseId + 2;
        var workflowStepId = runtimeBaseId + 3;
        var policyId = runtimeBaseId + 10;
        var policyVersionId = runtimeBaseId + 11;
        var calendarId = runtimeBaseId + 12;

        execution.WorkflowDefinitions.Add(new PA.Tickets.Infrastructure.Execution.WorkflowDefinitionRow { Id = workflowId, Code = workflowCode, Name = "Catalogue Runtime" });
        execution.WorkflowVersions.Add(new PA.Tickets.Infrastructure.Execution.WorkflowVersionRow { Id = workflowVersionId, WorkflowId = workflowId, VersionNo = 1, Status = "PUBLISHED", Definition = $"{{\"effectiveFrom\":\"{now:O}\",\"effectiveTo\":null}}" });
        execution.WorkflowStepDefinitions.Add(new PA.Tickets.Infrastructure.Execution.WorkflowStepDefinitionRow { Id = workflowStepId, VersionId = workflowVersionId, StepNo = 1, Code = "WORK", Name = "Work", Type = "WORK", ExecutionMode = "SEQUENTIAL" });
        execution.SlaCalendars.Add(new PA.Tickets.Infrastructure.Execution.SlaCalendarRow { Id = calendarId, Code = calendarCode, Active = true, TimeZoneId = "UTC", WorkingHoursJson = "{\"Monday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Tuesday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Wednesday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Thursday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Friday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Saturday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"},\"Sunday\":{\"Start\":\"00:00:00\",\"End\":\"23:59:00\"}}", HolidaysJson = "[]" });
        execution.SlaPolicies.Add(new PA.Tickets.Infrastructure.Execution.SlaPolicyRow { Id = policyVersionId, PolicyId = policyId, Code = policyCode, VersionNo = 1, Active = false, Status = "DRAFT", EffectiveFrom = now, CalendarCode = calendarCode, ResponseTargetMinutes = 60, ResolutionTargetMinutes = 120 });
        await execution.SaveChangesAsync();

        var service = await catalogue.CreateServiceAsync(9001, code, "Runtime Service", null, null, null, now, null, CancellationToken.None);
        var category = await catalogue.CreateCategoryAsync(9001, service.Id, $"CAT-{code}", "Runtime Category", null, now, null, CancellationToken.None);
        var requestType = await catalogue.CreateRequestTypeAsync(9001, service.Id, category.Id, 1, $"REQ-{code}", "Runtime Request", null, null, 501, 601, workflowId, policyId, 1, false, 0, 1, now, null, CancellationToken.None);
        await catalogue.PublishRequestTypeAsync(9001, requestType.Id, CancellationToken.None);
        var runtimePolicy = await execution.SlaPolicies.SingleAsync(x => x.Id == policyVersionId);
        runtimePolicy.ServiceId = service.Id;
        runtimePolicy.RequestTypeId = requestType.Id;
        runtimePolicy.Active = true;
        runtimePolicy.Status = "PUBLISHED";
        await execution.SaveChangesAsync();

        var tickets = scope.ServiceProvider.GetRequiredService<PA.Tickets.Application.Tickets.ITicketLifecycleService>();
        var created = await tickets.CreateAsync(new PA.Tickets.Application.Tickets.CreateTicketCommand(PA.Tickets.Domain.Tickets.TicketType.Incident, "Runtime ticket", null, 9001, 9001, requestType.Id), CancellationToken.None);
        Assert.Equal(1, await execution.WorkflowInstances.CountAsync(x => x.TicketId == created.Id && x.VersionId == workflowVersionId));
        var instance = await execution.WorkflowInstances.SingleAsync(x => x.TicketId == created.Id);
        Assert.True(await execution.WorkflowSteps.AnyAsync(x => x.WorkflowInstanceId == instance.Id && x.WorkflowStepId == workflowStepId));
        Assert.True(await execution.WorkItems.AnyAsync(x => x.TicketId == created.Id && x.ToDepartmentId == 501 && x.ToTeamId == 601));
        Assert.True(await execution.Slas.AnyAsync(x => x.TicketId == created.Id && x.PolicyId == policyId && x.PolicyVersion == 1));
    }

    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" }).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config); services.AddApplication(); services.AddInfrastructure(config); return services.BuildServiceProvider(validateScopes: true);
    }
}
