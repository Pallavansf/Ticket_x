namespace PA.Tickets.Domain.Communications;
public enum CommunicationChannel { Email, WhatsApp, Push, InApp }
public enum CommunicationStatus { Queued, Pending=Queued, Processing, Sent, Delivered, FailedTransient, FailedPermanent, Cancelled }
public sealed record CommunicationMessage(long Id,long TicketId,long? WorkItemId,CommunicationChannel Channel,string? ProviderCode,string? TemplateCode,string? Recipient,string? Subject,string? Body,string? StructuredPayload,CommunicationStatus Status,int RetryCount,string? CorrelationId,DateTimeOffset CreatedAt,DateTimeOffset? ScheduledAt,string PublicId="");
