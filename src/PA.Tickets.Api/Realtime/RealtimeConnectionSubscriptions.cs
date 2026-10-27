namespace PA.Tickets.Api.Realtime;

/// <summary>Tracks the authenticated ticket subscriptions held by this single API instance.</summary>
public sealed class RealtimeConnectionSubscriptions
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _connections = new(StringComparer.Ordinal);
    private readonly HashSet<(string ConnectionId, long TicketId)> _tickets = new();

    public void Connected(string connectionId, long actorUserId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || actorUserId <= 0) return;
        lock (_gate) _connections[connectionId] = actorUserId;
    }

    public bool Subscribe(string connectionId, long ticketId)
    {
        if (ticketId <= 0 || string.IsNullOrWhiteSpace(connectionId)) return false;
        lock (_gate)
        {
            if (!_connections.ContainsKey(connectionId)) return false;
            _tickets.Add((connectionId, ticketId));
            return true;
        }
    }

    public void Unsubscribe(string connectionId, long ticketId)
    {
        lock (_gate) _tickets.Remove((connectionId, ticketId));
    }

    public void Disconnected(string connectionId)
    {
        lock (_gate)
        {
            _connections.Remove(connectionId);
            _tickets.RemoveWhere(x => StringComparer.Ordinal.Equals(x.ConnectionId, connectionId));
        }
    }

    public IReadOnlyCollection<(string ConnectionId, long ActorUserId)> ForTicket(long ticketId)
    {
        lock (_gate)
            return _tickets.Where(x => x.TicketId == ticketId && _connections.ContainsKey(x.ConnectionId))
                .Select(x => (x.ConnectionId, _connections[x.ConnectionId]))
                .ToArray();
    }
}
