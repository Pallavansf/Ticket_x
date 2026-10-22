using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Api;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Domain.Tests;

public sealed class HostedCrossScopeAuthorizationTests
{
    [Fact]
    public async Task Out_of_scope_ticket_close_is_denied_without_mutation_or_success_audit()
    {
        var spectatorFacts = new NonSpectatorFactReader();
        var authorizationFacts = new DepartmentScopedFactsReader();
        var repository = new InMemoryTicketRepository();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t52_providerless_sentinel;Username=t52;Password=t52");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Jobs:Enabled", "false");
                builder.UseSetting("Communication:Worker:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpectatorMembershipReader>();
                    services.AddSingleton<ISpectatorMembershipReader>(spectatorFacts);
                    services.RemoveAll<IAuthorizationFactsReader>();
                    services.AddSingleton<IAuthorizationFactsReader>(authorizationFacts);
                    services.RemoveAll<ITicketRepository>();
                    services.AddSingleton<ITicketRepository>(repository);
                });
            });

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets/9001/close");
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "User");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Cross-scope test ticket", body, StringComparison.Ordinal);
        Assert.Equal(1, spectatorFacts.Calls);
        Assert.Equal(1, authorizationFacts.Calls);
        Assert.Equal(1, repository.GetCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Missing_authorization_actor_fact_is_denied_before_ticket_content_is_returned()
    {
        var spectatorFacts = new NonSpectatorFactReader();
        var authorizationFacts = new MissingActorFactsReader();
        var repository = new InMemoryTicketRepository();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t51_providerless_sentinel;Username=t51;Password=t51");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Jobs:Enabled", "false");
                builder.UseSetting("Communication:Worker:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpectatorMembershipReader>();
                    services.AddSingleton<ISpectatorMembershipReader>(spectatorFacts);
                    services.RemoveAll<IAuthorizationFactsReader>();
                    services.AddSingleton<IAuthorizationFactsReader>(authorizationFacts);
                    services.RemoveAll<ITicketRepository>();
                    services.AddSingleton<ITicketRepository>(repository);
                });
            });

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tickets/9001");
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "User");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Cross-scope test ticket", body, StringComparison.Ordinal);
        Assert.Equal(0, spectatorFacts.Calls);
        Assert.Equal(1, authorizationFacts.Calls);
        Assert.Equal(1, repository.GetCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Authenticated_user_is_allowed_for_ticket_within_department_scope()
    {
        var spectatorFacts = new NonSpectatorFactReader();
        var authorizationFacts = new DepartmentScopedFactsReader(10);
        var repository = new InMemoryTicketRepository();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t50_providerless_sentinel;Username=t50;Password=t50");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Jobs:Enabled", "false");
                builder.UseSetting("Communication:Worker:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpectatorMembershipReader>();
                    services.AddSingleton<ISpectatorMembershipReader>(spectatorFacts);
                    services.RemoveAll<IAuthorizationFactsReader>();
                    services.AddSingleton<IAuthorizationFactsReader>(authorizationFacts);
                    services.RemoveAll<ITicketRepository>();
                    services.AddSingleton<ITicketRepository>(repository);
                });
            });

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tickets/9001");
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "User");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cross-scope test ticket", body, StringComparison.Ordinal);
        Assert.Equal(0, spectatorFacts.Calls);
        Assert.Equal(1, authorizationFacts.Calls);
        Assert.Equal(1, repository.GetCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task Authenticated_user_is_denied_for_ticket_outside_department_scope()
    {
        var spectatorFacts = new NonSpectatorFactReader();
        var authorizationFacts = new DepartmentScopedFactsReader();
        var repository = new InMemoryTicketRepository();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t45_providerless_sentinel;Username=t45;Password=t45");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Jobs:Enabled", "false");
                builder.UseSetting("Communication:Worker:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpectatorMembershipReader>();
                    services.AddSingleton<ISpectatorMembershipReader>(spectatorFacts);
                    services.RemoveAll<IAuthorizationFactsReader>();
                    services.AddSingleton<IAuthorizationFactsReader>(authorizationFacts);
                    services.RemoveAll<ITicketRepository>();
                    services.AddSingleton<ITicketRepository>(repository);
                });
            });

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tickets/9001");
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "User");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Cross-scope test ticket", body, StringComparison.Ordinal);
        Assert.Equal(0, spectatorFacts.Calls);
        Assert.Equal(1, authorizationFacts.Calls);
        Assert.Equal(1, repository.GetCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    private sealed class NonSpectatorFactReader : ISpectatorMembershipReader
    {
        public int Calls { get; private set; }

        public Task<bool> IsActiveSpectatorAsync(long userId, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(false);
        }
    }

    private sealed class DepartmentScopedFactsReader : IAuthorizationFactsReader
    {
        private readonly long scopeDepartmentId;

        public DepartmentScopedFactsReader(long scopeDepartmentId = 20) => this.scopeDepartmentId = scopeDepartmentId;

        public int Calls { get; private set; }

        public Task<AuthorizationFacts> ReadAsync(long actorUserId, string permission, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuthorizationFacts(
                new AuthorizationActorFact(actorUserId, 10, null, true),
                new AuthorizationPermissionFact(91, permission, "TICKETS", "READ", "DEPARTMENT", "READ_ONLY", true, true),
                new[] { new AuthorizationRoleFact(12, RoleAuthorizationPolicy.User, true) },
                new[]
                {
                    new AuthorizationGrantFact(
                        12,
                        RoleAuthorizationPolicy.User,
                        true,
                        true,
                        EffectiveGrantAuthorization.Active,
                        at.AddMinutes(-1),
                        null,
                        permission,
                        "READ_ONLY",
                        "DEPARTMENT",
                        true)
                },
                new[]
                {
                    new AuthorizationScopeFact(
                        21,
                        12,
                        "DEPARTMENT",
                        null,
                        scopeDepartmentId,
                        null,
                        null,
                        null,
                        null,
                        at.AddMinutes(-1),
                        null,
                        true)
                }));
        }
    }

    private sealed class MissingActorFactsReader : IAuthorizationFactsReader
    {
        public int Calls { get; private set; }

        public Task<AuthorizationFacts> ReadAsync(long actorUserId, string permission, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuthorizationFacts(
                null,
                new AuthorizationPermissionFact(91, permission, "TICKETS", "READ", "DEPARTMENT", "READ_ONLY", true, true),
                new[] { new AuthorizationRoleFact(12, RoleAuthorizationPolicy.User, true) },
                new[]
                {
                    new AuthorizationGrantFact(
                        12,
                        RoleAuthorizationPolicy.User,
                        true,
                        true,
                        EffectiveGrantAuthorization.Active,
                        at.AddMinutes(-1),
                        null,
                        permission,
                        "READ_ONLY",
                        "DEPARTMENT",
                        true)
                },
                new[]
                {
                    new AuthorizationScopeFact(
                        21,
                        12,
                        "DEPARTMENT",
                        null,
                        10,
                        null,
                        null,
                        null,
                        null,
                        at.AddMinutes(-1),
                        null,
                        true)
                }));
        }
    }

    private sealed class InMemoryTicketRepository : ITicketRepository
    {
        private readonly Ticket ticket = Ticket.Restore(
            9001,
            "T-9001",
            TicketType.Request,
            TicketStatus.New,
            4107,
            4107,
            "Cross-scope test ticket",
            null,
            null,
            null,
            null,
            null,
            10,
            null,
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            false,
            false,
            0,
            null,
            []);

        public int GetCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public Task<Ticket?> GetAsync(long id, CancellationToken ct)
        {
            GetCalls++;
            return Task.FromResult<Ticket?>(id == ticket.Id ? ticket : null);
        }

        public Task AddAsync(Ticket ticket, CancellationToken ct) => throw new InvalidOperationException("Unexpected ticket insert.");
        public Task SaveAsync(Ticket ticket, CancellationToken ct)
        {
            SaveCalls++;
            throw new InvalidOperationException("Cross-scope denial must precede persistence.");
        }
        public Task AddRelationshipAsync(TicketRelationship relationship, CancellationToken ct) => throw new InvalidOperationException("Unexpected relationship insert.");
        public Task<TicketSearchPage> SearchAsync(TicketSearchCriteria criteria, CancellationToken ct) =>
            Task.FromResult(new TicketSearchPage([], criteria.Page, criteria.PageSize, 0));
    }
}
