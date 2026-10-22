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

public sealed class HostedSuperAdminScopeAuthorizationTests
{
    [Fact]
    public async Task Super_admin_without_explicit_organization_scope_is_denied_ticket_search()
    {
        var spectatorFacts = new NonSpectatorFactReader();
        var authorizationFacts = new SuperAdminWithoutOrganizationScopeFactsReader();
        var repository = new SpyTicketRepository();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t47_providerless_sentinel;Username=t47;Password=t47");
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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tickets?departmentId=10");
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "SuperAdmin");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("TicketSearchPage", body, StringComparison.Ordinal);
        Assert.Equal(0, spectatorFacts.Calls);
        Assert.Equal(1, authorizationFacts.Calls);
        Assert.Equal(0, repository.GetCalls);
        Assert.Equal(0, repository.SearchCalls);
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

    private sealed class SuperAdminWithoutOrganizationScopeFactsReader : IAuthorizationFactsReader
    {
        public int Calls { get; private set; }
        public Task<AuthorizationFacts> ReadAsync(long actorUserId, string permission, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuthorizationFacts(
                new AuthorizationActorFact(actorUserId, 10, null, true),
                new AuthorizationPermissionFact(93, permission, "TICKETS", "READ", "NONE", "READ_ONLY", true, true),
                new[] { new AuthorizationRoleFact(14, RoleAuthorizationPolicy.SuperAdmin, true) },
                new[]
                {
                    new AuthorizationGrantFact(
                        14,
                        RoleAuthorizationPolicy.SuperAdmin,
                        true,
                        true,
                        EffectiveGrantAuthorization.Active,
                        at.AddMinutes(-1),
                        null,
                        permission,
                        "READ_ONLY",
                        "NONE",
                        true)
                },
                Array.Empty<AuthorizationScopeFact>()));
        }
    }

    private sealed class SpyTicketRepository : ITicketRepository
    {
        public int GetCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public Task<Ticket?> GetAsync(long id, CancellationToken ct)
        {
            GetCalls++;
            throw new InvalidOperationException("The denied request must not load a ticket.");
        }
        public Task AddAsync(Ticket ticket, CancellationToken ct) => throw new InvalidOperationException("Unexpected insert.");
        public Task SaveAsync(Ticket ticket, CancellationToken ct) => throw new InvalidOperationException("Unexpected save.");
        public Task AddRelationshipAsync(TicketRelationship relationship, CancellationToken ct) => throw new InvalidOperationException("Unexpected relationship insert.");
        public Task<TicketSearchPage> SearchAsync(TicketSearchCriteria criteria, CancellationToken ct)
        {
            SearchCalls++;
            throw new InvalidOperationException("The denied request must not search ticket data.");
        }
    }
}
