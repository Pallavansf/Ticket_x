using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Sla;
using PA.Tickets.Domain.Sla;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlSlaAdministrationTests
{
    [Fact]
    public async Task Policy_calendar_version_lifecycle_immutability_overlap_audit_and_auditor_access_work()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"SLA-{Guid.NewGuid():N}";
        var calendarCode = $"CAL-{Guid.NewGuid():N}";
        var policyId = 0L;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISlaAdministrationRepository>();
            var now = DateTimeOffset.UtcNow;
            var hours = Enum.GetValues<DayOfWeek>().ToDictionary(day => day, _ => (TimeSpan.FromHours(9), TimeSpan.FromHours(17)));
            var calendar = new BusinessCalendar("Asia/Kolkata", hours, new HashSet<DateOnly> { DateOnly.FromDateTime(now.UtcDateTime.AddDays(20)) });
            await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateCalendarAsync(9001, $"{calendarCode}-BAD-TZ",
                new BusinessCalendar("Not/AZone", hours, new HashSet<DateOnly>()), now, null, "bad timezone", null, CancellationToken.None));
            var invalidHours = new Dictionary<DayOfWeek, (TimeSpan Start, TimeSpan End)> { [DayOfWeek.Monday] = (TimeSpan.FromHours(17), TimeSpan.FromHours(9)) };
            await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateCalendarAsync(9001, $"{calendarCode}-BAD-HOURS",
                new BusinessCalendar("Asia/Kolkata", invalidHours, new HashSet<DateOnly>()), now, null, "bad interval", null, CancellationToken.None));
            await repository.CreateCalendarAsync(9001, calendarCode, calendar, now.AddDays(-1), now.AddDays(30), "test calendar", "sla-admin-test", CancellationToken.None);
            var savedCalendar = await repository.GetCalendarAsync(9001, calendarCode, CancellationToken.None);
            Assert.Equal("Asia/Kolkata", savedCalendar.TimeZoneId);
            Assert.Contains("Monday", savedCalendar.WorkingHoursJson);

            policyId = await repository.CreatePolicyAsync(9001, code, "SLA administration test", "test", $"create-{Guid.NewGuid():N}", "sla-admin-test", CancellationToken.None);
            var policy = new SlaPolicyVersion(policyId, code, 1, now.AddMinutes(-1), now.AddDays(5), 60, 240, calendarCode,
                "WAITING_FOR_REQUESTER", null, "{\"onBreach\":\"record-event\"}", "{\"resolution\":true}", null);
            Assert.True((await repository.ValidateAsync(9001, policyId, policy, CancellationToken.None)).IsValid);
            var versionId = await repository.SaveDraftAsync(policyId, policy, 9001, $"draft-{Guid.NewGuid():N}", null, "sla-admin-test", CancellationToken.None);
            var draft = await repository.GetVersionAsync(9001, versionId, CancellationToken.None);
            var editedDraft = policy with { ResponseTargetMinutes = 45 };
            Assert.Equal(versionId, await repository.SaveDraftAsync(policyId, editedDraft, 9001, $"draft-edit-{Guid.NewGuid():N}", draft.RowVersion, "sla-admin-test", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveDraftAsync(policyId, policy with { ResponseTargetMinutes = 30 },
                9001, $"draft-stale-{Guid.NewGuid():N}", draft.RowVersion, "sla-admin-test", CancellationToken.None));
            await repository.PublishAsync(versionId, 9001, $"publish-{Guid.NewGuid():N}", "sla-admin-test", CancellationToken.None);

            var published = await repository.GetVersionAsync(9999, versionId, CancellationToken.None);
            Assert.Equal("PUBLISHED", published.Status);
            Assert.Equal(45, published.Policy.ResponseTargetMinutes);
            Assert.Null(published.Policy.SubCategoryId);
            Assert.Contains(published.Audit, item => item.Action == "SLA_POLICY_VERSION_PUBLISHED");
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveDraftAsync(policyId,
                policy with { ResponseTargetMinutes = 30 }, 9001, $"edit-{Guid.NewGuid():N}", published.RowVersion, "sla-admin-test", CancellationToken.None));

            var overlapping = policy with { VersionNo = 2, EffectiveFrom = now.AddDays(1), EffectiveTo = now.AddDays(10) };
            var overlapResult = await repository.ValidateAsync(9001, policyId, overlapping, CancellationToken.None);
            Assert.False(overlapResult.IsValid);
            Assert.Contains(overlapResult.Errors, error => error.Contains("overlaps", StringComparison.OrdinalIgnoreCase));
            var nextVersion = policy with { VersionNo = 2, EffectiveFrom = now.AddDays(5), EffectiveTo = now.AddDays(10) };
            var nextVersionId = await repository.SaveDraftAsync(policyId, nextVersion, 9001, $"draft-next-{Guid.NewGuid():N}", null, "sla-admin-test", CancellationToken.None);
            Assert.Equal(2, (await repository.ListVersionsAsync(9001, policyId, CancellationToken.None)).Count);
            Assert.Contains("VersionNo", (await repository.CompareVersionsAsync(9001, versionId, nextVersionId, CancellationToken.None)).ChangedFields);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                repository.CreatePolicyAsync(9999, $"{code}-DENIED", "Denied", null, $"auditor-{Guid.NewGuid():N}", null, CancellationToken.None));
            await repository.RetireAsync(versionId, 9001, $"retire-{Guid.NewGuid():N}", "test retirement", "sla-admin-test", CancellationToken.None);
            Assert.Equal("RETIRED", (await repository.GetVersionAsync(9999, versionId, CancellationToken.None)).Status);
        }
        finally
        {
            await CleanupAsync(provider, policyId, calendarCode);
        }
    }

    [Fact]
    public async Task Subcategory_specificity_beats_category_and_equal_specificity_conflicts_fail()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var baseId = DateTimeOffset.UtcNow.Ticks;
        var category = baseId + 21;
        var subCategory = baseId + 22;
        var service = baseId + 23;
        var team = baseId + 24;
        var policyIds = Enumerable.Range(0, 6).Select(index => baseId + 101 + index).ToArray();
        var ids = Enumerable.Range(0, 6).Select(index => baseId + 201 + index).ToArray();
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            db.SlaPolicies.AddRange(
                new SlaPolicyRow { Id = ids[0], PolicyId = policyIds[0], Code = "CAT-ONLY", VersionNo = 1, Active = true, Status = "PUBLISHED", EffectiveFrom = now, CategoryId = category, CalendarCode = "DEFAULT" },
                new SlaPolicyRow { Id = ids[1], PolicyId = policyIds[1], Code = "SUBCAT", VersionNo = 1, Active = true, Status = "PUBLISHED", EffectiveFrom = now, SubCategoryId = subCategory, CalendarCode = "DEFAULT" });
            await db.SaveChangesAsync();

            var selector = new PersistentSlaPolicyResolver(db);
            var context = new SlaEvaluationContext(service, category, null, null, null, null, null, null, subCategory);
            var selected = await selector.ResolveAsync(context, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(policyIds[1], selected!.PolicyId);

            db.SlaPolicies.Add(new SlaPolicyRow
            {
                Id = ids[2], PolicyId = policyIds[2], Code = "SERVICE-CATEGORY", VersionNo = 1, Active = true,
                Status = "PUBLISHED", EffectiveFrom = now, ServiceId = service, CategoryId = category, CalendarCode = "DEFAULT"
            });
            await db.SaveChangesAsync();
            selected = await selector.ResolveAsync(context, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(policyIds[2], selected!.PolicyId);

            var precedenceContext = new SlaEvaluationContext(null, null, baseId + 25, null, null, baseId + 26, baseId + 27, null, baseId + 28);
            db.SlaPolicies.AddRange(
                new SlaPolicyRow
                {
                    Id = ids[3], PolicyId = policyIds[3], Code = "REQUEST-TEAM", VersionNo = 1, Active = true,
                    Status = "PUBLISHED", EffectiveFrom = now, RequestTypeId = baseId + 25, TeamId = baseId + 27, CalendarCode = "DEFAULT"
                },
                new SlaPolicyRow
                {
                    Id = ids[4], PolicyId = policyIds[4], Code = "SUBCATEGORY-DEPARTMENT", VersionNo = 1, Active = true,
                    Status = "PUBLISHED", EffectiveFrom = now, SubCategoryId = baseId + 28, DepartmentId = baseId + 26, CalendarCode = "DEFAULT"
                });
            await db.SaveChangesAsync();
            selected = await selector.ResolveAsync(precedenceContext, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(policyIds[3], selected!.PolicyId);

            db.SlaPolicies.AddRange(
                new SlaPolicyRow { Id = ids[5], PolicyId = policyIds[5], Code = "TEAM-A", VersionNo = 1, Active = true, Status = "PUBLISHED", EffectiveFrom = now, TeamId = team, CalendarCode = "DEFAULT" },
                new SlaPolicyRow { Id = ids[5] + 1, PolicyId = policyIds[5] + 1, Code = "TEAM-B", VersionNo = 1, Active = true, Status = "PUBLISHED", EffectiveFrom = now, TeamId = team, CalendarCode = "DEFAULT" });
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                selector.ResolveAsync(new SlaEvaluationContext(null, null, null, null, null, null, team, null), DateTimeOffset.UtcNow, CancellationToken.None));
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
            await db.SlaPolicies.Where(x => x.Id >= ids[0] && x.Id <= ids[5] + 1)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Active, false).SetProperty(x => x.Status, "RETIRED"));
        }
    }

    [Fact]
    public async Task Global_and_each_supported_target_dimension_match_the_runtime_context()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        var selector = new PersistentSlaPolicyResolver(db);
        var baseId = DateTimeOffset.UtcNow.Ticks;
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var cases = new (SlaPolicyRow Policy, SlaEvaluationContext Context)[]
        {
            (new() { ServiceId = 872001 }, new(872001, null, null, null, null, null, null, null)),
            (new() { ClientId = 872002 }, new(null, null, null, null, null, null, null, 872002)),
            (new() { CategoryId = 872003 }, new(null, 872003, null, null, null, null, null, null)),
            (new() { SubCategoryId = 872004 }, new(null, 872003, null, null, null, null, null, null, 872004)),
            (new() { RequestTypeId = 872005 }, new(null, null, 872005, null, null, null, null, null)),
            (new() { TicketType = 8 }, new(null, null, null, 8, null, null, null, null)),
            (new() { PriorityId = 872007 }, new(null, null, null, null, 872007, null, null, null)),
            (new() { DepartmentId = 872008 }, new(null, null, null, null, null, 872008, null, null)),
            (new() { TeamId = 872009 }, new(null, null, null, null, null, null, 872009, null))
        };
        var inserted = new List<long>();
        try
        {
            var global = new SlaPolicyRow
            {
                Id = baseId, PolicyId = baseId, Code = "GLOBAL", VersionNo = 1, Active = true, Status = "PUBLISHED",
                EffectiveFrom = now, CalendarCode = "DEFAULT"
            };
            db.SlaPolicies.Add(global);
            inserted.Add(global.Id);
            var activeDraft = new SlaPolicyRow
            {
                Id = baseId + 1000, PolicyId = baseId + 1000, Code = "ACTIVE-DRAFT", VersionNo = 1,
                Active = true, Status = "DRAFT", EffectiveFrom = now, CalendarCode = "DEFAULT"
            };
            db.SlaPolicies.Add(activeDraft);
            inserted.Add(activeDraft.Id);
            await db.SaveChangesAsync();
            Assert.Equal(baseId, (await selector.ResolveAsync(new(null, null, null, null, null, null, null, null), DateTimeOffset.UtcNow, CancellationToken.None))!.PolicyId);
            await db.SlaPolicies.Where(x => x.Id == global.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Active, false));

            var index = 0;
            foreach (var (target, context) in cases)
            {
                var id = baseId + ++index;
                target.Id = id;
                target.PolicyId = id;
                target.Code = $"TARGET-{index}";
                target.VersionNo = 1;
                target.Active = true;
                target.Status = "PUBLISHED";
                target.EffectiveFrom = now;
                target.CalendarCode = "DEFAULT";
                db.SlaPolicies.Add(target);
                inserted.Add(id);
                await db.SaveChangesAsync();
                Assert.Equal(id, (await selector.ResolveAsync(context, DateTimeOffset.UtcNow, CancellationToken.None))!.PolicyId);
                target.Active = false;
                target.Status = "RETIRED";
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            await db.SlaPolicies.Where(x => inserted.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Active, false).SetProperty(x => x.Status, "RETIRED"));
        }
    }

    private static async Task CleanupAsync(ServiceProvider provider, long policyId, string calendarCode)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        if (policyId > 0)
        {
            await db.SlaPolicies.Where(x => x.PolicyId == policyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Active, false).SetProperty(x => x.Status, "RETIRED"));
            await db.SlaPolicyDefinitions.Where(x => x.Id == policyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ActiveFlag, 0));
        }
        await db.SlaCalendars.Where(x => x.Code == calendarCode)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Active, false));
        await db.SaveChangesAsync();
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
