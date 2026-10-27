using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Realtime;

public sealed record RealtimeTicketDeliveryTargets(
    IReadOnlyCollection<string> AuthorizedConnections,
    IReadOnlyCollection<string> RevokedConnections);

/// <summary>Rechecks current feature and ticket authorization before publishing to tracked connections.</summary>
public sealed class RealtimeTicketDeliveryFilter(
    IFeatureSwitchService featureSwitches,
    RealtimeTicketSubscriptionAuthorizer subscriptions,
    RealtimeConnectionSubscriptions connectionSubscriptions)
{
    public async Task<RealtimeTicketDeliveryTargets> GetAuthorizedConnectionsAsync(
        long ticketId,
        long? departmentId,
        CancellationToken ct)
    {
        var tracked = connectionSubscriptions.ForTicket(ticketId);
        var revoked = new List<string>();
        if (!await featureSwitches.IsEnabledAsync("REALTIME", departmentId, ct))
        {
            foreach (var (connectionId, _) in tracked)
            {
                connectionSubscriptions.Unsubscribe(connectionId, ticketId);
                revoked.Add(connectionId);
            }
            return new(Array.Empty<string>(), revoked);
        }

        var authorized = new List<string>();
        foreach (var (connectionId, actorUserId) in tracked)
        {
            if (await subscriptions.CanSubscribeAsync(actorUserId, ticketId, ct))
            {
                authorized.Add(connectionId);
                continue;
            }

            connectionSubscriptions.Unsubscribe(connectionId, ticketId);
            revoked.Add(connectionId);
        }

        return new(authorized, revoked);
    }
}
