namespace PA.Tickets.Domain.Tickets;
public sealed record TicketRelationship(long TicketId,long RelatedTicketId,string RelationType,string? Description,long CreatedBy,DateTimeOffset CreatedAt);
