using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlCatalogueSubCategoryTests
{
    [Fact]
    public async Task Catalogue_paths_resolve_validate_persist_and_preserve_published_versions()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var (serviceA, categoryA) = await CreateBaseAsync(provider);
        long serviceBId = 0;
        try
        {
            await using var setupScope = provider.CreateAsyncScope();
            var catalogue = setupScope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
            var now = DateTimeOffset.UtcNow.AddMinutes(-2);
            var serviceB = await catalogue.CreateServiceAsync(9001, $"SVC-{Guid.NewGuid():N}", "Sub-category service B", null, null, null, now, null, CancellationToken.None);
            serviceBId = serviceB.Id;
            var categoryB = await catalogue.CreateCategoryAsync(9001, serviceB.Id, $"CAT-{Guid.NewGuid():N}", "Category B", null, now, null, CancellationToken.None);
            var subCategoryA = await catalogue.CreateSubCategoryAsync(9001, serviceA.Id, categoryA.Id, $"SUB-{Guid.NewGuid():N}", "Sub-category A", null, now, null, CancellationToken.None);
            var subCategoryB = await catalogue.CreateSubCategoryAsync(9001, serviceB.Id, categoryB.Id, $"SUB-{Guid.NewGuid():N}", "Sub-category B", null, now, null, CancellationToken.None);

            var ancestryDb = setupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            ancestryDb.CatalogueRequestTypes.Add(new PA.Tickets.Infrastructure.Foundations.CatalogueRequestTypeRow { ServiceId = serviceB.Id, CategoryId = categoryA.Id, TicketType = 1, Code = $"DB-BAD-CAT-{Guid.NewGuid():N}", Name = "Invalid category ancestry", VersionNo = 1, Status = "DRAFT", EffectiveFrom = now });
            await Assert.ThrowsAsync<DbUpdateException>(() => ancestryDb.SaveChangesAsync());
            ancestryDb.ChangeTracker.Clear();
            ancestryDb.CatalogueRequestTypes.Add(new PA.Tickets.Infrastructure.Foundations.CatalogueRequestTypeRow { ServiceId = serviceA.Id, CategoryId = categoryA.Id, SubCategoryId = subCategoryB.Id, TicketType = 1, Code = $"DB-BAD-SUB-{Guid.NewGuid():N}", Name = "Invalid sub-category ancestry", VersionNo = 1, Status = "DRAFT", EffectiveFrom = now });
            await Assert.ThrowsAsync<DbUpdateException>(() => ancestryDb.SaveChangesAsync());
            ancestryDb.ChangeTracker.Clear();

            await Assert.ThrowsAsync<InvalidOperationException>(() => catalogue.CreateSubCategoryAsync(9001, serviceB.Id, categoryA.Id, "WRONG-SERVICE", "Wrong service", null, now, null, CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => catalogue.CreateRequestTypeAsync(9001, serviceB.Id, categoryA.Id, (int)TicketType.Incident, "WRONG-CATEGORY", "Wrong category", null, null, null, null, null, null, null, false, 0, 1, now, null, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalogue.CreateRequestTypeAsync(9001, serviceB.Id, categoryB.Id, (int)TicketType.Incident, "WRONG-SUBCATEGORY", "Wrong sub-category", null, null, null, null, null, null, null, false, 0, 1, now, null, CancellationToken.None, subCategoryA.Id));

            var directType = await CreateRequestTypeAsync(catalogue, serviceA.Id, categoryA.Id, null, $"DIRECT-{Guid.NewGuid():N}", 1, now, null);
            var nestedType = await CreateRequestTypeAsync(catalogue, serviceA.Id, categoryA.Id, subCategoryA.Id, $"NESTED-{Guid.NewGuid():N}", 1, now, now.AddHours(1));
            Assert.Null(directType.SubCategoryId);
            Assert.Equal(subCategoryA.Id, nestedType.SubCategoryId);
            Assert.Single(await catalogue.ListSubCategoriesAsync(9001, serviceA.Id, categoryA.Id, CancellationToken.None));
            Assert.Contains(await catalogue.ListRequestTypesAsync(9001, categoryA.Id, CancellationToken.None, serviceA.Id), x => x.Id == directType.Id);
            Assert.DoesNotContain(await catalogue.ListRequestTypesAsync(9001, categoryA.Id, CancellationToken.None, serviceA.Id), x => x.Id == nestedType.Id);
            Assert.Contains(await catalogue.ListRequestTypesAsync(9001, categoryA.Id, CancellationToken.None, serviceA.Id, subCategoryA.Id), x => x.Id == nestedType.Id);
            await Assert.ThrowsAsync<ArgumentException>(() => catalogue.ListRequestTypesAsync(9001, null, CancellationToken.None, serviceA.Id, subCategoryA.Id));

            var publishedDirect = await catalogue.PublishRequestTypeAsync(9001, directType.Id, CancellationToken.None);
            var publishedNested = await catalogue.PublishRequestTypeAsync(9001, nestedType.Id, CancellationToken.None);
            Assert.Equal("PUBLISHED", publishedDirect.Status);
            Assert.Equal("PUBLISHED", publishedNested.Status);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => catalogue.CreateSubCategoryAsync(9999, serviceA.Id, categoryA.Id, "AUDITOR-DENIED", "Denied", null, now, null, CancellationToken.None));

            var tickets = setupScope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var directTicket = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Direct catalogue path", null, 9001, 9001, directType.Id, ServiceId: serviceA.Id, CategoryId: categoryA.Id), CancellationToken.None);
            var nestedTicket = await tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Nested catalogue path", null, 9001, 9001, nestedType.Id, ServiceId: serviceA.Id, CategoryId: categoryA.Id, SubCategoryId: subCategoryA.Id), CancellationToken.None);
            Assert.Equal(serviceA.Id, directTicket.ServiceId);
            Assert.Equal(categoryA.Id, directTicket.CategoryId);
            Assert.Null(directTicket.SubCategoryId);
            Assert.Equal(subCategoryA.Id, nestedTicket.SubCategoryId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Mismatched catalogue path", null, 9001, 9001, directType.Id, ServiceId: serviceA.Id, CategoryId: categoryA.Id, SubCategoryId: subCategoryA.Id), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Mismatched category", null, 9001, 9001, nestedType.Id, ServiceId: serviceA.Id, CategoryId: categoryB.Id, SubCategoryId: subCategoryB.Id), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Partial catalogue path", null, 9001, 9001, directType.Id, ServiceId: serviceA.Id), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "SubCategory without RequestType", null, 9001, 9001, SubCategoryId: subCategoryA.Id), CancellationToken.None));

            var db = setupScope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            db.ChangeTracker.Clear();
            var storedNested = await db.Tickets.SingleAsync(x => x.Id == nestedTicket.Id);
            Assert.Equal(subCategoryA.Id, db.Entry(storedNested).Property<long?>("SubCategoryId").CurrentValue);
            Assert.Equal(subCategoryA.Id, (await tickets.GetAsync(nestedTicket.Id, 9001, CancellationToken.None)).SubCategoryId);

            var versionTwo = await CreateRequestTypeAsync(catalogue, serviceA.Id, categoryA.Id, subCategoryA.Id, nestedType.Code, 2, now.AddHours(1), null);
            await catalogue.PublishRequestTypeAsync(9001, versionTwo.Id, CancellationToken.None);
            db.ChangeTracker.Clear();
            storedNested = await db.Tickets.SingleAsync(x => x.Id == nestedTicket.Id);
            Assert.Equal(1, db.Entry(storedNested).Property<int?>("RequestTypeVersion").CurrentValue);
            Assert.Equal(subCategoryA.Id, db.Entry(storedNested).Property<long?>("SubCategoryId").CurrentValue);
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalogue.UpdateRequestTypeAsync(9001, nestedType.Id, serviceA.Id, categoryA.Id, (int)TicketType.Incident, nestedType.Code, "Mutated published version", null, null, null, null, null, null, null, false, 0, now, null, CancellationToken.None, subCategoryA.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalogue.UpdateSubCategoryAsync(9001, subCategoryA.Id, serviceA.Id, categoryA.Id, subCategoryA.Code, subCategoryA.Name, null, now, now.AddMinutes(30), CancellationToken.None));
            await catalogue.UpdateSubCategoryAsync(9001, subCategoryA.Id, serviceA.Id, categoryA.Id, subCategoryA.Code, "Updated sub-category A", null, now, null, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Mismatched service", null, 9001, 9001, nestedType.Id, ServiceId: serviceB.Id, CategoryId: categoryA.Id), CancellationToken.None));
            await catalogue.SetRequestTypeActiveAsync(9001, directType.Id, false, CancellationToken.None);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Inactive RequestType", null, 9001, 9001, directType.Id), CancellationToken.None));

            await catalogue.SetSubCategoryActiveAsync(9001, subCategoryA.Id, false, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Inactive path", null, 9001, 9001, nestedType.Id), CancellationToken.None));
            await catalogue.SetSubCategoryActiveAsync(9001, subCategoryA.Id, true, CancellationToken.None);

            await catalogue.SetServiceActiveAsync(9001, serviceB.Id, false, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalogue.CreateSubCategoryAsync(9001, serviceB.Id, categoryB.Id, $"INACTIVE-{Guid.NewGuid():N}", "Inactive parent", null, now, null, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalogue.SetSubCategoryActiveAsync(9001, subCategoryB.Id, true, CancellationToken.None));
            await catalogue.SetServiceActiveAsync(9001, serviceA.Id, false, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.CreateAsync(new CreateTicketCommand(TicketType.Incident, "Inactive service path", null, 9001, 9001, nestedType.Id), CancellationToken.None));
            await catalogue.SetServiceActiveAsync(9001, serviceA.Id, true, CancellationToken.None);

            await Assert.ThrowsAsync<DbUpdateException>(() => catalogue.CreateSubCategoryAsync(9001, serviceA.Id, categoryA.Id, subCategoryA.Code, "Duplicate code", null, now, null, CancellationToken.None));
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<DbUpdateException>(() => catalogue.CreateSubCategoryAsync(9001, serviceA.Id, categoryA.Id, $"SUB-{Guid.NewGuid():N}", "Updated sub-category A", null, now, null, CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Contains(await db.Audits.AsNoTracking().Where(x => x.EntityType == "SUB_CATEGORY" && x.EntityId == subCategoryA.Id.ToString()).Select(x => x.Action).ToListAsync(), x => x == "SUB_CATEGORY_CREATED");
            Assert.Contains(await db.Audits.AsNoTracking().Where(x => x.EntityType == "SUB_CATEGORY" && x.EntityId == subCategoryA.Id.ToString()).Select(x => x.Action).ToListAsync(), x => x == "SUB_CATEGORY_UPDATED");
            Assert.Contains(await db.Audits.AsNoTracking().Where(x => x.EntityType == "SUB_CATEGORY" && x.EntityId == subCategoryA.Id.ToString()).Select(x => x.Action).ToListAsync(), x => x == "SUB_CATEGORY_DEACTIVATED");
            Assert.Contains(await db.Audits.AsNoTracking().Where(x => x.EntityType == "SUB_CATEGORY" && x.EntityId == subCategoryA.Id.ToString()).Select(x => x.Action).ToListAsync(), x => x == "SUB_CATEGORY_ACTIVATED");
            Assert.Contains(await db.Audits.AsNoTracking().Where(x => x.EntityType == "CATALOGUE" && x.EntityId == nestedType.Id.ToString()).Select(x => x.Action).ToListAsync(), x => x == "REQUEST_TYPE_PUBLISHED");
        }
        finally
        {
            await CleanupAsync(provider, serviceA.Id, serviceBId);
        }
    }

    [Fact]
    public async Task Concurrent_duplicate_sub_category_creation_keeps_one_valid_child()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var (service, category) = await CreateBaseAsync(provider);
        var code = $"DUP-{Guid.NewGuid():N}";
        try
        {
            var tasks = Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var scope = provider.CreateAsyncScope();
                var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
                try
                {
                    return await catalogue.CreateSubCategoryAsync(9001, service.Id, category.Id, code, "Concurrent SubCategory", null, DateTimeOffset.UtcNow.AddMinutes(-1), null, CancellationToken.None);
                }
                catch (DbUpdateException)
                {
                    return null;
                }
            });
            var results = await Task.WhenAll(tasks);
            Assert.Single(results, x => x is not null);
            Assert.Single(results, x => x is null);
        }
        finally
        {
            await CleanupAsync(provider, service.Id);
        }
    }

    private static async Task<(CatalogueService Service, CatalogueCategory Category)> CreateBaseAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueAdministrationService>();
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        var service = await catalogue.CreateServiceAsync(9001, $"SVC-{Guid.NewGuid():N}", "Sub-category test service", null, null, null, now, null, CancellationToken.None);
        var category = await catalogue.CreateCategoryAsync(9001, service.Id, $"CAT-{Guid.NewGuid():N}", "Sub-category test category", null, now, null, CancellationToken.None);
        return (service, category);
    }

    private static Task<CatalogueRequestType> CreateRequestTypeAsync(ICatalogueAdministrationService catalogue, long serviceId, long categoryId, long? subCategoryId, string code, int version, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => catalogue.CreateRequestTypeAsync(9001, serviceId, categoryId, (int)TicketType.Incident, code, $"Request {code}", null, null, null, null, null, null, null, false, 0, version, effectiveFrom, effectiveTo, CancellationToken.None, subCategoryId);

    private static async Task CleanupAsync(ServiceProvider provider, params long[] serviceIds)
    {
        foreach (var serviceId in serviceIds.Where(x => x > 0))
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_VALUE\" where \"N_TICKET_ID\" in (select \"N_TICKET_ID\" from \"PA_TICKETS\" where \"N_SERVICE_ID\" = {serviceId})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" in (select \"N_TICKET_ID\" from \"PA_TICKETS\" where \"N_SERVICE_ID\" = {serviceId})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_SERVICE_ID\" = {serviceId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_REQUEST_FIELD\" where \"N_REQUEST_TYPE_ID\" in (select \"N_REQUEST_TYPE_ID\" from \"PA_TICKETS_REQUEST_TYPE\" where \"N_SERVICE_ID\" = {serviceId})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_REQUEST_TYPE\" where \"N_SERVICE_ID\" = {serviceId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_SUB_CATEGORY\" where \"N_CATEGORY_ID\" in (select \"N_CATEGORY_ID\" from \"PA_TICKETS_CATEGORY\" where \"N_SERVICE_ID\" = {serviceId})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_CATEGORY\" where \"N_SERVICE_ID\" = {serviceId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_SERVICE\" where \"N_SERVICE_ID\" = {serviceId}");
        }
    }


    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddApplication();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
