using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Api;
using PA.Tickets.Api.Controllers;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class HostedRbacAuthorizationTests
{
    [Fact]
    public async Task Actual_program_denies_active_spectator_before_mutating_authentication_service()
    {
        var membershipFact = new ActiveSpectatorFact();
        var localAuthentication = new UnexpectedLocalAuthenticationService();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t43_providerless_sentinel;Username=t43;Password=t43");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Jobs:Enabled", "false");
                builder.UseSetting("Communication:Worker:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpectatorMembershipReader>();
                    services.AddSingleton<ISpectatorMembershipReader>(membershipFact);
                    services.RemoveAll<ILocalAuthenticationService>();
                    services.AddSingleton<ILocalAuthenticationService>(localAuthentication);
                });
            });

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(
                "current-password",
                "new-password-long",
                "new-password-long"))
        };
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "User");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, membershipFact.Calls);
        Assert.Equal(0, localAuthentication.ChangePasswordCalls);
    }

    private sealed class ActiveSpectatorFact : ISpectatorMembershipReader
    {
        public int Calls { get; private set; }

        public Task<bool> IsActiveSpectatorAsync(long userId, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(userId == 4107);
        }
    }

    private sealed class UnexpectedLocalAuthenticationService : ILocalAuthenticationService
    {
        public int ChangePasswordCalls { get; private set; }

        public Task<LocalAuthenticatedUser?> AuthenticateAsync(string loginName, string password, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The hosted mutation slice must not invoke local authentication persistence.");

        public Task<bool> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
        {
            ChangePasswordCalls++;
            throw new InvalidOperationException("The Spectator mutation guard must run before the controller action.");
        }
    }
}
