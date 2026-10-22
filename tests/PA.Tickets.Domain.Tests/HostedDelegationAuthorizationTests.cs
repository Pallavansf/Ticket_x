using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Api;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class HostedDelegationAuthorizationTests
{
    [Fact]
    public async Task Admin_head_without_active_delegation_is_denied_for_target_memberships()
    {
        var spectatorFacts = new NonSpectatorFactReader();
        var authorizationFacts = new AdminHeadAuthorizationFactsReader();
        var delegationFacts = new MissingDelegationFactsReader();
        var targetReader = new ActiveTargetReader();
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t46_providerless_sentinel;Username=t46;Password=t46");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Jobs:Enabled", "false");
                builder.UseSetting("Communication:Worker:Enabled", "false");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpectatorMembershipReader>();
                    services.AddSingleton<ISpectatorMembershipReader>(spectatorFacts);
                    services.RemoveAll<IAuthorizationFactsReader>();
                    services.AddSingleton<IAuthorizationFactsReader>(authorizationFacts);
                    services.RemoveAll<IAdminHeadDelegationFactsReader>();
                    services.AddSingleton<IAdminHeadDelegationFactsReader>(delegationFacts);
                    services.RemoveAll<IAdministrationTargetReader>();
                    services.AddSingleton<IAdministrationTargetReader>(targetReader);
                });
            });

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users/9002/roles");
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "AdminHead");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("AdministrationMembership", body, StringComparison.Ordinal);
        Assert.Equal(0, spectatorFacts.Calls);
        Assert.Equal(1, targetReader.Calls);
        Assert.Equal(1, authorizationFacts.Calls);
        Assert.Equal(1, delegationFacts.Calls);
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

    private sealed class ActiveTargetReader : IAdministrationTargetReader
    {
        public int Calls { get; private set; }
        public Task<ActiveAdministrationTargetFact?> ReadActiveAsync(long userId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<ActiveAdministrationTargetFact?>(
                userId == 9002 ? new ActiveAdministrationTargetFact(9002, 10, null, true) : null);
        }
    }

    private sealed class AdminHeadAuthorizationFactsReader : IAuthorizationFactsReader
    {
        public int Calls { get; private set; }
        public Task<AuthorizationFacts> ReadAsync(long actorUserId, string permission, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuthorizationFacts(
                new AuthorizationActorFact(actorUserId, 10, null, true),
                new AuthorizationPermissionFact(92, permission, "ADMINISTRATION", "ADMIN", "ADMIN_GROUP", "DEPARTMENT_ADMIN", false, true),
                new[] { new AuthorizationRoleFact(13, RoleAuthorizationPolicy.AdminHead, true) },
                new[]
                {
                    new AuthorizationGrantFact(
                        13,
                        RoleAuthorizationPolicy.AdminHead,
                        true,
                        true,
                        EffectiveGrantAuthorization.Active,
                        at.AddMinutes(-1),
                        null,
                        permission,
                        "DEPARTMENT_ADMIN",
                        "ADMIN_GROUP",
                        false)
                },
                new[]
                {
                    new AuthorizationScopeFact(31, 13, "ADMIN_GROUP", null, null, null, null, null, null,
                        at.AddMinutes(-1), null, true)
                }));
        }
    }

    private sealed class MissingDelegationFactsReader : IAdminHeadDelegationFactsReader
    {
        public int Calls { get; private set; }
        public Task<AdminHeadDelegationFacts> ReadAsync(long adminHeadUserId, long targetUserId, DateTimeOffset at, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AdminHeadDelegationFacts(
                TargetIsPrivileged: true,
                TargetIsDelegatedAdminOnly: true,
                HasActiveDelegation: false));
        }
    }
}
