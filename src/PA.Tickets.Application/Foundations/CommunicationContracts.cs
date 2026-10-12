using PA.Tickets.Domain.Communications;
namespace PA.Tickets.Application.Foundations;
public sealed record QueueCommunicationCommand(long TicketId,long? WorkItemId,CommunicationChannel Channel,string? ProviderCode,string? TemplateCode,string Recipient,string? Subject,string? Body,string? StructuredPayload,DateTimeOffset? ScheduledAt,string? CorrelationId,string IdempotencyKey,IReadOnlyDictionary<string,string>? TemplateValues=null);
public sealed record CommunicationView(string PublicId,long TicketId,CommunicationChannel Channel,string? ProviderCode,string? TemplateCode,string Recipient,string? Subject,string? Body,CommunicationStatus Status,int AttemptCount,string? ProviderMessageId,string? CorrelationId,DateTimeOffset CreatedAt,DateTimeOffset? SentAt,DateTimeOffset? DeliveredAt,string? FailureCode,string? FailureReason);
public sealed record CommunicationTemplate(string Code,CommunicationChannel Channel,int Version,string? Subject,string Body,string Status,DateTimeOffset EffectiveFrom,DateTimeOffset? EffectiveTo);
public enum CommunicationFailureClassification { Transient, Permanent }
public sealed record CommunicationDeliveryResult(bool Accepted,string? ProviderMessageId,string? FailureReason,string? FailureCode=null,CommunicationFailureClassification? FailureClassification=null,bool Delivered=false);
public sealed record ProviderSendRequest(CommunicationMessage Message,string IdempotencyKey,string? CorrelationId);
public interface ICommunicationProvider { string Code { get; } bool IsProductionReady => true; Task<CommunicationDeliveryResult> SendAsync(ProviderSendRequest request,CancellationToken ct); }
public interface ICommunicationProviderResolver { void ValidateConfiguration(); ICommunicationProvider Resolve(string? providerCode,CommunicationChannel channel); }
public interface IRecipientResolver { Task<string> ResolveAsync(QueueCommunicationCommand command,long actor,CancellationToken ct); }
public interface ICommunicationDeliveryProcessor { Task<int> ProcessOnceAsync(string workerId,CancellationToken ct); }
public interface ICommunicationStore
{
	Task<CommunicationMessage> QueueAsync(QueueCommunicationCommand command,long actor,CancellationToken ct);
	Task<CommunicationView> GetAsync(string publicId,long actor,CancellationToken ct);
	Task<IReadOnlyCollection<CommunicationView>> ForTicketAsync(long ticketId,long actor,CancellationToken ct);
	Task<bool> RetryAsync(string publicId,long actor,string? correlationId,CancellationToken ct);
	Task<bool> CancelAsync(string publicId,long actor,string? correlationId,CancellationToken ct);
	Task<IReadOnlyCollection<CommunicationMessage>> LeaseAsync(string workerId,int limit,TimeSpan leaseDuration,CancellationToken ct);
	Task ReleaseLeaseAsync(long communicationId,string workerId,CancellationToken ct);
	Task CompleteAsync(long communicationId,string workerId,CommunicationDeliveryResult result,int maxAttempts,TimeSpan retryDelay,string? correlationId,CancellationToken ct);
}
public interface ICommunicationService : ICommunicationStore { }
public sealed class CommunicationService(ICommunicationStore store):ICommunicationService
{
	public Task<CommunicationMessage> QueueAsync(QueueCommunicationCommand c,long a,CancellationToken ct)=>store.QueueAsync(c,a,ct);
	public Task<CommunicationView> GetAsync(string id,long a,CancellationToken ct)=>store.GetAsync(id,a,ct);
	public Task<IReadOnlyCollection<CommunicationView>> ForTicketAsync(long id,long a,CancellationToken ct)=>store.ForTicketAsync(id,a,ct);
	public Task<bool> RetryAsync(string id,long a,string? c,CancellationToken ct)=>store.RetryAsync(id,a,c,ct);
	public Task<bool> CancelAsync(string id,long a,string? c,CancellationToken ct)=>store.CancelAsync(id,a,c,ct);
	public Task<IReadOnlyCollection<CommunicationMessage>> LeaseAsync(string w,int l,TimeSpan d,CancellationToken ct)=>store.LeaseAsync(w,l,d,ct);
	public Task ReleaseLeaseAsync(long id,string w,CancellationToken ct)=>store.ReleaseLeaseAsync(id,w,ct);
	public Task CompleteAsync(long id,string w,CommunicationDeliveryResult r,int m,TimeSpan d,string? c,CancellationToken ct)=>store.CompleteAsync(id,w,r,m,d,c,ct);
}
