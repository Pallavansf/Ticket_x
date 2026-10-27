using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Tickets;

internal static class TicketSearchScopePolicy
{
    internal static IQueryable<TicketRow> ApplyOwnOrAssigned(
        IQueryable<TicketRow> tickets,
        long? userId)
    {
        if (userId is not long actorUserId || actorUserId <= 0)
            return tickets;

        return tickets.Where(ticket =>
            ticket.RequestedBy == actorUserId || ticket.CurrentAssignee == actorUserId);
    }
}
