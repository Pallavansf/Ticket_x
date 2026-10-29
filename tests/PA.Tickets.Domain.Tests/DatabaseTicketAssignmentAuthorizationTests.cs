using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class DatabaseTicketAssignmentAuthorizationTests
{
    [Fact]
    public async Task Assignment_requires_update_and_dedicated_scoped_assignment_permission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TicketsDbContext(new DbContextOptionsBuilder<TicketsDbContext>().UseSqlite(connection).Options);
        await CreateAssignmentFactsAsync(db);
        Assert.Equal(1, await db.AdministrationUserRoles.CountAsync());
        Assert.Equal(1, await db.AdministrationUserRoleScopes.CountAsync());
        Assert.Equal(1, await db.AdministrationRoles.CountAsync());
        Assert.Equal(1, await db.AssignmentAuthorityRules.CountAsync());
        var membershipFact = await db.AdministrationUserRoles.SingleAsync();
        var roleFact = await db.AdministrationRoles.SingleAsync();
        var scopeFact = await db.AdministrationUserRoleScopes.SingleAsync();
        Assert.True(membershipFact.Active);
        Assert.True(roleFact.Active);
        Assert.True(scopeFact.Active);
        Assert.Equal(501, scopeFact.DepartmentId);
        Assert.Equal(601, scopeFact.TeamId);
        Assert.True(membershipFact.ValidFrom <= TimeProvider.System.GetUtcNow());
        Assert.True(scopeFact.ValidFrom <= TimeProvider.System.GetUtcNow());
        var authorization = new RecordingAuthorization("TICKET_UPDATE", "TICKET_ASSIGN_TEAM");
        var access = new DatabaseTicketAccessAuthorizer(authorization, db, TimeProvider.System);

        await access.EnsureCanAssignAsync(
            actorUserId: 1001,
            ticketResource: new AuthorizationResource(TicketId: 7001, RequesterUserId: 1001),
            departmentId: 501,
            teamId: 601,
            assigneeUserId: null,
            CancellationToken.None);

        Assert.Equal(new[] { "TICKET_ASSIGN_TEAM", "TICKET_UPDATE", "TICKET_ASSIGN_TEAM", "TICKET_UPDATE" }, authorization.Permissions);
        Assert.All(authorization.Resources, resource => Assert.Equal(7001, resource!.TicketId));
        Assert.Equal(501, authorization.Resources[2]!.DepartmentId);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Assignment_denies_when_active_membership_or_team_scope_is_missing(bool includeMembership, bool includeTeamScope)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TicketsDbContext(new DbContextOptionsBuilder<TicketsDbContext>().UseSqlite(connection).Options);
        await CreateAssignmentFactsAsync(db, includeMembership, includeTeamScope);
        var authorization = new RecordingAuthorization("TICKET_UPDATE", "TICKET_ASSIGN_TEAM");
        var access = new DatabaseTicketAccessAuthorizer(authorization, db, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.EnsureCanAssignAsync(
            1001, new AuthorizationResource(TicketId: 7001, RequesterUserId: 1001),
            501, 601, null, CancellationToken.None));
    }

    [Fact]
    public async Task User_with_ticket_update_but_without_assignment_authority_is_denied()
    {
        await using var db = new TicketsDbContext(new DbContextOptionsBuilder<TicketsDbContext>().Options);
        var authorization = new RecordingAuthorization("TICKET_UPDATE");
        var access = new DatabaseTicketAccessAuthorizer(authorization, db, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.EnsureCanAssignAsync(
            actorUserId: 1001,
            ticketResource: new AuthorizationResource(TicketId: 7001, RequesterUserId: 1001),
            departmentId: 501,
            teamId: null,
            assigneeUserId: null,
            CancellationToken.None));

        Assert.Equal(new[] { "TICKET_ASSIGN_TEAM" }, authorization.Permissions);
    }

    private static async Task CreateAssignmentFactsAsync(TicketsDbContext db, bool includeMembership = true, bool includeTeamScope = true)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "PA_TICKETS_TEAM" (
                "N_TEAM_ID" INTEGER PRIMARY KEY, "N_DEPARTMENT_ID" INTEGER NOT NULL,
                "S_TEAM_CODE" TEXT NOT NULL, "S_TEAM_NAME" TEXT NOT NULL, "S_DESCRIPTION" TEXT,
                "N_ACTIVE" INTEGER NOT NULL, "D_CREATED_AT" TEXT NOT NULL, "N_CREATED_BY" INTEGER NOT NULL);
            CREATE TABLE "PA_TICKETS_ROLE" (
                "N_ROLE_ID" INTEGER PRIMARY KEY, "S_ROLE_CODE" TEXT NOT NULL, "S_ROLE_NAME" TEXT NOT NULL,
                "S_DESCRIPTION" TEXT, "N_SYSTEM_ROLE" INTEGER NOT NULL, "N_ACTIVE" INTEGER NOT NULL);
            CREATE TABLE "PA_TICKETS_USER_ROLE" (
                "N_USER_ROLE_ID" INTEGER PRIMARY KEY, "N_USER_ID" INTEGER NOT NULL, "N_ROLE_ID" INTEGER NOT NULL,
                "D_VALID_FROM" TEXT NOT NULL, "D_VALID_TO" TEXT, "N_ACTIVE" INTEGER NOT NULL,
                "D_CREATED_AT" TEXT NOT NULL, "N_CREATED_BY" INTEGER NOT NULL);
            CREATE TABLE "PA_TICKETS_USER_ROLE_SCOPE" (
                "N_ROLE_SCOPE_ID" INTEGER PRIMARY KEY, "N_USER_ROLE_ID" INTEGER NOT NULL,
                "S_SCOPE_TYPE" TEXT NOT NULL, "N_ORGANIZATION_ID" INTEGER, "N_DEPARTMENT_ID" INTEGER,
                "N_TEAM_ID" INTEGER, "N_CLIENT_ID" INTEGER, "S_RESOURCE_TYPE" TEXT, "S_RESOURCE_ID" TEXT,
                "D_VALID_FROM" TEXT NOT NULL, "D_VALID_TO" TEXT, "N_ACTIVE" INTEGER NOT NULL,
                "D_CREATED_AT" TEXT NOT NULL, "N_CREATED_BY" INTEGER NOT NULL);
            CREATE TABLE "PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE" (
                "N_ASSIGNMENT_RULE_ID" INTEGER PRIMARY KEY, "S_PERMISSION_CODE" TEXT NOT NULL,
                "S_TARGET_RELATION" TEXT NOT NULL, "S_MODE" TEXT NOT NULL,
                "N_NOTIFY_TARGET_MANAGER" INTEGER NOT NULL, "N_REQUIRE_MANAGER_ACCEPTANCE" INTEGER NOT NULL,
                "N_ENABLED" INTEGER NOT NULL, "D_VALID_FROM" TEXT NOT NULL, "D_VALID_TO" TEXT,
                "N_VERSION" INTEGER NOT NULL, "D_CREATED_AT" TEXT NOT NULL, "N_CREATED_BY" INTEGER NOT NULL);
            INSERT INTO "PA_TICKETS_TEAM" VALUES (601,501,'TEAM-601','Team 601',NULL,1,'2026-01-01T00:00:00Z',1001);
            INSERT INTO "PA_TICKETS_ROLE" VALUES (9001,'TEST_MANAGER','Test Manager',NULL,0,1);
            INSERT INTO "PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE" VALUES (9301,'TICKET_ASSIGN_TEAM','OWN_TEAM','ASSIGN',0,0,1,'2020-01-01T00:00:00Z',NULL,1,'2020-01-01T00:00:00Z',1001);
            """);
        if (includeMembership)
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"PA_TICKETS_USER_ROLE\" VALUES (9101,1001,9001,'2020-01-01T00:00:00Z',NULL,1,'2020-01-01T00:00:00Z',1001)");
        if (includeTeamScope)
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"PA_TICKETS_USER_ROLE_SCOPE\" VALUES (9201,9101,'TEAM',NULL,501,601,NULL,NULL,NULL,'2020-01-01T00:00:00Z',NULL,1,'2020-01-01T00:00:00Z',1001)");
    }

    private sealed class RecordingAuthorization(params string[] allowed) : IAuthorizationService
    {
        public List<string> Permissions { get; } = [];
        public List<AuthorizationResource?> Resources { get; } = [];

        public Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct)
        {
            Permissions.Add(permission);
            Resources.Add(resource);
            if (!allowed.Contains(permission, StringComparer.Ordinal))
                throw new UnauthorizedAccessException();
            return Task.CompletedTask;
        }

        public Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) =>
            Task.FromResult(allowed.Contains(permission, StringComparer.Ordinal));
    }
}
