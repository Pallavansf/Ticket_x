using System.Collections.Concurrent;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Infrastructure.Persistence;

internal static class TicketEventCommitBoundary
{
    private static readonly ConcurrentDictionary<long, Ticket> Pending = new();

    public static void DeferUntilOuterCommit(Ticket ticket) => Pending[ticket.Id] = ticket;

    public static void ClearAfterSuccessfulCommit(Ticket ticket) => ticket.ClearEvents();

    public static void ClearAfterSuccessfulCommit(long ticketId)
    {
        if (Pending.TryRemove(ticketId, out var ticket))
            ticket.ClearEvents();
    }

    public static void DiscardPending(long ticketId) => Pending.TryRemove(ticketId, out _);
}
