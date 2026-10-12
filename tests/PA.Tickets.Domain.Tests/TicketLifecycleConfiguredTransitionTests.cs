using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Domain.Tests;

public sealed class TicketLifecycleConfiguredTransitionTests
{
    [Fact]
    public async Task Configured_allowed_transition_is_consulted_and_saved()
    {
        var setup = CreateService(allowed: true);

        var result = await setup.Service.ClassifyAsync(1, 10, 20, 30, 40, 50, CancellationToken.None);

        Assert.Equal(TicketStatus.Classified, result.Status);
        Assert.Equal(("NEW", "TRIAGE"), setup.Configuration.LastTransition);
        Assert.Equal(1, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Configured_denied_transition_is_rejected_before_save()
    {
        var setup = CreateService(allowed: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ClassifyAsync(1, 10, 20, 30, 40, 50, CancellationToken.None));

        Assert.Equal(("NEW", "TRIAGE"), setup.Configuration.LastTransition);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Missing_configured_transition_fails_closed()
    {
        var setup = CreateService(allowed: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ClassifyAsync(1, 10, 20, 30, 40, 50, CancellationToken.None));

        Assert.Equal(("NEW", "TRIAGE"), setup.Configuration.LastTransition);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Theory]
    [InlineData(TicketStatus.New, "NEW")]
    [InlineData(TicketStatus.Classified, "TRIAGE")]
    [InlineData(TicketStatus.Routed, "ASSIGNED")]
    [InlineData(TicketStatus.InProgress, "IN_PROGRESS")]
    [InlineData(TicketStatus.Waiting, "WAITING")]
    [InlineData(TicketStatus.PendingApproval, "PENDING_APPROVAL")]
    [InlineData(TicketStatus.Resolved, "RESOLVED")]
    [InlineData(TicketStatus.Closed, "CLOSED")]
    [InlineData(TicketStatus.Cancelled, "CANCELLED")]
    [InlineData(TicketStatus.Reopened, "REOPENED")]
    public void Existing_domain_statuses_have_canonical_configuration_codes(TicketStatus status, string code)
    {
        Assert.Equal(code, TicketStatusCodes.ToConfigurationCode(status));
    }

    private static (TicketLifecycleService Service, TestTicketRepository Repository, TestStatusConfiguration Configuration) CreateService(bool? allowed)
    {
        var repository = new TestTicketRepository(
            Ticket.Create(1, "T-1", TicketType.Request, "Subject", null, 50, 50, DateTimeOffset.UtcNow));
        var configuration = new TestStatusConfiguration(allowed);
        var service = new TicketLifecycleService(
            repository,
            null!,
            new TestTicketAccessAuthorizer(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            TimeProvider.System,
            null!,
            null!,
            configuration,
            new TestPriorityConfiguration());
        return (service, repository, configuration);
    }

    private sealed class TestTicketRepository(Ticket ticket) : ITicketRepository
    {
        public int SaveCount { get; private set; }

        public Task<Ticket?> GetAsync(long id, CancellationToken ct) => Task.FromResult<Ticket?>(id == ticket.Id ? ticket : null);

        public Task AddAsync(Ticket value, CancellationToken ct) => throw new NotSupportedException();

        public Task SaveAsync(Ticket value, CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddRelationshipAsync(TicketRelationship relationship, CancellationToken ct) => throw new NotSupportedException();

        public Task<TicketSearchPage> SearchAsync(TicketSearchCriteria criteria, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TestTicketAccessAuthorizer : ITicketAccessAuthorizer
    {
        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Task.CompletedTask;

        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Task.CompletedTask;

        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TestStatusConfiguration(bool? allowed) : ITicketStatusConfigurationRepository
    {
        public (string From, string To)? LastTransition { get; private set; }

        public Task<IReadOnlyCollection<StatusDefinitionView>> ListStatusesAsync(long actor, string? search, CancellationToken ct) => throw new NotSupportedException();

        public Task<StatusDefinitionView> GetStatusAsync(long actor, long statusId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsStatusActiveAsync(long actor, string code, CancellationToken ct) => Task.FromResult(true);

        public Task<StatusDefinitionView> CreateStatusAsync(long actor, string code, string name, string? description, bool isInitial, bool isOpen, bool isClosed, bool isCancelled, int displayOrder, string? reason, CancellationToken ct) => throw new NotSupportedException();

        public Task<StatusDefinitionView> UpdateStatusAsync(long actor, long statusId, string code, string name, string? description, bool isInitial, bool isOpen, bool isClosed, bool isCancelled, int displayOrder, string? reason, CancellationToken ct, int? expectedRowVersion = null) => throw new NotSupportedException();

        public Task<StatusDefinitionView> SetStatusActiveAsync(long actor, long statusId, bool active, string? reason, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<StatusTransitionDefinition>> ListTransitionsAsync(long actor, CancellationToken ct) => throw new NotSupportedException();

        public Task<StatusTransitionDefinition> SetTransitionAsync(long actor, string fromCode, string toCode, string? condition, bool isAllowed, string? reason, CancellationToken ct, int? expectedRowVersion = null) => throw new NotSupportedException();

        public Task<bool> CanTransitionAsync(long actor, string fromCode, string toCode, CancellationToken ct)
        {
            LastTransition = (fromCode, toCode);
            return Task.FromResult(allowed is true);
        }

        public Task<IReadOnlyCollection<StatusConfigurationAudit>> GetStatusAuditAsync(long actor, long statusId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TestPriorityConfiguration : ITicketPriorityConfigurationRepository
    {
        public Task<IReadOnlyCollection<PriorityDefinitionView>> ListPrioritiesAsync(long actor, string? search, CancellationToken ct) => throw new NotSupportedException();
        public Task<PriorityDefinitionView> GetPriorityAsync(long actor, long priorityId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PriorityDefinitionView> CreatePriorityAsync(long actor, string code, string name, string? description, int level, int displayOrder, bool isDefault, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<PriorityDefinitionView> UpdatePriorityAsync(long actor, long priorityId, string code, string name, string? description, int level, int displayOrder, bool isDefault, string? reason, CancellationToken ct, int? expectedRowVersion = null) => throw new NotSupportedException();
        public Task<PriorityDefinitionView> SetPriorityActiveAsync(long actor, long priorityId, bool active, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<PriorityDefinitionView> SetDefaultPriorityAsync(long actor, long priorityId, string? reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<PriorityDefinitionView> ResolvePriorityForSelectionAsync(long actor, long? priorityId, CancellationToken ct) =>
            Task.FromResult(new PriorityDefinitionView(priorityId ?? 2, "MEDIUM", "Medium", null, 2, 2, true, true, 1));
        public Task<IReadOnlyCollection<PriorityConfigurationAudit>> GetPriorityAuditAsync(long actor, long priorityId, CancellationToken ct) => throw new NotSupportedException();
    }
}
