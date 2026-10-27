using Microsoft.AspNetCore.SignalR;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Realtime;

public sealed class SignalRRealtimePublisher(
    IHubContext<RealtimeHub> hub,
    RealtimeTicketDeliveryFilter deliveryFilter) : IRealtimePublisher
{
    public async Task PublishTicketEventAsync(long ticketId, long? departmentId, RealtimeTicketEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (ticketId <= 0 || message.Id <= 0 || message.Version < 0 ||
            string.IsNullOrWhiteSpace(message.Type) || string.IsNullOrWhiteSpace(message.ChangeKind))
            throw new ArgumentException("A valid minimal real-time event is required.", nameof(message));
        var targets = await deliveryFilter.GetAuthorizedConnectionsAsync(ticketId, departmentId, ct);
        foreach (var connectionId in targets.RevokedConnections)
            await hub.Groups.RemoveFromGroupAsync(connectionId, RealtimeGroups.Ticket(ticketId), ct);

        if (targets.AuthorizedConnections.Count > 0)
            await hub.Clients.Clients(targets.AuthorizedConnections).SendAsync("ticketEvent", message, ct);
    }
}
