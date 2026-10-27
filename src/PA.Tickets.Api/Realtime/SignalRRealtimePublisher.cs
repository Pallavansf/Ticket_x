using Microsoft.AspNetCore.SignalR;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Realtime;

public sealed class SignalRRealtimePublisher(IHubContext<RealtimeHub> hub, IFeatureSwitchService featureSwitches) : IRealtimePublisher
{
    public async Task PublishTicketEventAsync(long ticketId, long? departmentId, RealtimeTicketEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (ticketId <= 0 || message.Id <= 0 || message.Version < 0 ||
            string.IsNullOrWhiteSpace(message.Type) || string.IsNullOrWhiteSpace(message.ChangeKind))
            throw new ArgumentException("A valid minimal real-time event is required.", nameof(message));
        if (!await featureSwitches.IsEnabledAsync("REALTIME", departmentId, ct)) return;

        await hub.Clients.Group(RealtimeGroups.Ticket(ticketId)).SendAsync("ticketEvent", message, ct);
    }
}
