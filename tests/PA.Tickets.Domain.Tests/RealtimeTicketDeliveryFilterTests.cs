using PA.Tickets.Api.Realtime;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class RealtimeTicketDeliveryFilterTests
{
    [Fact]
    public async Task Removes_tracked_subscriptions_when_ticket_read_is_revoked()
    {
        var tracker = NewTracker();
        var switches = new StubFeatureSwitches(true);
        var authorization = new StubAuthorization(12);
        var filter = NewFilter(tracker, switches, authorization);

        var targets = await filter.GetAuthorizedConnectionsAsync(25, 8, CancellationToken.None);

        Assert.Equal(new[] { "connection-b" }, targets.AuthorizedConnections);
        Assert.Equal(new[] { "connection-a" }, targets.RevokedConnections);
        Assert.Equal(new[] { ("connection-b", 12L) }, tracker.ForTicket(25));
        Assert.Equal("TICKET_READ", authorization.Permission);
    }

    [Fact]
    public async Task Removes_all_tracked_subscriptions_when_realtime_is_disabled()
    {
        var tracker = NewTracker();
        var switches = new StubFeatureSwitches(false);
        var authorization = new StubAuthorization(11, 12);
        var filter = NewFilter(tracker, switches, authorization);

        var targets = await filter.GetAuthorizedConnectionsAsync(25, 8, CancellationToken.None);

        Assert.Empty(targets.AuthorizedConnections);
        Assert.Equal(new[] { "connection-a", "connection-b" }, targets.RevokedConnections);
        Assert.Empty(tracker.ForTicket(25));
        Assert.Null(authorization.Permission);
    }

    [Fact]
    public async Task Unavailable_switch_configuration_throws_and_never_returns_delivery_targets()
    {
        var tracker = NewTracker();
        var filter = NewFilter(tracker, new UnavailableFeatureSwitches(), new StubAuthorization(11, 12));

        await Assert.ThrowsAsync<FeatureSwitchUnavailableException>(
            () => filter.GetAuthorizedConnectionsAsync(25, 8, CancellationToken.None));

        Assert.Equal(2, tracker.ForTicket(25).Count);
    }

    private static RealtimeConnectionSubscriptions NewTracker()
    {
        var tracker = new RealtimeConnectionSubscriptions();
        tracker.Connected("connection-a", 11);
        tracker.Connected("connection-b", 12);
        Assert.True(tracker.Subscribe("connection-a", 25));
        Assert.True(tracker.Subscribe("connection-b", 25));
        return tracker;
    }

    private static RealtimeTicketDeliveryFilter NewFilter(
        RealtimeConnectionSubscriptions tracker,
        IFeatureSwitchService switches,
        StubAuthorization authorization) => new(
            switches,
            new RealtimeTicketSubscriptionAuthorizer(
                new StubScopeReader(new RealtimeTicketScope(25, 8, 13, 19, 31)), authorization, switches),
            tracker);

    private sealed class StubScopeReader(RealtimeTicketScope? result) : IRealtimeTicketScopeReader
    {
        public Task<RealtimeTicketScope?> GetAsync(long ticketId, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class StubAuthorization(params long[] allowedActors) : IAuthorizationService
    {
        public string? Permission { get; private set; }
        public Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct)
        {
            Permission = permission;
            return Task.FromResult(allowedActors.Contains(actorUserId));
        }
    }

    private sealed class StubFeatureSwitches(bool enabled) : IFeatureSwitchService
    {
        public Task<bool> IsEnabledAsync(string code, long? departmentId, CancellationToken ct) => Task.FromResult(enabled);
        public Task<IReadOnlyCollection<FeatureSwitchView>> ListAsync(long actor, long? departmentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<FeatureSwitchView> SetAsync(long actor, string code, long? departmentId, bool value, int? expectedRowVersion, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorAsync(long actor, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class UnavailableFeatureSwitches : IFeatureSwitchService
    {
        public Task<bool> IsEnabledAsync(string code, long? departmentId, CancellationToken ct) =>
            throw new FeatureSwitchUnavailableException(new InvalidOperationException("configuration unavailable"));
        public Task<IReadOnlyCollection<FeatureSwitchView>> ListAsync(long actor, long? departmentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<FeatureSwitchView> SetAsync(long actor, string code, long? departmentId, bool value, int? expectedRowVersion, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorAsync(long actor, CancellationToken ct) => throw new NotSupportedException();
    }
}
