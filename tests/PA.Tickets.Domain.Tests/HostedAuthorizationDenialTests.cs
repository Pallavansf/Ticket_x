using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Api;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class HostedAuthorizationDenialTests
{
    [Fact]
    public async Task Authenticated_user_without_effective_permission_is_denied_before_report_query()
    {
        var spectatorFacts = new NonSpectatorFactReader();
        var authorizationFacts = new NoEffectivePermissionFactsReader();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t44_providerless_sentinel;Username=t44;Password=t44");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Jobs:Enabled", "false");
                builder.UseSetting("Communication:Worker:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpectatorMembershipReader>();
                    services.AddSingleton<ISpectatorMembershipReader>(spectatorFacts);
                    services.RemoveAll<IAuthorizationFactsReader>();
                    services.AddSingleton<IAuthorizationFactsReader>(authorizationFacts);
                });
            });

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reports/ticket-volume/query")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "User");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, spectatorFacts.Calls);
        Assert.Equal(1, authorizationFacts.Calls);
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

    private sealed class NoEffectivePermissionFactsReader : IAuthorizationFactsReader
    {
        public int Calls { get; private set; }

        public Task<AuthorizationFacts> ReadAsync(
            long actorUserId,
            string permission,
            DateTimeOffset at,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuthorizationFacts(
                new AuthorizationActorFact(actorUserId, null, null, true),
                new AuthorizationPermissionFact(
                    91,
                    permission,
                    "TICKETS",
                    "READ",
                    "NONE",
                    "READ_ONLY",
                    true,
                    true),
                new[] { new AuthorizationRoleFact(12, "UNSUPPORTED_ROLE", true) },
                new[]
                {
                    new AuthorizationGrantFact(
                        12,
                        "UNSUPPORTED_ROLE",
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
}
