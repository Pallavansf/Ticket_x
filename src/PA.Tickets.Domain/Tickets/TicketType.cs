namespace PA.Tickets.Domain.Tickets;
public enum TicketType { Request=1, Incident=2, Problem=3, Task=4, Change=5 }
public enum TicketStatus { New=1, Classified=2, Routed=3, InProgress=4, Waiting=5, PendingApproval=6, Resolved=7, Closed=8, Reopened=10, Cancelled=9 }
