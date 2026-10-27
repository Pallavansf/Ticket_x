using PA.Tickets.Api.Realtime;

namespace PA.Tickets.Domain.Tests;

public sealed class RealtimeConnectionSubscriptionsTests
{
    [Fact]
    public void Tracks_authenticated_ticket_subscriptions_and_removes_them_on_revoke_or_disconnect()
    {
        var subscriptions = new RealtimeConnectionSubscriptions();

        Assert.False(subscriptions.Subscribe("connection-a", 25));
        subscriptions.Connected("connection-a", 4);
        subscriptions.Connected("connection-b", 5);
        Assert.True(subscriptions.Subscribe("connection-a", 25));
        Assert.True(subscriptions.Subscribe("connection-b", 25));
        Assert.Equal(new[] { ("connection-a", 4L), ("connection-b", 5L) }, subscriptions.ForTicket(25));

        subscriptions.Unsubscribe("connection-a", 25);
        subscriptions.Disconnected("connection-b");

        Assert.Empty(subscriptions.ForTicket(25));
    }
}
