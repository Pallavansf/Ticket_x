using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api.Realtime;

[Authorize]
public sealed class RealtimeHub(
    RealtimeTicketSubscriptionAuthorizer subscriptions,
    IFeatureSwitchService featureSwitches) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!await featureSwitches.IsEnabledAsync("REALTIME", null, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }
        var userId = ActorId();
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
    }

    public Task UnsubscribeTicket(long ticketId)
    {
        if (ticketId <= 0) throw new HubException("Subscription denied.");
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Ticket(ticketId), Context.ConnectionAborted);
    }

    private long ActorId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
            throw new HubException("The authenticated subject is unavailable.");
        return userId;
    }
}
