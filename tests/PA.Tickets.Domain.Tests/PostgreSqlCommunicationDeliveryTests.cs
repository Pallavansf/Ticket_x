using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Application;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlCommunicationDeliveryTests
{
    [Fact]
    public void Production_provider_gate_rejects_missing_or_deterministic_providers()
    {
        var deterministic=new CommunicationProviderResolver([new DeterministicCommunicationProvider()],production:true);
        Assert.Throws<InvalidOperationException>(()=>{deterministic.ValidateConfiguration();});
        Assert.Throws<InvalidOperationException>(()=>{deterministic.Resolve("deterministic",CommunicationChannel.Email);});

        var missing=new CommunicationProviderResolver(Array.Empty<ICommunicationProvider>(),production:true);
        Assert.Throws<InvalidOperationException>(()=>{missing.ValidateConfiguration();});
        Assert.Throws<InvalidOperationException>(()=>{missing.Resolve(null,CommunicationChannel.Email);});

        var valid=new CommunicationProviderResolver([new ConfiguredTestProvider()],production:true);
        valid.ValidateConfiguration();
        Assert.Equal("configured-test",valid.Resolve("configured-test",CommunicationChannel.Email).Code);
    }

    [Fact]
    public async Task Communication_template_queue_delivery_and_audit_are_durable()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider();
        var ticketId = 0L;
        var templateId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(1, 500);
        var channelConfigId = templateId + 1;
        var providerConfigId = templateId + 2;
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
            ticketId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Communication {Guid.NewGuid():N}", "slice 3", 1001, 1001), CancellationToken.None)).Id;
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.CommunicationTemplates.Add(new CommunicationTemplateRow { Id = templateId, Code = "WELCOME", Channel = "EMAIL", Version = 1, Subject = "Hello {{TicketNumber}}", Body = "Ticket {{TicketNumber}}: {{TicketTitle}}", Status = "ACTIVE", EffectiveFrom = now.AddMinutes(-1) });
            db.CommunicationChannelConfigurations.Add(new CommunicationChannelConfigurationRow { Id = channelConfigId, Channel = "EMAIL", Enabled = true, EffectiveFrom = now.AddMinutes(-1), UpdatedAt = now, UpdatedBy = 1001, RowVersion = 1 });
            db.CommunicationProviderConfigurations.Add(new CommunicationProviderConfigurationRow { Id = providerConfigId, ProviderCode = "deterministic", Channel = "EMAIL", Enabled = true, MaxAttempts = 3, RetryDelaySeconds = 0, EffectiveFrom = now.AddMinutes(-1), UpdatedAt = now, UpdatedBy = 1001, RowVersion = 1 });
            await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<ICommunicationService>();
            var queued = await service.QueueAsync(new QueueCommunicationCommand(ticketId, null, CommunicationChannel.Email, "delivered", "WELCOME", "person@example.invalid", null, null, null, null, "slice3-correlation", "slice3-communication-1", new Dictionary<string, string> { ["TicketNumber"] = "REQ-TEST", ["TicketTitle"] = "Safe title" }), 1001, CancellationToken.None);
            Assert.Equal(CommunicationStatus.Queued, queued.Status);
            Assert.DoesNotContain("{{", (await service.GetAsync(queued.PublicId, 1001, CancellationToken.None)).Body);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueueAsync(new QueueCommunicationCommand(ticketId, null, CommunicationChannel.Email, "delivered", "WELCOME", "person@example.invalid", null, null, null, null, "slice3-correlation", "slice3-communication-1", new Dictionary<string, string> { ["TicketNumber"] = "changed" }), 1001, CancellationToken.None));
            var processor = scope.ServiceProvider.GetRequiredService<ICommunicationDeliveryProcessor>();
            Assert.True(await processor.ProcessOnceAsync("slice3-worker", CancellationToken.None) >= 1);
            var view = await service.GetAsync(queued.PublicId, 1001, CancellationToken.None);
            Assert.Equal(CommunicationStatus.Delivered, view.Status);
            var history = await service.ForTicketAsync(ticketId, 1001, CancellationToken.None);
            Assert.Single(history, x => x.PublicId == queued.PublicId && x.ProviderMessageId == view.ProviderMessageId);
            Assert.NotNull(view.ProviderMessageId);
            Assert.Equal("REQ-TEST", view.Subject!.Split(' ')[1]);
            Assert.Equal("Safe title", view.Body!.Split(':')[1].Trim());
            Assert.DoesNotContain("Storage", System.Text.Json.JsonSerializer.Serialize(view), StringComparison.OrdinalIgnoreCase);
            Assert.True(await db.CommunicationAttempts.AnyAsync(x => x.CommunicationId == queued.Id && x.Result == "DELIVERED"));
            Assert.True(await db.Events.AnyAsync(x => x.TicketId == ticketId && x.Type == "COMMUNICATION_DELIVERED"));
            Assert.True(await db.Audits.AnyAsync(x => x.EntityType == "COMMUNICATION" && x.EntityId == queued.PublicId));
        }
        finally { await CleanupAsync(provider, ticketId, templateId, channelConfigId, providerConfigId); }
    }

    [Fact]
    public async Task Communication_leasing_retry_and_authorization_are_concurrency_safe()
    {
        PostgreSqlTestEnvironment.EnsureConfigured();
        await using var provider = BuildProvider(true);
        var ticketId = 0L;
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var tickets = scope.ServiceProvider.GetRequiredService<ITicketLifecycleService>();
                ticketId = (await tickets.CreateAsync(new CreateTicketCommand(TicketType.Request, $"Communication retry {Guid.NewGuid():N}", "slice 3", 1001, 1001), CancellationToken.None)).Id;
                var service = scope.ServiceProvider.GetRequiredService<ICommunicationService>();
                var transient = await service.QueueAsync(new QueueCommunicationCommand(ticketId, null, CommunicationChannel.Email, "transient", null, "retry@example.invalid", "Retry", "body", null, null, "retry-correlation", "slice3-transient", null), 1001, CancellationToken.None);
                var permanent = await service.QueueAsync(new QueueCommunicationCommand(ticketId, null, CommunicationChannel.Email, "permanent", null, "bad@example.invalid", "Permanent", "body", null, null, "permanent-correlation", "slice3-permanent", null), 1001, CancellationToken.None);
                var first = await service.LeaseAsync("worker-a", 10, TimeSpan.FromMinutes(1), CancellationToken.None);
                var second = await service.LeaseAsync("worker-b", 10, TimeSpan.FromMinutes(1), CancellationToken.None);
                Assert.Equal(2, first.Count);
                Assert.Empty(second);
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.Communications.Where(x => x.TicketId == ticketId).ExecuteUpdateAsync(x => x.SetProperty(row => row.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
                var recoveredLease = await service.LeaseAsync("worker-c", 10, TimeSpan.FromMinutes(1), CancellationToken.None);
                Assert.Equal(2, recoveredLease.Count);
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(first.Single(x => x.Id == transient.Id).Id, "worker-b", new CommunicationDeliveryResult(true, "wrong", null, null, null, false), 3, TimeSpan.FromSeconds(1), "wrong-worker", CancellationToken.None));
                await service.CompleteAsync(transient.Id, "worker-c", new CommunicationDeliveryResult(false, null, "timeout", "TIMEOUT", CommunicationFailureClassification.Transient), 3, TimeSpan.Zero, "retry-correlation", CancellationToken.None);
                await service.CompleteAsync(permanent.Id, "worker-c", new CommunicationDeliveryResult(false, null, "invalid recipient", "INVALID_RECIPIENT", CommunicationFailureClassification.Permanent), 3, TimeSpan.Zero, "permanent-correlation", CancellationToken.None);
                Assert.Equal(CommunicationStatus.FailedTransient, (await service.GetAsync(transient.PublicId, 1001, CancellationToken.None)).Status);
                Assert.Equal(CommunicationStatus.FailedPermanent, (await service.GetAsync(permanent.PublicId, 1001, CancellationToken.None)).Status);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(transient.PublicId, 2002, CancellationToken.None));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetryAsync(transient.PublicId, 2002, "unauthorized", CancellationToken.None));
                await service.RetryAsync(transient.PublicId, 1001, "authorized", CancellationToken.None);
                var recovered = await service.LeaseAsync("worker-c", 10, TimeSpan.FromMinutes(1), CancellationToken.None);
                Assert.Single(recovered);
            }
        }
        finally { await CleanupAsync(provider, ticketId, 0); }
    }

    private static ServiceProvider BuildProvider(bool restricted = false)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL", ["Communication:Retry:MaxAttempts"] = "3", ["Communication:Retry:DelaySeconds"] = "0" }).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config); services.AddApplication(); services.AddInfrastructure(config);
        if (restricted) services.AddScoped<ITicketAccessAuthorizer, RestrictedAuthorizer>();
        return services.BuildServiceProvider(validateScopes: true);
    }
    private static async Task CleanupAsync(ServiceProvider provider,long ticketId,long templateId,long channelConfigId=0,long providerConfigId=0)
    {
        if (ticketId == 0) return;
        await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"select pa_tickets_maintenance_delete_audit_for_ticket({ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION_ATTEMPT\" where \"N_COMMUNICATION_ID\" in (select \"N_COMMUNICATION_ID\" from \"PA_TICKETS_COMMUNICATION\" where \"N_TICKET_ID\" = {ticketId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION\" where \"N_TICKET_ID\" = {ticketId}");
        if (templateId != 0) await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION_TEMPLATE\" where \"N_TEMPLATE_VERSION_ID\" = {templateId}");
        if (providerConfigId != 0) await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG\" where \"N_PROVIDER_CONFIG_ID\" = {providerConfigId}");
        if (channelConfigId != 0) await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG\" where \"N_CHANNEL_CONFIG_ID\" = {channelConfigId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS_EVENT\" where \"N_TICKET_ID\" = {ticketId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from \"PA_TICKETS\" where \"N_TICKET_ID\" = {ticketId}");
    }
    private sealed class RestrictedAuthorizer : ITicketAccessAuthorizer
    {
        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId);
        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Ensure(actorUserId);
        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => Ensure(actorUserId);
        private static Task Ensure(long actor) => actor == 1001 ? Task.CompletedTask : throw new UnauthorizedAccessException("Ticket access denied.");
    }

    private sealed class ConfiguredTestProvider:ICommunicationProvider
    {
        public string Code=>"configured-test";
        public Task<CommunicationDeliveryResult> SendAsync(ProviderSendRequest request,CancellationToken ct)=>
            Task.FromResult(new CommunicationDeliveryResult(false,null,"test failure","TEST",CommunicationFailureClassification.Permanent));
    }
}
