using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Application.Tickets;

public static class TicketStatusCodes
{
    public static string ToConfigurationCode(TicketStatus status) => status switch
    {
        TicketStatus.New => "NEW",
        TicketStatus.Classified => "TRIAGE",
        TicketStatus.Routed => "ASSIGNED",
        TicketStatus.InProgress => "IN_PROGRESS",
        TicketStatus.Waiting => "WAITING",
        TicketStatus.PendingApproval => "PENDING_APPROVAL",
        TicketStatus.Resolved => "RESOLVED",
        TicketStatus.Closed => "CLOSED",
        TicketStatus.Cancelled => "CANCELLED",
        TicketStatus.Reopened => "REOPENED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Ticket status has no operational configuration mapping.")
    };
}
