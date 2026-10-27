using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class DatabaseAuthorizationCanonicalCodeTests
{
    [Fact]
    public async Task Noncanonical_permission_request_is_denied_without_reading_facts()
    {
        var factsReader = new StubFactsReader(CreateFacts("USER", "TICKET_READ"));
        await using var db = CreateProviderlessContext();
        var authorization = CreateAuthorization(db, factsReader);

        var allowed = await authorization.HasPermissionAsync(42, "ticket_read", null, CancellationToken.None);

        Assert.False(allowed);
        Assert.Equal(0, factsReader.Calls);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("spectator")]
    [InlineData("SUPERADMIN")]
    [InlineData("ADMINHEAD")]
    public async Task Noncanonical_or_alias_role_facts_fail_closed(string roleCode)
    {
        var factsReader = new StubFactsReader(CreateFacts(roleCode, "TICKET_READ"));
        await using var db = CreateProviderlessContext();
        var authorization = CreateAuthorization(db, factsReader);

        var allowed = await authorization.HasPermissionAsync(42, "TICKET_READ", null, CancellationToken.None);

        Assert.False(allowed);
        Assert.Equal(1, factsReader.Calls);
    }

    [Fact]
    public async Task Duplicate_active_role_identity_fails_closed()
    {
        var facts = CreateFacts("USER", "TICKET_READ") with
        {
            ActiveRoles = [new(7, "USER", true, 70), new(8, "USER", true, 80)]
        };
        var factsReader = new StubFactsReader(facts);
        await using var db = CreateProviderlessContext();
        var authorization = CreateAuthorization(db, factsReader);

        Assert.False(await authorization.HasPermissionAsync(42, "TICKET_READ", null, CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_effective_grant_identity_fails_closed()
    {
        var facts = CreateFacts("USER", "TICKET_READ");
        facts = facts with { Grants = [facts.Grants.Single(), facts.Grants.Single()] };
        var factsReader = new StubFactsReader(facts);
        await using var db = CreateProviderlessContext();
        var authorization = CreateAuthorization(db, factsReader);

        Assert.False(await authorization.HasPermissionAsync(42, "TICKET_READ", null, CancellationToken.None));
    }

    [Fact]
    public async Task Exact_canonical_role_permission_and_metadata_still_authorize()
    {
        var factsReader = new StubFactsReader(CreateFacts("USER", "TICKET_READ"));
        await using var db = CreateProviderlessContext();
        var authorization = CreateAuthorization(db, factsReader);

        Assert.True(await authorization.HasPermissionAsync(42, "TICKET_READ", null, CancellationToken.None));
    }

    [Theory]
    [InlineData("TICKET_READ")]
    [InlineData("TICKET_UPDATE")]
    public async Task User_can_access_ticket_when_requester_or_current_assignee(string permission)
    {
        var facts = CreateFacts("USER", permission);
        var authorization = await CreateProviderlessAuthorization(facts);
        var asRequester = new AuthorizationResource(TicketId: 100, RequesterUserId: 42);
        var asAssignee = new AuthorizationResource(TicketId: 100, AssignedUserId: 42, RequesterUserId: 9);

        Assert.True(await authorization.HasPermissionAsync(42, permission, asRequester, CancellationToken.None));
        Assert.True(await authorization.HasPermissionAsync(42, permission, asAssignee, CancellationToken.None));
    }

    [Theory]
    [InlineData("TICKET_READ")]
    [InlineData("TICKET_UPDATE")]
    public async Task User_record_access_fails_closed_for_unrelated_or_incomplete_resource(string permission)
    {
        var authorization = await CreateProviderlessAuthorization(CreateFacts("USER", permission));

        Assert.False(await authorization.HasPermissionAsync(42, permission,
            new AuthorizationResource(TicketId: 100, DepartmentId: 7, TeamId: 8, RequesterUserId: 9), CancellationToken.None));
        Assert.False(await authorization.HasPermissionAsync(42, permission,
            new AuthorizationResource(TicketId: 100, DepartmentId: 7, TeamId: 8, AssignedUserId: 42), CancellationToken.None));
        Assert.False(await authorization.HasPermissionAsync(42, permission,
            new AuthorizationResource(DepartmentId: 7, TeamId: 8, RequesterUserId: 42), CancellationToken.None));
    }

    [Fact]
    public async Task Ticket_detail_authorizer_supplies_server_loaded_owner_and_denies_unrelated_ticket()
    {
        await using var db = CreateProviderlessContext();
        var authorization = CreateAuthorization(db, new StubFactsReader(CreateFacts("USER", "TICKET_READ")));
        var ticketAccess = new DatabaseTicketAccessAuthorizer(authorization, db, TimeProvider.System);
        var ownTicket = PA.Tickets.Domain.Tickets.Ticket.Create(100, "T-100", PA.Tickets.Domain.Tickets.TicketType.Request,
            "Own ticket", null, 42, 42, DateTimeOffset.UtcNow);
        var unrelatedTicket = PA.Tickets.Domain.Tickets.Ticket.Create(101, "T-101", PA.Tickets.Domain.Tickets.TicketType.Request,
            "Other ticket", null, 9, 9, DateTimeOffset.UtcNow);

        await ticketAccess.EnsureCanAccessAsync(42, ownTicket, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ticketAccess.EnsureCanAccessAsync(42, unrelatedTicket, CancellationToken.None));
    }

    [Fact]
    public async Task Mixed_user_and_admin_membership_uses_separately_scoped_admin_grant()
    {
        var now = DateTimeOffset.UtcNow;
        var baseFacts = CreateFacts("USER", "TICKET_READ");
        var facts = baseFacts with
        {
            ActiveRoles = [new(7, "USER", true, 70), new(8, "ADMIN", true, 80)],
            Grants = [
                baseFacts.Grants.Single(),
                new(8, "ADMIN", true, true, PA.Tickets.Application.Foundations.EffectiveGrantAuthorization.Active,
                    now.AddMinutes(-1), null, "TICKET_READ", "READ_ONLY", "DEPARTMENT", true)],
            Scopes = [new(1, 8, "DEPARTMENT", null, 7, null, null, null, null, now.AddMinutes(-1), null, true)]
        };
        var authorization = await CreateProviderlessAuthorization(facts);

        Assert.True(await authorization.HasPermissionAsync(42, "TICKET_READ",
            new AuthorizationResource(TicketId: 100, DepartmentId: 7, TeamId: 99, RequesterUserId: 9), CancellationToken.None));
        Assert.False(await authorization.HasPermissionAsync(42, "TICKET_READ",
            new AuthorizationResource(TicketId: 101, DepartmentId: 8, TeamId: 99, RequesterUserId: 9), CancellationToken.None));
    }

    [Fact]
    public async Task Admin_head_ticket_access_remains_team_scoped()
    {
        var now = DateTimeOffset.UtcNow;
        var facts = new AuthorizationFacts(
            new AuthorizationActorFact(42, 7, 8, true),
            new AuthorizationPermissionFact(3, "TICKET_READ", "TICKETS", "VIEW", "TEAM", "READ_ONLY", true, true),
            [new AuthorizationRoleFact(8, "ADMIN_HEAD", true, 80)],
            [new AuthorizationGrantFact(8, "ADMIN_HEAD", true, true, PA.Tickets.Application.Foundations.EffectiveGrantAuthorization.Active,
                now.AddMinutes(-1), null, "TICKET_READ", "READ_ONLY", "TEAM", true)],
            [new AuthorizationScopeFact(1, 8, "TEAM", null, 7, 8, null, null, null, now.AddMinutes(-1), null, true)]);
        var authorization = await CreateProviderlessAuthorization(facts);

        Assert.True(await authorization.HasPermissionAsync(42, "TICKET_READ",
            new AuthorizationResource(TicketId: 100, DepartmentId: 7, TeamId: 8, RequesterUserId: 9), CancellationToken.None));
        Assert.False(await authorization.HasPermissionAsync(42, "TICKET_READ",
            new AuthorizationResource(TicketId: 101, DepartmentId: 7, TeamId: 9, RequesterUserId: 9), CancellationToken.None));
    }

    [Fact]
    public async Task User_and_super_admin_memberships_fail_closed_as_mutually_exclusive()
    {
        var facts = CreateFacts("USER", "TICKET_READ") with
        {
            ActiveRoles = [new(7, "USER", true, 70), new(8, "SUPER_ADMIN", true, 80)]
        };
        var authorization = await CreateProviderlessAuthorization(facts);

        Assert.False(await authorization.HasPermissionAsync(42, "TICKET_READ",
            new AuthorizationResource(TicketId: 100, DepartmentId: 7, RequesterUserId: 9), CancellationToken.None));
    }

    [Fact]
    public async Task Custom_role_scope_does_not_widen_access_for_a_user_membership()
    {
        var now = DateTimeOffset.UtcNow;
        var baseFacts = CreateFacts("USER", "TICKET_READ");
        var facts = baseFacts with
        {
            ActiveRoles = [new(7, "USER", true, 70), new(8, "SUPPORT", false, 80)],
            Grants = [new(8, "SUPPORT", false, true, PA.Tickets.Application.Foundations.EffectiveGrantAuthorization.Active,
                now.AddMinutes(-1), null, "TICKET_READ", "READ_ONLY", "TEAM", true)],
            Scopes = [new(1, 8, "TEAM", null, 7, 8, null, null, null, now.AddMinutes(-1), null, true)]
        };
        var authorization = await CreateProviderlessAuthorization(facts);

        Assert.False(await authorization.HasPermissionAsync(42, "TICKET_READ",
            new AuthorizationResource(TicketId: 100, DepartmentId: 7, TeamId: 8, RequesterUserId: 9), CancellationToken.None));
        Assert.True(await authorization.HasPermissionAsync(42, "TICKET_READ",
            new AuthorizationResource(TicketId: 101, DepartmentId: 7, TeamId: 8, RequesterUserId: 42), CancellationToken.None));
    }

    [Fact]
    public void User_search_predicate_keeps_own_and_assigned_rows_before_paging()
    {
        var rows = new[]
        {
            new PA.Tickets.Infrastructure.Persistence.TicketRow { Id = 1, RequestedBy = 42, CurrentAssignee = 8 },
            new PA.Tickets.Infrastructure.Persistence.TicketRow { Id = 2, RequestedBy = 9, CurrentAssignee = 42 },
            new PA.Tickets.Infrastructure.Persistence.TicketRow { Id = 3, RequestedBy = 9, CurrentAssignee = 10 }
        }.AsQueryable();

        var visible = PA.Tickets.Infrastructure.Tickets.TicketSearchScopePolicy
            .ApplyOwnOrAssigned(rows, 42).Select(ticket => ticket.Id).ToArray();

        Assert.Equal([1L, 2L], visible);
    }

    [Fact]
    public async Task Spectator_audit_export_is_denied_even_when_an_effective_grant_is_present()
    {
        var facts = CreateFacts("SPECTATOR", "AUDIT_EXPORT") with
        {
            Permission = new AuthorizationPermissionFact(9, "AUDIT_EXPORT", "AUDIT", "EXPORT", "RECORD", "READ_ONLY", true, true),
            Grants = [new AuthorizationGrantFact(7, "SPECTATOR", true, true,
                PA.Tickets.Application.Foundations.EffectiveGrantAuthorization.Active,
                DateTimeOffset.UtcNow.AddMinutes(-1), null, "AUDIT_EXPORT", "READ_ONLY", "RECORD", true)]
        };
        var factsReader = new StubFactsReader(facts);
        await using var db = CreateProviderlessContext();
        var authorization = CreateAuthorization(db, factsReader);

        Assert.False(await authorization.HasPermissionAsync(42, "AUDIT_EXPORT", null, CancellationToken.None));
    }

    private static AuthorizationFacts CreateFacts(string roleCode, string permissionCode)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuthorizationFacts(
            new AuthorizationActorFact(42, null, null, true),
            new AuthorizationPermissionFact(3, permissionCode, "TICKETS", "VIEW", "NONE", "READ_ONLY", true, true),
            [new AuthorizationRoleFact(7, roleCode, true)],
            [new AuthorizationGrantFact(7, roleCode, true, true, PA.Tickets.Application.Foundations.EffectiveGrantAuthorization.Active,
                now.AddMinutes(-1), null, permissionCode, "READ_ONLY", "NONE", true)],
            []);
    }

    private static TicketsDbContext CreateProviderlessContext() =>
        new(new DbContextOptionsBuilder<TicketsDbContext>().Options);

    private static Task<DatabaseAuthorizationService> CreateProviderlessAuthorization(AuthorizationFacts facts)
    {
        var db = CreateProviderlessContext();
        return Task.FromResult(new DatabaseAuthorizationService(db, TimeProvider.System, new StubFactsReader(facts), new StubDelegationFactsReader()));
    }

    private static DatabaseAuthorizationService CreateAuthorization(
        TicketsDbContext db, IAuthorizationFactsReader factsReader) =>
        new(db, TimeProvider.System, factsReader, new StubDelegationFactsReader());

    private sealed class StubFactsReader(AuthorizationFacts facts) : IAuthorizationFactsReader
    {
        public int Calls { get; private set; }

        public Task<AuthorizationFacts> ReadAsync(long actorUserId, string permission, DateTimeOffset at,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(facts);
        }
    }

    private sealed class StubDelegationFactsReader : IAdminHeadDelegationFactsReader
    {
        public Task<AdminHeadDelegationFacts> ReadAsync(long adminHeadUserId, long targetUserId,
            DateTimeOffset at, CancellationToken cancellationToken) =>
            Task.FromResult(new AdminHeadDelegationFacts(false, false, false));
    }
}
