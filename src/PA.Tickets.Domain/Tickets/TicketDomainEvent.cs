namespace PA.Tickets.Domain.Tickets;
public sealed record TicketDomainEvent(string Action,long TicketId,long ActorUserId,string? OldValue,string? NewValue,DateTimeOffset OccurredAt,long? WorkItemId=null,string? Reason=null);
