using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Domain.Tests;

public sealed class KpiClientMembershipScopeTests
{
    [Fact]
    public void Effective_client_scope_excludes_expired_and_not_yet_effective_memberships()
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var memberships = new[]
        {
            new AdministrationUserClientRow { Id = 1, UserId = 42, ClientId = 101, Active = true, ValidFrom = now.AddDays(-1) },
            new AdministrationUserClientRow { Id = 2, UserId = 42, ClientId = 102, Active = true, ValidFrom = now.AddDays(-2), ValidTo = now },
            new AdministrationUserClientRow { Id = 3, UserId = 42, ClientId = 103, Active = true, ValidFrom = now.AddTicks(1) },
            new AdministrationUserClientRow { Id = 4, UserId = 42, ClientId = 104, Active = false, ValidFrom = now.AddDays(-1) },
            new AdministrationUserClientRow { Id = 5, UserId = 99, ClientId = 105, Active = true, ValidFrom = now.AddDays(-1) }
        };

        var authorizedClientIds = memberships
            .AsQueryable()
            .Where(KpiService.EffectiveClientMembership(42, now))
            .Select(x => x.ClientId)
            .ToArray();

        Assert.Equal([101], authorizedClientIds);
    }

    [Fact]
    public void Effective_client_scope_includes_membership_at_start_and_before_expiry()
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var memberships = new[]
        {
            new AdministrationUserClientRow { Id = 1, UserId = 42, ClientId = 101, Active = true, ValidFrom = now, ValidTo = now.AddTicks(1) }
        };

        var authorizedClientIds = memberships
            .AsQueryable()
            .Where(KpiService.EffectiveClientMembership(42, now))
            .Select(x => x.ClientId)
            .ToArray();

        Assert.Equal([101], authorizedClientIds);
    }

    [Fact]
    public void Assigned_ticket_scope_does_not_bypass_department_team_or_client_boundaries()
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var memberships = new[]
        {
            new AdministrationUserClientRow { Id = 1, UserId = 42, ClientId = 101, Active = true, ValidFrom = now.AddDays(-1) },
            new AdministrationUserClientRow { Id = 2, UserId = 42, ClientId = 102, Active = true, ValidFrom = now.AddDays(-2), ValidTo = now }
        }.AsQueryable();
        var authorizedClients = memberships
            .Where(KpiService.EffectiveClientMembership(42, now))
            .Select(x => x.ClientId)
            .AsQueryable();
        var tickets = new[]
        {
            new TicketRow { Id = 1, CurrentAssignee = 42, ClientId = 101 },
            new TicketRow { Id = 2, CurrentAssignee = 42, CurrentDepartmentId = 20, CurrentTeamId = 21 },
            new TicketRow { Id = 3, CurrentAssignee = 42, ClientId = 102 }
        }.AsQueryable();

        var result = tickets
            .Where(KpiService.AuthorizedTicketScope(42, null, null, authorizedClients))
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1], result);
    }
}
