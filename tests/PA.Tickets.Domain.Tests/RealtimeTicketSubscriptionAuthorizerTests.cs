using PA.Tickets.Api.Realtime;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class RealtimeTicketSubscriptionAuthorizerTests
{
    [Fact]
    public async Task Requires_enabled_module_and_ticket_read_scope_before_subscription()
    {
        var scopes = new StubScopeReader(new RealtimeTicketScope(25, 8, 13, 19, 31));
        var authorization = new StubAuthorization(true);
        var switches = new StubFeatureSwitches(true);
        var sut = new RealtimeTicketSubscriptionAuthorizer(scopes, authorization, switches);

        Assert.True(await sut.CanSubscribeAsync(4, 25, CancellationToken.None));
        Assert.Equal(new AuthorizationResource(25, 8, 13, 19, 31), authorization.Resource);
        Assert.Equal("TICKET_READ", authorization.Permission);
        Assert.Equal(("REALTIME", (long?)8), switches.LastCheck);
    }

    [Fact]
    public async Task Fails_closed_when_module_is_disabled_or_ticket_is_missing()
    {
        var authorization = new StubAuthorization(true);
        var disabled = new RealtimeTicketSubscriptionAuthorizer(
            new StubScopeReader(new RealtimeTicketScope(25, 8, null, null, null)),
            authorization,
            new StubFeatureSwitches(false));
        Assert.False(await disabled.CanSubscribeAsync(4, 25, CancellationToken.None));
        Assert.Null(authorization.Resource);

        var missing = new RealtimeTicketSubscriptionAuthorizer(
            new StubScopeReader(null), authorization, new StubFeatureSwitches(true));
        Assert.False(await missing.CanSubscribeAsync(4, 25, CancellationToken.None));
    }

    private sealed class StubScopeReader(RealtimeTicketScope? result) : IRealtimeTicketScopeReader
    {
        public Task<RealtimeTicketScope?> GetAsync(long ticketId, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class StubAuthorization(bool allowed) : IAuthorizationService
    {
        public AuthorizationResource? Resource { get; private set; }
        public string? Permission { get; private set; }
        public Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct)
        {
            Permission = permission;
            Resource = resource;
            return Task.FromResult(allowed);
        }
    }

    private sealed class StubFeatureSwitches(bool enabled) : IFeatureSwitchService
    {
        public (string Code, long? DepartmentId)? LastCheck { get; private set; }
        public Task<bool> IsEnabledAsync(string code, long? departmentId, CancellationToken ct)
        {
            LastCheck = (code, departmentId);
            return Task.FromResult(enabled);
        }
        public Task<IReadOnlyCollection<FeatureSwitchView>> ListAsync(long actor, long? departmentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<FeatureSwitchView> SetAsync(long actor, string code, long? departmentId, bool value, int? expectedRowVersion, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorAsync(long actor, CancellationToken ct) => throw new NotSupportedException();
    }
}
