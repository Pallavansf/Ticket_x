using PA.Tickets.Application.Foundations;using PA.Tickets.Domain.Communications;
namespace PA.Tickets.Infrastructure.Foundations;
// Provider SDK adapters are intentionally not registered until provider credentials/configuration exist.
public abstract class PendingChannelAdapter(string code):ICommunicationProvider { public string Code=>code; public Task<CommunicationDeliveryResult> SendAsync(ProviderSendRequest request,CancellationToken ct)=>Task.FromException<CommunicationDeliveryResult>(new InvalidOperationException($"No {code} provider is configured; delivery remains pending.")); }
