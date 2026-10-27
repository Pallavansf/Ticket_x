using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PA.Tickets.Api.Controllers;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class WorkstreamHRegressionTests
{
    [Fact]
    public async Task TicketsController_rejects_blank_idempotency_key_before_service_call()
    {
        var service = new StubTicketLifecycleService();
        var controller = new TicketsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "42")],
                        authenticationType: "test"))
                }
            }
        };

        var result = await controller.Create(
            new CreateTicketRequest(TicketType.Request, "Missing key", "Description", RequestTypeId: null, FieldValues: null, ServiceId: 10, CategoryId: 11),
            "   ",
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var title = badRequest.Value?.GetType().GetProperty("title")?.GetValue(badRequest.Value) as string;
        Assert.Equal("Idempotency-Key is required.", title);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public void TicketLifecycleService_maps_the_persisted_public_work_item_id_without_using_the_display_number()
    {
        var ticket = Ticket.Restore(
            42,
            "INC-42",
            TicketType.Request,
            TicketStatus.Routed,
            10,
            11,
            "Printer",
            "Description",
            10,
            11,
            null,
            7,
            4,
            null,
            1,
            DateTimeOffset.UtcNow,
            null,
            null,
            false,
            false,
            0,
            null,
            [WorkItem.Restore(9, 42, "WI-9", null, null, 7, 4, "IN_PROGRESS", null, null, null, null, null, null, 12, DateTimeOffset.UtcNow.AddMinutes(-10), null, "PRVT-9F2C1AB8D3E4")],
            null,
            null,
            null);

        var response = TicketLifecycleService.Map(ticket);

        var workItem = Assert.Single(response.WorkItems);
        Assert.Equal("WI-9", workItem.Number);
        Assert.Equal("PRVT-9F2C1AB8D3E4", workItem.PublicId);
        Assert.NotEqual(workItem.Number, workItem.PublicId);
    }

    [Fact]
    public void CommunicationProviderResolver_rejects_unknown_provider_and_non_production_ready_provider_in_production_mode()
    {
        var deterministic = new DeterministicCommunicationProvider();
        var resolver = new CommunicationProviderResolver([deterministic], production: false);

        Assert.Same(deterministic, resolver.Resolve(null, CommunicationChannel.Email));
        Assert.Same(deterministic, resolver.Resolve("deterministic", CommunicationChannel.Email));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new CommunicationProviderResolver([deterministic], production: true).ValidateConfiguration());
        Assert.Contains("no production-ready provider", ex.Message, StringComparison.OrdinalIgnoreCase);

        var validProvider = new ProductionReadyProvider();
        var productionResolver = new CommunicationProviderResolver([validProvider], production: true);
        productionResolver.ValidateConfiguration();
        Assert.Same(validProvider, productionResolver.Resolve("ready", CommunicationChannel.Email));

        var unknownProvider = Assert.Throws<InvalidOperationException>(() =>
            productionResolver.Resolve("unknown-provider", CommunicationChannel.Email));
        Assert.Contains("No communication provider is configured", unknownProvider.Message, StringComparison.OrdinalIgnoreCase);

        var productionRejected = Assert.Throws<InvalidOperationException>(() =>
            new CommunicationProviderResolver([new DeterministicCommunicationProvider()], production: true)
                .Resolve("deterministic", CommunicationChannel.Email));
        Assert.Contains("test-only", productionRejected.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommunicationDeliveryProcessor_skips_provider_when_required_configuration_is_missing_or_invalid()
    {
        var now = DateTimeOffset.UtcNow;
        var cases = new (bool? ChannelEnabled, DateTimeOffset? ChannelEffectiveFrom, DateTimeOffset? ChannelEffectiveTo, bool? ProviderEnabled, DateTimeOffset? ProviderEffectiveFrom, DateTimeOffset? ProviderEffectiveTo)[]
        {
            (null, null, null, null, null, null),
            (true, now.AddMinutes(-5), now.AddMinutes(5), null, null, null),
            (false, now.AddMinutes(-5), now.AddMinutes(5), true, now.AddMinutes(-5), now.AddMinutes(5)),
            (true, now.AddMinutes(5), now.AddMinutes(10), true, now.AddMinutes(-10), now.AddMinutes(10)),
            (true, now.AddMinutes(-10), now.AddMinutes(-5), true, now.AddMinutes(-10), now.AddMinutes(10))
        };

        foreach (var testCase in cases)
        {
            await using var db = CreateDbContext();
            AddChannelConfig(db, testCase.ChannelEnabled, testCase.ChannelEffectiveFrom, testCase.ChannelEffectiveTo);
            AddProviderConfig(db, testCase.ProviderEnabled, testCase.ProviderEffectiveFrom, testCase.ProviderEffectiveTo);
            var store = new StubCommunicationStore(new CommunicationMessage(42, 7, null, CommunicationChannel.Email, "deterministic", null, "person@example.com", "Subject", "Body", null, CommunicationStatus.Queued, 0, "corr", now.AddMinutes(-1), null, "PRVT-9F2C1AB8D3E4"));
            var provider = new TrackingCommunicationProvider();
            var processor = new CommunicationDeliveryProcessor(store, new CommunicationProviderResolver([provider]), new ConfigurationBuilder().Build(), db, new TestFeatureSwitchService(true));

            var count = await processor.ProcessOnceAsync("worker-1", CancellationToken.None);

            Assert.Equal(1, count);
            Assert.Equal(0, provider.Invocations);
            Assert.Equal(1, store.ReleaseCalls);
            Assert.Equal(0, store.CompleteCalls);
        }
    }

    [Fact]
    public async Task CommunicationDeliveryProcessor_calls_provider_when_configuration_is_enabled_and_in_window_with_real_resolver()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateDbContext();
        AddChannelConfig(db, true, now.AddMinutes(-5), now.AddMinutes(30));
        AddProviderConfig(db, true, now.AddMinutes(-5), now.AddMinutes(30));
        var store = new StubCommunicationStore(new CommunicationMessage(42, 7, null, CommunicationChannel.Email, "deterministic", null, "person@example.com", "Subject", "Body", null, CommunicationStatus.Queued, 0, "corr", now.AddMinutes(-1), null, "PRVT-9F2C1AB8D3E4"));
        var provider = new TrackingCommunicationProvider();
        var processor = new CommunicationDeliveryProcessor(store, new CommunicationProviderResolver([provider]), new ConfigurationBuilder().Build(), db, new TestFeatureSwitchService(true));

        var count = await processor.ProcessOnceAsync("worker-2", CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal(1, provider.Invocations);
        Assert.Equal(0, store.ReleaseCalls);
        Assert.Equal(1, store.CompleteCalls);
    }

    [Fact]
    public async Task CommunicationDeliveryProcessor_releases_message_when_channel_feature_switch_is_disabled()
    {
        await using var db = CreateDbContext();
        var store = new StubCommunicationStore(new CommunicationMessage(42, 7, null, CommunicationChannel.Email,
            "deterministic", null, "person@example.com", "Subject", "Body", null, CommunicationStatus.Queued,
            0, "corr", DateTimeOffset.UtcNow, null, "PRVT-9F2C1AB8D3E4"));
        var provider = new TrackingCommunicationProvider();
        var processor = new CommunicationDeliveryProcessor(store, new CommunicationProviderResolver([provider]),
            new ConfigurationBuilder().Build(), db, new TestFeatureSwitchService(false));

        var count = await processor.ProcessOnceAsync("worker-disabled", CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal(0, provider.Invocations);
        Assert.Equal(1, store.ReleaseCalls);
        Assert.Equal(0, store.CompleteCalls);
    }

    private static TicketsDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TicketsDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new TicketsDbContext(options);
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE ""PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG"" (
                ""N_CHANNEL_CONFIG_ID"" INTEGER PRIMARY KEY,
                ""S_CHANNEL"" TEXT NOT NULL,
                ""N_ENABLED"" INTEGER NOT NULL,
                ""D_EFFECTIVE_FROM"" TEXT NOT NULL,
                ""D_EFFECTIVE_TO"" TEXT NULL,
                ""D_UPDATED_AT"" TEXT NOT NULL,
                ""N_UPDATED_BY"" INTEGER NOT NULL,
                ""N_ROW_VERSION"" INTEGER NOT NULL
            );
            CREATE TABLE ""PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG"" (
                ""N_PROVIDER_CONFIG_ID"" INTEGER PRIMARY KEY,
                ""S_PROVIDER_CODE"" TEXT NOT NULL,
                ""S_CHANNEL"" TEXT NOT NULL,
                ""N_ENABLED"" INTEGER NOT NULL,
                ""N_MAX_ATTEMPTS"" INTEGER NOT NULL,
                ""N_RETRY_DELAY_SECONDS"" INTEGER NOT NULL,
                ""D_EFFECTIVE_FROM"" TEXT NOT NULL,
                ""D_EFFECTIVE_TO"" TEXT NULL,
                ""D_UPDATED_AT"" TEXT NOT NULL,
                ""N_UPDATED_BY"" INTEGER NOT NULL,
                ""N_ROW_VERSION"" INTEGER NOT NULL
            );
            CREATE TABLE ""PA_TICKETS"" (""N_TICKET_ID"" INTEGER PRIMARY KEY, ""N_CURRENT_DEPARTMENT_ID"" INTEGER NULL);
            INSERT INTO ""PA_TICKETS"" (""N_TICKET_ID"", ""N_CURRENT_DEPARTMENT_ID"") VALUES (7, NULL);");
        return db;
    }

    private static void AddChannelConfig(TicketsDbContext db, bool? enabled, DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo)
    {
        if (enabled is null && effectiveFrom is null && effectiveTo is null) return;

        db.CommunicationChannelConfigurations.Add(new CommunicationChannelConfigurationRow
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Channel = "EMAIL",
            Enabled = enabled ?? false,
            EffectiveFrom = effectiveFrom ?? DateTimeOffset.UtcNow.AddMinutes(-10),
            EffectiveTo = effectiveTo,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = 1,
            RowVersion = 1
        });
        db.SaveChanges();
    }

    private static void AddProviderConfig(TicketsDbContext db, bool? enabled, DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo)
    {
        if (enabled is null && effectiveFrom is null && effectiveTo is null) return;

        db.CommunicationProviderConfigurations.Add(new CommunicationProviderConfigurationRow
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            ProviderCode = "deterministic",
            Channel = "EMAIL",
            Enabled = enabled ?? false,
            MaxAttempts = 3,
            RetryDelaySeconds = 1,
            EffectiveFrom = effectiveFrom ?? DateTimeOffset.UtcNow.AddMinutes(-10),
            EffectiveTo = effectiveTo,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = 1,
            RowVersion = 1
        });
        db.SaveChanges();
    }

    private sealed class TestFeatureSwitchService(bool enabled) : IFeatureSwitchService
    {
        public Task<IReadOnlyCollection<FeatureSwitchView>> ListAsync(long actor, long? departmentId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<FeatureSwitchView>>([]);
        public Task<FeatureSwitchView> SetAsync(long actor, string code, long? departmentId, bool enabled, int? expectedRowVersion, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsEnabledAsync(string code, long? departmentId, CancellationToken ct) => Task.FromResult(enabled);
        public Task<IReadOnlyCollection<FeatureAvailabilityView>> ForActorAsync(long actor, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<FeatureAvailabilityView>>([]);
    }

    private sealed class TrackingCommunicationProvider : ICommunicationProvider
    {
        public string Code => "deterministic";
        public int Invocations { get; private set; }

        public Task<CommunicationDeliveryResult> SendAsync(ProviderSendRequest request, CancellationToken ct)
        {
            Invocations++;
            return Task.FromResult(new CommunicationDeliveryResult(true, "provider-message-1", null, null, null, true));
        }
    }

    private sealed class ProductionReadyProvider : ICommunicationProvider
    {
        public string Code => "ready";
        public bool IsProductionReady => true;

        public Task<CommunicationDeliveryResult> SendAsync(ProviderSendRequest request, CancellationToken ct)
            => Task.FromResult(new CommunicationDeliveryResult(true, "provider-message-2", null, null, null, true));
    }

    private sealed class StubCommunicationStore(params CommunicationMessage[] messages) : ICommunicationStore
    {
        public int ReleaseCalls { get; private set; }
        public int CompleteCalls { get; private set; }

        public Task<CommunicationMessage> QueueAsync(QueueCommunicationCommand command, long actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<CommunicationView> GetAsync(string publicId, long actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<CommunicationView>> ForTicketAsync(long ticketId, long actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> RetryAsync(string publicId, long actor, string? correlationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> CancelAsync(string publicId, long actor, string? correlationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<CommunicationMessage>> LeaseAsync(string workerId, int limit, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<CommunicationMessage>>(messages);
        public Task ReleaseLeaseAsync(long communicationId, string workerId, CancellationToken ct) { ReleaseCalls++; return Task.CompletedTask; }
        public Task CompleteAsync(long communicationId, string workerId, CommunicationDeliveryResult result, int maxAttempts, TimeSpan retryDelay, string? correlationId, CancellationToken ct) { CompleteCalls++; return Task.CompletedTask; }
    }

    private sealed class StubTicketLifecycleService : ITicketLifecycleService
    {
        public bool CreateCalled { get; private set; }

        public Task<TicketView> CreateAsync(CreateTicketCommand command, CancellationToken ct)
        {
            CreateCalled = true;
            return Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        }

        public Task<TicketView> GetAsync(long id, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> LookupAsync(string identifier, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketSearchPage> SearchAsync(TicketSearchCriteria criteria, long actor, CancellationToken ct) => Task.FromResult(new TicketSearchPage(Array.Empty<TicketView>(), 1, 25, 0));
        public Task<TicketView> ClassifyAsync(long id, long serviceId, long categoryId, long requestTypeId, long priorityId, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> RouteAsync(long id, long? departmentId, long? teamId, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> RequestApprovalAsync(long id, long approver, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> ApproveAsync(long id, long approver, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> ExecuteAsync(long id, long workItemId, long executor, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> ReassignAsync(long id, long workItemId, long assignee, long actor, string? reason, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> WaitAsync(long id, long workItemId, WaitingRequest request, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> ResumeAsync(long id, long workItemId, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> AddEvidenceAsync(long id, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> CompleteWorkAsync(long id, long workItemId, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> ResolveAsync(long id, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> ReopenAsync(long id, string reason, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> CancelAsync(long id, string reason, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> CloseAsync(long id, long actor, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task<TicketView> TransitionSpecializedStatusAsync(long id, TicketStatus status, long actor, string? reason, CancellationToken ct) => Task.FromResult(new TicketView(0, "", TicketType.Request, TicketStatus.New, "", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null));
        public Task EnsureSpecializedStatusTransitionAllowedAsync(long id, TicketStatus status, long actor, CancellationToken ct) => Task.CompletedTask;
        public Task<TicketRelationship> AddRelationshipAsync(long id, long relatedId, string relationType, string? description, long actor, CancellationToken ct) => Task.FromResult(new TicketRelationship(0, 0, "", "", 0, DateTimeOffset.UtcNow));
    }

    private sealed class StubResolver : ICommunicationProviderResolver
    {
        private readonly ICommunicationProvider _provider;

        public StubResolver(ICommunicationProvider provider)
        {
            _provider = provider;
        }

        public void ValidateConfiguration() { }
        public ICommunicationProvider Resolve(string? providerCode, CommunicationChannel channel) => _provider;
    }

    private sealed class TrackingProvider : ICommunicationProvider
    {
        public string Code => "configured-test";
        public bool WasCalled { get; private set; }

        public Task<CommunicationDeliveryResult> SendAsync(ProviderSendRequest request, CancellationToken ct)
        {
            WasCalled = true;
            return Task.FromResult(new CommunicationDeliveryResult(true, "provider-message", null, "OK", null, true));
        }
    }
}
