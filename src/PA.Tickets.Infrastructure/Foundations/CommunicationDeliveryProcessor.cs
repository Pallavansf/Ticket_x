using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PA.Tickets.Infrastructure.Execution;
using Microsoft.Extensions.Configuration;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class CommunicationDeliveryProcessor(
    ICommunicationStore store,
    ICommunicationProviderResolver providers,
    IConfiguration configuration,
    TicketsDbContext db, ILogger<CommunicationDeliveryProcessor>? logger = null) : ICommunicationDeliveryProcessor
{
    public async Task<int> ProcessOnceAsync(string workerId,CancellationToken ct)
    {
        providers.ValidateConfiguration();
        var maxAttempts = configuration.GetValue("Communication:Retry:MaxAttempts",3);
        var delay = TimeSpan.FromSeconds(configuration.GetValue("Communication:Retry:DelaySeconds",5));
        var leased = await store.LeaseAsync(workerId,configuration.GetValue("Communication:Worker:BatchSize",10),TimeSpan.FromSeconds(configuration.GetValue("Communication:Worker:LeaseSeconds",60)),ct);
        foreach (var message in leased)
        {
            var now = DateTimeOffset.UtcNow;
            var channel = message.Channel.ToString().ToUpperInvariant();
            var providerCode = ResolveConfiguredProviderCode(message.ProviderCode);
            var channelConfig = await db.CommunicationChannelConfigurations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Channel == channel, ct);
            var providerConfig = await db.CommunicationProviderConfigurations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProviderCode.ToLower() == providerCode && x.Channel == channel, ct);
            if (!IsActive(channelConfig?.Enabled, channelConfig?.EffectiveFrom, channelConfig?.EffectiveTo, now)
                || !IsActive(providerConfig?.Enabled, providerConfig?.EffectiveFrom, providerConfig?.EffectiveTo, now))
            {
                await store.ReleaseLeaseAsync(message.Id, workerId, ct);
                continue;
            }

            var configuredMaxAttempts = providerConfig?.MaxAttempts ?? maxAttempts;
            var configuredDelay = TimeSpan.FromSeconds(providerConfig?.RetryDelaySeconds ?? (int)delay.TotalSeconds);
            CommunicationDeliveryResult result;
            try { result = await providers.Resolve(message.ProviderCode,message.Channel).SendAsync(new ProviderSendRequest(message,message.IdempotencyKey(),message.CorrelationId),ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException ex) { logger?.LogWarning("Communication provider timeout {CommunicationId} {Channel}", message.Id, channel); result = new(false,null,ex.GetType().Name,"TIMEOUT",CommunicationFailureClassification.Transient); }
            catch (TimeoutException ex) { logger?.LogWarning("Communication provider timeout {CommunicationId} {Channel}", message.Id, channel); result = new(false,null,ex.Message,"TIMEOUT",CommunicationFailureClassification.Transient); }
            catch (HttpRequestException ex) { var retryable = ex.StatusCode is null || ex.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests || (int)ex.StatusCode.Value >= 500; logger?.LogWarning("Communication provider HTTP failure {CommunicationId} {Channel} {Retryable}", message.Id, channel, retryable); result = new(false,null,ex.GetType().Name,"HTTP_ERROR",retryable ? CommunicationFailureClassification.Transient : CommunicationFailureClassification.Permanent); }
            catch (Exception ex) { logger?.LogError("Communication provider failed {CommunicationId} {Channel} {FailureType}", message.Id, channel, ex.GetType().Name); result = new(false,null,ex.GetType().Name,"PROVIDER_ERROR",CommunicationFailureClassification.Permanent); }
            await store.CompleteAsync(message.Id,workerId,result,configuredMaxAttempts,configuredDelay,message.CorrelationId,ct);
        }
        return leased.Count;
    }

    private static bool IsActive(bool? enabled, DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset now)
        => enabled == true && (from is null || from <= now) && (to is null || to > now);

    private static string ResolveConfiguredProviderCode(string? providerCode)
        => providerCode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "transient" or "permanent" or "delivered" or "lost-response" => "deterministic",
            var code => code
        };
}

internal static class CommunicationMessageExtensions
{
    public static string IdempotencyKey(this CommunicationMessage message) => string.IsNullOrWhiteSpace(message.PublicId) ? message.Id.ToString() : message.PublicId;
}
