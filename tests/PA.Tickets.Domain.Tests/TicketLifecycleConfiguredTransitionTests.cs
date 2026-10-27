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
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ClassifyAsync(1, 10, 20, 30, 40, 50, CancellationToken.None));

        Assert.Equal(("NEW", "TRIAGE"), setup.Configuration.LastTransition);
        Assert.Equal(TicketStatus.New, setup.Ticket.Status);
        Assert.Null(setup.Ticket.ServiceId);
        Assert.Null(setup.Ticket.CategoryId);
        Assert.Null(setup.Ticket.RequestTypeId);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
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

    [Fact]
    public async Task Denied_approval_transition_preserves_state_fields_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.RequestApprovalAsync(1, 60, 50, CancellationToken.None));

        Assert.Equal(TicketStatus.New, setup.Ticket.Status);
        Assert.False(setup.Ticket.ApprovalRequired);
        Assert.False(setup.Ticket.Approved);
        Assert.Null(setup.Ticket.ApprovalApproverUserId);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_approval_completion_preserves_state_fields_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        setup.Ticket.RequestApproval(60, 50, DateTimeOffset.UtcNow);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ApproveAsync(1, 60, CancellationToken.None));

        Assert.Equal(TicketStatus.PendingApproval, setup.Ticket.Status);
        Assert.True(setup.Ticket.ApprovalRequired);
        Assert.False(setup.Ticket.Approved);
        Assert.Equal(60, setup.Ticket.ApprovalApproverUserId);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_cancellation_preserves_state_fields_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.CancelAsync(1, "duplicate request", 50, CancellationToken.None));

        Assert.Equal(TicketStatus.New, setup.Ticket.Status);
        Assert.Null(setup.Ticket.ResolvedAt);
        Assert.Null(setup.Ticket.ClosedAt);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_close_transition_preserves_state_fields_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        setup.Ticket.TransitionSpecializedStatus(TicketStatus.Resolved, 50, DateTimeOffset.UtcNow, "setup");
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.CloseAsync(1, 50, CancellationToken.None));

        Assert.Equal(TicketStatus.Resolved, setup.Ticket.Status);
        Assert.Null(setup.Ticket.ClosedAt);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_reopen_transition_preserves_state_fields_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        setup.Ticket.TransitionSpecializedStatus(TicketStatus.Resolved, 50, DateTimeOffset.UtcNow, "setup");
        var resolvedAt = setup.Ticket.ResolvedAt;
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ReopenAsync(1, "regression detected", 50, CancellationToken.None));

        Assert.Equal(TicketStatus.Resolved, setup.Ticket.Status);
        Assert.Equal(resolvedAt, setup.Ticket.ResolvedAt);
        Assert.Null(setup.Ticket.ClosedAt);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_route_transition_preserves_routing_state_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        setup.Ticket.Classify(10, 20, 30, 40, 50, DateTimeOffset.UtcNow);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.RouteAsync(1, 70, 80, 50, CancellationToken.None));

        Assert.Equal(TicketStatus.Classified, setup.Ticket.Status);
        Assert.Null(setup.Ticket.CurrentDepartmentId);
        Assert.Null(setup.Ticket.CurrentTeamId);
        Assert.Empty(setup.Ticket.WorkItems);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Persistence_failure_does_not_return_a_successful_lifecycle_result()
    {
        var failure = new InvalidOperationException("persistence failed");
        var setup = CreateService(allowed: true, saveFailure: failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ClassifyAsync(1, 10, 20, 30, 40, 50, CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(1, setup.Repository.SaveCount);
        Assert.Equal(1, setup.Authorizer.MutationChecks);
        Assert.Equal(TicketStatus.Classified, setup.Ticket.Status);
        Assert.Equal(0, setup.Repository.SuccessfulSaveCount);
    }

    [Fact]
    public async Task Denied_specialized_transition_does_not_mutate_before_policy_check()
    {
        var setup = CreateService(allowed: false);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.TransitionSpecializedStatusAsync(1, TicketStatus.Resolved, 50, "resolve", CancellationToken.None));

        Assert.Equal(TicketStatus.New, setup.Ticket.Status);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Reopened)]
    public async Task Denied_specialized_status_matrix_preserves_state_and_events(TicketStatus target)
    {
        var setup = CreateService(allowed: false);
        if (target is TicketStatus.Closed or TicketStatus.Reopened)
            setup.Ticket.TransitionSpecializedStatus(TicketStatus.Resolved, 50, DateTimeOffset.UtcNow, "setup");

        var originalStatus = setup.Ticket.Status;
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.TransitionSpecializedStatusAsync(1, target, 50, "denied", CancellationToken.None));

        Assert.Equal(originalStatus, setup.Ticket.Status);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Specialized_preflight_rejects_denied_target_without_mutation_or_save()
    {
        var setup = CreateService(allowed: false);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.EnsureSpecializedStatusTransitionAllowedAsync(1, TicketStatus.Resolved, 50, CancellationToken.None));

        Assert.Equal(TicketStatus.New, setup.Ticket.Status);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Specialized_preflight_allows_target_without_mutation_or_save()
    {
        var setup = CreateService(allowed: true);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await setup.Service.EnsureSpecializedStatusTransitionAllowedAsync(1, TicketStatus.Resolved, 50, CancellationToken.None);

        Assert.Equal(TicketStatus.New, setup.Ticket.Status);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
        Assert.Equal(("NEW", "RESOLVED"), setup.Configuration.LastTransition);
    }

    [Fact]
    public async Task Authorization_denial_prevents_mutation_persistence_and_audit_attempts()
    {
        var setup = CreateService(allowed: true, authorizer: new TestTicketAccessAuthorizer(denyMutation: true));
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            setup.Service.ClassifyAsync(1, 10, 20, 30, 40, 50, CancellationToken.None));

        Assert.Equal(1, setup.Authorizer.MutationChecks);
        Assert.Equal(TicketStatus.New, setup.Ticket.Status);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
        Assert.Equal(0, setup.Repository.SuccessfulSaveCount);
    }

    [Fact]
    public async Task Execution_rejects_an_executor_outside_the_actor_assignment_scope()
    {
        var setup = CreateExecutionService(allowedExecutor: 60);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            setup.Service.ExecuteAsync(1, 5, 70, 50, CancellationToken.None));

        Assert.Equal((50L, 1L, 10L, 20L, 70L), setup.Authorizer.AssignmentAttempt);
        Assert.Equal(0, setup.Repository.SaveCount);
        Assert.Null(setup.Ticket.CurrentAssignee);
        Assert.Null(setup.Ticket.LastExecutor);
        Assert.Equal(TicketStatus.Routed, setup.Ticket.Status);
        Assert.Null(Assert.Single(setup.Ticket.WorkItems).AssignedToUserId);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
    }

    [Fact]
    public async Task Denied_execution_transition_preserves_assignment_state_events_and_persistence()
    {
        var setup = CreateExecutionService(allowedExecutor: 60, allowedTransition: false);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();
        var item = Assert.Single(setup.Ticket.WorkItems);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ExecuteAsync(1, item.Id, 60, 50, CancellationToken.None));

        Assert.Equal(TicketStatus.Routed, setup.Ticket.Status);
        Assert.Null(setup.Ticket.CurrentAssignee);
        Assert.Null(setup.Ticket.LastExecutor);
        Assert.Null(item.AssignedToUserId);
        Assert.Equal("PENDING", item.Status);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_waiting_transition_preserves_waiting_state_events_and_persistence()
    {
        var setup = CreateService(
            allowed: false,
            waitingReasons: new TestWaitingReasonCatalog());
        var now = DateTimeOffset.UtcNow;
        setup.Ticket.Classify(1, 2, 3, 4, 50, now);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, now);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, now);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.WaitAsync(1, item.Id, new WaitingRequest("CUSTOMER", "awaiting response", null, null), 50, CancellationToken.None));

        Assert.Equal(TicketStatus.InProgress, setup.Ticket.Status);
        Assert.Equal("IN_PROGRESS", item.Status);
        Assert.Null(item.WaitingFor);
        Assert.Null(item.WaitingComment);
        Assert.Null(item.WaitingStartedAt);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Missing_waiting_reason_preserves_state_events_and_persistence()
    {
        var setup = CreateService(allowed: true, waitingReasons: new TestWaitingReasonCatalog());
        var now = DateTimeOffset.UtcNow;
        setup.Ticket.Classify(1, 2, 3, 4, 50, now);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, now);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, now);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            setup.Service.WaitAsync(1, item.Id, new WaitingRequest(" ", null, null, null), 50, CancellationToken.None));

        Assert.Equal(TicketStatus.InProgress, setup.Ticket.Status);
        Assert.Equal("IN_PROGRESS", item.Status);
        Assert.Null(item.WaitingStartedAt);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Unknown_waiting_reason_preserves_state_events_and_persistence()
    {
        var setup = CreateService(allowed: true, waitingReasons: new TestWaitingReasonCatalog());
        var now = DateTimeOffset.UtcNow;
        setup.Ticket.Classify(1, 2, 3, 4, 50, now);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, now);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, now);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.WaitAsync(1, item.Id, new WaitingRequest("UNKNOWN", null, null, null), 50, CancellationToken.None));

        Assert.Equal(TicketStatus.InProgress, setup.Ticket.Status);
        Assert.Equal("IN_PROGRESS", item.Status);
        Assert.Null(item.WaitingStartedAt);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Valid_waiting_reason_transitions_and_saves_once()
    {
        var setup = CreateService(allowed: true, waitingReasons: new TestWaitingReasonCatalog());
        var now = DateTimeOffset.UtcNow;
        setup.Ticket.Classify(1, 2, 3, 4, 50, now);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, now);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, now);

        var result = await setup.Service.WaitAsync(1, item.Id, new WaitingRequest("CUSTOMER", "awaiting response", null, null), 50, CancellationToken.None);

        Assert.Equal(TicketStatus.Waiting, result.Status);
        Assert.Equal("WAITING", item.Status);
        Assert.Equal("CUSTOMER", item.WaitingFor);
        Assert.Single(setup.Ticket.UncommittedEvents, x => x.Action == "WORK_ITEM_WAITING");
        Assert.Equal(1, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_resume_transition_preserves_waiting_state_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        var now = DateTimeOffset.UtcNow;
        setup.Ticket.Classify(1, 2, 3, 4, 50, now);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, now);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, now);
        setup.Ticket.Wait(item, "CUSTOMER", "awaiting response", "evidence-1", now.AddDays(1), 50, now);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();
        var waitingStartedAt = item.WaitingStartedAt;
        var waitingMinutes = item.WaitingMinutes;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ResumeAsync(1, item.Id, 50, CancellationToken.None));

        Assert.Equal(TicketStatus.Waiting, setup.Ticket.Status);
        Assert.Equal("WAITING", item.Status);
        Assert.Equal("CUSTOMER", item.WaitingFor);
        Assert.Equal("awaiting response", item.WaitingComment);
        Assert.Equal("evidence-1", item.WaitingEvidenceReference);
        Assert.Equal(waitingStartedAt, item.WaitingStartedAt);
        Assert.Equal(waitingMinutes, item.WaitingMinutes);
        Assert.Null(item.WaitingResumedAt);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Ticket_read_exposes_active_waiting_age_and_start_time()
    {
        var started = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
        var now = started.AddMinutes(65);
        var setup = CreateService(allowed: true, clock: new FixedTimeProvider(now));
        setup.Ticket.Classify(1, 2, 3, 4, 50, started);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, started);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, started);
        setup.Ticket.Wait(item, "CUSTOMER", "awaiting response", null, null, 50, started);

        var result = await setup.Service.GetAsync(1, 50, CancellationToken.None);

        var view = Assert.Single(result.WorkItems);
        Assert.Equal(started, view.WaitingStartedAt);
        Assert.Equal(65, view.WaitingAgeMinutes);
        Assert.Equal(0, view.WaitingMinutes);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Valid_resume_follows_matrix_and_persists_accumulated_waiting_age()
    {
        var started = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
        var resumed = started.AddMinutes(75);
        var setup = CreateService(allowed: true, clock: new FixedTimeProvider(resumed));
        setup.Ticket.Classify(1, 2, 3, 4, 50, started);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, started);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, started);
        setup.Ticket.Wait(item, "CUSTOMER", "awaiting response", null, null, 50, started);

        var result = await setup.Service.ResumeAsync(1, item.Id, 50, CancellationToken.None);

        Assert.Equal(TicketStatus.InProgress, result.Status);
        Assert.Equal("IN_PROGRESS", item.Status);
        Assert.Equal(75, item.WaitingMinutes);
        Assert.Null(item.WaitingStartedAt);
        Assert.Single(setup.Ticket.UncommittedEvents, x => x.Action == "WORK_ITEM_RESUMED");
        Assert.Equal(1, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Denied_resolution_transition_preserves_resolution_state_events_and_persistence()
    {
        var setup = CreateService(allowed: false);
        var now = DateTimeOffset.UtcNow;
        setup.Ticket.Classify(1, 2, 3, 4, 50, now);
        setup.Ticket.Route(5, "T-1-WI-001", 10, 20, 50, now);
        var item = Assert.Single(setup.Ticket.WorkItems);
        setup.Ticket.StartExecution(item, 60, 50, now);
        setup.Ticket.AddEvidence(50, now);
        setup.Ticket.CompleteWork(item, 50, now);
        var originalEvents = setup.Ticket.UncommittedEvents.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.ResolveAsync(1, 50, CancellationToken.None));

        Assert.Equal(TicketStatus.InProgress, setup.Ticket.Status);
        Assert.Null(setup.Ticket.ResolvedAt);
        Assert.Equal("COMPLETED", item.Status);
        Assert.Equal(originalEvents, setup.Ticket.UncommittedEvents);
        Assert.Equal(0, setup.Repository.SaveCount);
    }

    [Fact]
    public async Task Execution_preserves_authorized_actor_and_executor_identity()
    {
        var setup = CreateExecutionService(allowedExecutor: 60);

        var result = await setup.Service.ExecuteAsync(1, 5, 60, 50, CancellationToken.None);

        Assert.Equal((50L, 1L, 10L, 20L, 60L), setup.Authorizer.AssignmentAttempt);
        Assert.Equal(1, setup.Repository.SaveCount);
        Assert.Equal(TicketStatus.InProgress, result.Status);
        Assert.Equal(60, setup.Ticket.CurrentAssignee);
        Assert.Equal(60, setup.Ticket.LastExecutor);
        Assert.Equal(50, Assert.Single(setup.Ticket.UncommittedEvents, x => x.Action == "WORK_ITEM_STARTED").ActorUserId);
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

    private static (TicketLifecycleService Service, TestTicketRepository Repository, TestStatusConfiguration Configuration, TestTicketAccessAuthorizer Authorizer, Ticket Ticket) CreateService(bool? allowed, Exception? saveFailure = null, TestTicketAccessAuthorizer? authorizer = null, TestTicketIdentityGenerator? identities = null, IWaitingReasonCatalog? waitingReasons = null, TimeProvider? clock = null)
    {
        var ticket = Ticket.Create(1, "T-1", TicketType.Request, "Subject", null, 50, 50, DateTimeOffset.UtcNow);
        var repository = new TestTicketRepository(ticket) { SaveFailure = saveFailure };
        var configuration = new TestStatusConfiguration(allowed);
        authorizer ??= new TestTicketAccessAuthorizer();
        identities ??= new TestTicketIdentityGenerator();
        var service = new TicketLifecycleService(
            repository,
            identities,
            authorizer,
            null!,
            waitingReasons!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            clock ?? TimeProvider.System,
            null!,
            null!,
            configuration,
            new TestPriorityConfiguration());
        return (service, repository, configuration, authorizer, ticket);
    }

    private static (
        TicketLifecycleService Service,
        TestTicketRepository Repository,
        TestTicketAccessAuthorizer Authorizer,
        Ticket Ticket) CreateExecutionService(long allowedExecutor, bool? allowedTransition = true)
    {
        var now = DateTimeOffset.UtcNow;
        var ticket = Ticket.Create(1, "T-1", TicketType.Request, "Subject", null, 50, 50, now);
        ticket.Classify(1, 2, 3, 4, 50, now);
        ticket.Route(5, "T-1-WI-001", 10, 20, 50, now);
        var repository = new TestTicketRepository(ticket);
        var authorizer = new TestTicketAccessAuthorizer(allowedExecutor);
        var service = new TicketLifecycleService(
            repository,
            null!,
            authorizer,
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
            new TestStatusConfiguration(allowedTransition),
            new TestPriorityConfiguration());
        return (service, repository, authorizer, ticket);
    }

    private sealed class TestTicketRepository(Ticket ticket) : ITicketRepository
    {
        public int SaveCount { get; private set; }
        public int SuccessfulSaveCount { get; private set; }
        public Exception? SaveFailure { get; init; }

        public Task<Ticket?> GetAsync(long id, CancellationToken ct) => Task.FromResult<Ticket?>(id == ticket.Id ? ticket : null);

        public Task AddAsync(Ticket value, CancellationToken ct) => throw new NotSupportedException();

        public Task SaveAsync(Ticket value, CancellationToken ct)
        {
            SaveCount++;
            if (SaveFailure is not null) throw SaveFailure;
            SuccessfulSaveCount++;
            return Task.CompletedTask;
        }

        public Task AddRelationshipAsync(TicketRelationship relationship, CancellationToken ct) => throw new NotSupportedException();

        public Task<TicketSearchPage> SearchAsync(TicketSearchCriteria criteria, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TestTicketIdentityGenerator : ITicketIdentityGenerator
    {
        public Task<(long Id, string Number)> NextAsync(TicketType type, CancellationToken ct) => Task.FromResult((2L, "REQ-000002"));
        public Task<long> NextWorkItemIdAsync(CancellationToken ct) => Task.FromResult(2L);
    }

    private sealed class TestWaitingReasonCatalog : IWaitingReasonCatalog
    {
        public Task<WaitingReasonDefinition> ResolveAsync(string code, DateTimeOffset at, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Waiting reason is required.");
            if (code != "CUSTOMER") throw new InvalidOperationException("Waiting reason is not configured.");
            return Task.FromResult(new WaitingReasonDefinition(1, code, true, at.AddMinutes(-1), null, true, false, false, false, new HashSet<TicketStatus> { TicketStatus.InProgress }));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestTicketAccessAuthorizer(long? allowedExecutor = null, bool denyMutation = false) : ITicketAccessAuthorizer
    {
        public (long Actor, long? TicketId, long? DepartmentId, long? TeamId, long? AssigneeId)? AssignmentAttempt { get; private set; }
        public int MutationChecks { get; private set; }
        public bool DenyMutation { get; } = denyMutation;

        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct)
        {
            MutationChecks++;
            if (DenyMutation) throw new UnauthorizedAccessException("Mutation denied.");
            return Task.CompletedTask;
        }

        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct) => Task.CompletedTask;

        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => Task.CompletedTask;

        public Task EnsureCanAssignAsync(
            long actorUserId,
            AuthorizationResource ticketResource,
            long? departmentId,
            long? teamId,
            long? assigneeUserId,
            CancellationToken ct)
        {
            AssignmentAttempt = (
                actorUserId,
                ticketResource.TicketId,
                departmentId,
                teamId,
                assigneeUserId);
            if (assigneeUserId != allowedExecutor)
                throw new UnauthorizedAccessException("The assignment target is outside the actor's assignment scope.");
            return Task.CompletedTask;
        }
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
