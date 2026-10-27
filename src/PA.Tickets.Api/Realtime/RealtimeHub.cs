using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Realtime;

[Authorize]
public sealed class RealtimeHub(
    RealtimeTicketSubscriptionAuthorizer subscriptions,
    IFeatureSwitchService featureSwitches,
    RealtimeConnectionSubscriptions connectionSubscriptions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = ActorId();
        var availability = await featureSwitches.ForActorAsync(userId, Context.ConnectionAborted);
        if (!FeatureSwitchResolution.IsEnabledForActor("REALTIME", availability))
        {
            Context.Abort();
            return;
        }
        connectionSubscriptions.Connected(Context.ConnectionId, userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task SubscribeTicket(long ticketId)
    {
        if (ticketId <= 0) throw new HubException("Subscription denied.");
        var userId = ActorId();
        if (!await subscriptions.CanSubscribeAsync(userId, ticketId, Context.ConnectionAborted))
            throw new HubException("Subscription denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Ticket(ticketId), Context.ConnectionAborted);
        if (!connectionSubscriptions.Subscribe(Context.ConnectionId, ticketId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Ticket(ticketId), Context.ConnectionAborted);
            throw new HubException("Subscription denied.");
        }
    }

    public Task UnsubscribeTicket(long ticketId)
    {
        if (ticketId <= 0) throw new HubException("Subscription denied.");
        connectionSubscriptions.Unsubscribe(Context.ConnectionId, ticketId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Ticket(ticketId), Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connectionSubscriptions.Disconnected(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private long ActorId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
            throw new HubException("The authenticated subject is unavailable.");
        return userId;
    }
}
