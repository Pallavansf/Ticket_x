using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlAdministrationAuthorizationTests
{
    [Fact]
    public async Task Ticket_search_requires_explicit_scope_filters_for_non_admin_users()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var baseId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100;
        var departmentA = baseId + 1;
        var departmentB = baseId + 2;
        var teamA = baseId + 3;
        var teamB = baseId + 4;
        var userA = baseId + 11;
        var userB = baseId + 12;
        var userWithoutOrgScope = baseId + 13;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var now = DateTimeOffset.UtcNow;
                db.AdministrationDepartments.AddRange(
                    new AdministrationDepartmentRow { Id = departmentA, Code = $"D-{departmentA}", Name = "Department A", Active = true, CreatedAt = now, CreatedBy = 9001 },
                    new AdministrationDepartmentRow { Id = departmentB, Code = $"D-{departmentB}", Name = "Department B", Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationTeams.AddRange(
                    new AdministrationTeamRow { Id = teamA, DepartmentId = departmentA, Code = $"T-{teamA}", Name = "Team A", Active = true, CreatedAt = now, CreatedBy = 9001 },
                    new AdministrationTeamRow { Id = teamB, DepartmentId = departmentB, Code = $"T-{teamB}", Name = "Team B", Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUsers.AddRange(
                    new AdministrationUserRow { Id = userA, UserCode = $"U-{userA}", LoginName = $"u-{userA}", DisplayName = "User A", DepartmentId = departmentA, TeamId = teamA, Active = true, CreatedAt = now, CreatedBy = 9001 },
                    new AdministrationUserRow { Id = userB, UserCode = $"U-{userB}", LoginName = $"u-{userB}", DisplayName = "User B", DepartmentId = departmentB, TeamId = teamB, Active = true, CreatedAt = now, CreatedBy = 9001 },
                    new AdministrationUserRow { Id = userWithoutOrgScope, UserCode = $"U-{userWithoutOrgScope}", LoginName = $"u-{userWithoutOrgScope}", DisplayName = "User without organizational scope", Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUserRoles.Add(new AdministrationUserRoleRow { UserId = userA, RoleId = 1, ValidFrom = now.AddMinutes(-1), Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUserRoles.Add(new AdministrationUserRoleRow { UserId = userB, RoleId = 1, ValidFrom = now.AddMinutes(-1), Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUserRoles.Add(new AdministrationUserRoleRow { UserId = userWithoutOrgScope, RoleId = 1, ValidFrom = now.AddMinutes(-1), Active = true, CreatedAt = now, CreatedBy = 9001 });
                await db.SaveChangesAsync();
            }

            await using var scope2 = provider.CreateAsyncScope();
            var service = scope2.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            var search = new TicketSearchCriteria(null, null, null, null, null, null, null, null, null, null, null, null, null, 1, 25);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchAsync(search, userA, CancellationToken.None));
            var scoped = new TicketSearchCriteria(null, null, null, null, null, null, null, null, departmentA, teamA, null, null, null, 1, 25);
            var page = await service.SearchAsync(scoped, userA, CancellationToken.None);
            Assert.NotNull(page);
            Assert.True(page.TotalCount >= 0);
            var foreignDepartmentSearch = new TicketSearchCriteria(null, null, null, null, null, null, null, null, departmentB, null, null, null, null, 1, 25);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchAsync(foreignDepartmentSearch, userWithoutOrgScope, CancellationToken.None));
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER_ROLE\" where \"N_USER_ID\" in ({userA},{userB},{userWithoutOrgScope})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER\" where \"N_USER_ID\" in ({userA},{userB},{userWithoutOrgScope})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_TEAM\" where \"N_TEAM_ID\" in ({teamA},{teamB})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_DEPARTMENT\" where \"N_DEPARTMENT_ID\" in ({departmentA},{departmentB})");
        }
    }

    [Fact]
    public async Task Reports_do_not_return_tickets_from_a_foreign_department_filter()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ticketId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100 + Random.Shared.Next(1, 99);
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                db.Tickets.Add(new TicketRow
                {
                    Id = ticketId,
                    Number = $"AUTH-{ticketId}",
                    TypeId = (int)TicketType.Incident,
                    StatusId = (int)TicketStatus.New,
                    RequestedBy = 1001,
                    CreatedBy = 1001,
                    Subject = "Foreign department report fixture",
                    CurrentDepartmentId = 999,
                    CurrentTeamId = 999,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();

                var reports = scope.ServiceProvider.GetRequiredService<IReportingQueryService>();
                var result = await reports.QueryAsync(
                    "ticket-aging",
                    new ReportQueryRequest(DepartmentId: 999),
                    1001,
                    "authorization-report-scope",
                    CancellationToken.None);
                Assert.Equal(0, result.TotalCount);
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Database
                .ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
        }
    }

    [Fact]
    public async Task Database_authorization_enforces_permission_scope_effective_dates_and_auditor_read_only_access()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var baseId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100;
        var departmentA = baseId + 1;
        var departmentB = baseId + 2;
        var teamA = baseId + 3;
        var teamB = baseId + 4;
        var userA = baseId + 11;
        var userB = baseId + 12;
        var clientA = baseId + 21;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var now = DateTimeOffset.UtcNow;
                db.AdministrationDepartments.AddRange(
                    new AdministrationDepartmentRow { Id = departmentA, Code = $"D-{departmentA}", Name = "Department A", Active = true, CreatedAt = now, CreatedBy = 9001 },
                    new AdministrationDepartmentRow { Id = departmentB, Code = $"D-{departmentB}", Name = "Department B", Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationTeams.AddRange(
                    new AdministrationTeamRow { Id = teamA, DepartmentId = departmentA, Code = $"T-{teamA}", Name = "Team A", Active = true, CreatedAt = now, CreatedBy = 9001 },
                    new AdministrationTeamRow { Id = teamB, DepartmentId = departmentB, Code = $"T-{teamB}", Name = "Team B", Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUsers.AddRange(
                    new AdministrationUserRow { Id = userA, UserCode = $"U-{userA}", LoginName = $"u-{userA}", DisplayName = "User A", DepartmentId = departmentA, TeamId = teamA, Active = true, CreatedAt = now, CreatedBy = 9001 },
                    new AdministrationUserRow { Id = userB, UserCode = $"U-{userB}", LoginName = $"u-{userB}", DisplayName = "User B", DepartmentId = departmentB, TeamId = teamB, Active = false, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUserRoles.Add(new AdministrationUserRoleRow { UserId = userA, RoleId = 1, ValidFrom = now.AddMinutes(-1), Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUserRoles.Add(new AdministrationUserRoleRow { UserId = userB, RoleId = 1, ValidFrom = now.AddMinutes(-1), Active = true, CreatedAt = now, CreatedBy = 9001 });
                db.AdministrationUserClients.Add(new AdministrationUserClientRow { UserId = userA, ClientId = clientA, ValidFrom = now.AddMinutes(-1), Active = true, CreatedAt = now, CreatedBy = 9001 });
                await db.SaveChangesAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
                Assert.True(await authorization.HasPermissionAsync(userA, "TICKET_READ", new(departmentA, departmentA, teamA, clientA), CancellationToken.None));
                Assert.False(await authorization.HasPermissionAsync(userA, "TICKET_READ", new(DepartmentId: departmentB), CancellationToken.None));
                Assert.False(await authorization.HasPermissionAsync(userA, "TICKET_READ", new(DepartmentId: departmentB, TeamId: teamB, AssignedUserId: userA), CancellationToken.None));
                Assert.False(await authorization.HasPermissionAsync(userA, "TICKET_READ", new(ClientId: clientA + 1), CancellationToken.None));
                Assert.False(await authorization.HasPermissionAsync(userB, "TICKET_READ", null, CancellationToken.None));
                var ticketAccess = scope.ServiceProvider.GetRequiredService<ITicketAccessAuthorizer>();
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ticketAccess.EnsureCanAssignAsync(
                    userA,
                    new AuthorizationResource(DepartmentId: departmentA, TeamId: teamA),
                    departmentB,
                    teamB,
                    null,
                    CancellationToken.None));
                Assert.True(await authorization.HasPermissionAsync(9999, "TICKET_READ", null, CancellationToken.None));
                Assert.False(await authorization.HasPermissionAsync(9999, "TICKET_UPDATE", null, CancellationToken.None));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorization.EnsurePermissionAsync(userA, "USER_ADMIN", null, CancellationToken.None));
            }
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER_CLIENT\" where \"N_USER_ID\" in ({userA},{userB})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER_ROLE\" where \"N_USER_ID\" in ({userA},{userB})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER\" where \"N_USER_ID\" in ({userA},{userB})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_TEAM\" where \"N_TEAM_ID\" in ({teamA},{teamB})");
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_DEPARTMENT\" where \"N_DEPARTMENT_ID\" in ({departmentA},{departmentB})");
        }
    }

    [Fact]
    public async Task Administration_mutations_are_permission_gated_persisted_and_audited()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var code = $"SLICE1-{Guid.NewGuid():N}";
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAdministrationService>();
            var created = await service.CreateDepartmentAsync(9001, code, "Slice 1 Department", "authorization test", CancellationToken.None);
            Assert.True(created.Active);
            Assert.Contains(await service.ListDepartmentsAsync(9001, CancellationToken.None), x => x.Id == created.Id);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateDepartmentAsync(1001, $"DENIED-{Guid.NewGuid():N}", "Denied", null, CancellationToken.None));
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "ADMINISTRATION" && x.EntityId == created.Id.ToString() && x.Action == "DEPARTMENT_CREATED"));
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_DEPARTMENT\" where \"S_DEPARTMENT_CODE\" = {code}");
        }
    }

    [Fact]
    public async Task Effective_dated_memberships_can_be_regranted_without_losing_history_or_granting_duplicates()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var userCode = $"REGRANT-{Guid.NewGuid():N}";
        long userId = 0;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var administration = scope.ServiceProvider.GetRequiredService<IAdministrationService>();
                var user = await administration.CreateUserAsync(9001, userCode, userCode, "Membership regrant test", null, null, null, CancellationToken.None);
                userId = user.Id;
                var firstRole = await administration.AssignRoleAsync(9001, userId, 1, DateTimeOffset.UtcNow.AddMinutes(-1), null, CancellationToken.None);
                var firstClient = await administration.AssignClientAsync(9001, userId, 880001, DateTimeOffset.UtcNow.AddMinutes(-1), null, CancellationToken.None);

                await administration.RemoveRoleAsync(9001, firstRole.Id, CancellationToken.None);
                await administration.RemoveClientAsync(9001, firstClient.Id, CancellationToken.None);

                var secondRole = await administration.AssignRoleAsync(9001, userId, 1, DateTimeOffset.UtcNow, null, CancellationToken.None);
                var secondClient = await administration.AssignClientAsync(9001, userId, 880001, DateTimeOffset.UtcNow, null, CancellationToken.None);
                Assert.NotEqual(firstRole.Id, secondRole.Id);
                Assert.NotEqual(firstClient.Id, secondClient.Id);

                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var roleHistory = await db.AdministrationUserRoles.AsNoTracking().Where(x => x.UserId == userId && x.RoleId == 1).OrderBy(x => x.Id).ToArrayAsync();
                var clientHistory = await db.AdministrationUserClients.AsNoTracking().Where(x => x.UserId == userId && x.ClientId == 880001).OrderBy(x => x.Id).ToArrayAsync();
                Assert.Equal(2, roleHistory.Length);
                Assert.Equal(2, clientHistory.Length);
                Assert.False(roleHistory[0].Active);
                Assert.False(clientHistory[0].Active);
                Assert.True(roleHistory[1].Active);
                Assert.True(clientHistory[1].Active);

                var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
                Assert.True(await authorization.HasPermissionAsync(userId, "TICKET_READ", null, CancellationToken.None));
            }

            var concurrentStart = DateTimeOffset.UtcNow.AddMinutes(1);
            await using (var scope = provider.CreateAsyncScope())
            {
                var administration = scope.ServiceProvider.GetRequiredService<IAdministrationService>();
                var activeMembership = await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().AdministrationUserRoles
                    .SingleAsync(x => x.UserId == userId && x.RoleId == 1 && x.Active);
                await administration.RemoveRoleAsync(9001, activeMembership.Id, CancellationToken.None);
            }

            var grants = new[]
            {
                TryAssignRoleAsync(provider, userId, concurrentStart),
                TryAssignRoleAsync(provider, userId, concurrentStart)
            };
            var results = await Task.WhenAll(grants);
            Assert.Single(results, x => x);
            await using (var scope = provider.CreateAsyncScope())
            {
                var history = await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().AdministrationUserRoles
                    .AsNoTracking().Where(x => x.UserId == userId && x.RoleId == 1).ToArrayAsync();
                Assert.Equal(3, history.Length);
                Assert.Single(history, x => x.Active);
                var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
                Assert.False(await authorization.HasPermissionAsync(userId, "TICKET_READ", null, CancellationToken.None));
            }
        }
        finally
        {
            if (userId != 0)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER_CLIENT\" where \"N_USER_ID\" = {userId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER_ROLE\" where \"N_USER_ID\" = {userId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_USER\" where \"N_USER_ID\" = {userId}");
            }
        }
    }

    private static async Task<bool> TryAssignRoleAsync(IServiceProvider provider, long userId, DateTimeOffset validFrom)
    {
        await using var scope = provider.CreateAsyncScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<IAdministrationService>()
                .AssignRoleAsync(9001, userId, 1, validFrom, null, CancellationToken.None);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return false;
        }
    }

    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" }).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config); services.AddApplication(); services.AddInfrastructure(config); return services.BuildServiceProvider(validateScopes: true);
    }
}
