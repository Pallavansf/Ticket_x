using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class TicketIdentifierTests
{
    [Theory]
    [InlineData("REQ-000123", "REQ-000123")]
    [InlineData("inc-1000000", "INC-1000000")]
    [InlineData("PRB-000001", "PRB-000001")]
    public void Ticket_numbers_are_normalized_and_allow_widening(string input, string expected)
    {
        Assert.Equal(expected, TicketIdentifier.NormalizeForLookup(input));
        Assert.True(TicketIdentifier.IsTicketNumber(expected));
    }

    [Fact]
    public void Sixteen_character_public_id_is_preserved()
    {
        const string publicId = "ABCDEFGHJKLMNPQR";

        Assert.Equal(publicId, TicketIdentifier.NormalizeForLookup(publicId));
        Assert.False(TicketIdentifier.IsTicketNumber(publicId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("REQ-12345")]
    [InlineData("ABC-000001")]
    [InlineData("123456789012345")]
    [InlineData("12345678901234567")]
    [InlineData("ABCDEFGHJKLMNPQO")]
    [InlineData("ABCDEFGHJKLMNPQ1")]
    [InlineData("ABCDEFGHJKLMNPQ!")]
    public void Unsupported_identifier_formats_are_rejected(string input)
    {
        Assert.Throws<ArgumentException>(() => TicketIdentifier.NormalizeForLookup(input));
    }

    [Theory]
    [InlineData("inc-1000000", "INC-1000000", true)]
    [InlineData("ABCDEFGHJKLMNPQR", "ABCDEFGHJKLMNPQR", false)]
    public async Task Lookup_uses_exact_identifier_and_checks_ticket_access(string input, string expected, bool isTicketNumber)
    {
        var ticket = Ticket.Create(42, "INC-1000000", TicketType.Incident, "Subject", null, 7, 7, DateTimeOffset.UtcNow);
        var repository = new LookupRepository(ticket);
        var authorizer = new LookupAuthorizer();
        var service = new TicketLifecycleService(repository, null!, authorizer, null!, null!, null!, null!, null!, null!, null!, null!, null!, TimeProvider.System, null!, null!);

        var result = await service.LookupAsync(input, 7, CancellationToken.None);

        Assert.Equal(ticket.Id, result.Id);
        Assert.Equal((expected, isTicketNumber), repository.Lookup);
        Assert.Equal((7L, ticket.Id), authorizer.AccessCheck);
    }

    [Fact]
    public async Task Lookup_rejects_a_ticket_the_actor_cannot_access()
    {
        var ticket = Ticket.Create(42, "INC-1000000", TicketType.Incident, "Subject", null, 7, 7, DateTimeOffset.UtcNow);
        var authorizer = new LookupAuthorizer { DenyAccess = true };
        var service = new TicketLifecycleService(new LookupRepository(ticket), null!, authorizer, null!, null!, null!, null!, null!, null!, null!, null!, null!, TimeProvider.System, null!, null!);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LookupAsync("INC-1000000", 8, CancellationToken.None));
        Assert.Equal((8L, ticket.Id), authorizer.AccessCheck);
    }

    [Fact]
    public async Task Lookup_uses_user_own_or_assigned_authorization_for_the_loaded_ticket()
    {
        await using var db = new TicketsDbContext(new DbContextOptionsBuilder<TicketsDbContext>().Options);
        var now = DateTimeOffset.UtcNow;
        var facts = new AuthorizationFacts(
            new AuthorizationActorFact(42, null, null, true),
            new AuthorizationPermissionFact(3, "TICKET_READ", "TICKETS", "VIEW", "NONE", "READ_ONLY", true, true),
            [new AuthorizationRoleFact(7, "USER", true, 70)],
            [new AuthorizationGrantFact(7, "USER", true, true, PA.Tickets.Application.Foundations.EffectiveGrantAuthorization.Active,
                now.AddMinutes(-1), null, "TICKET_READ", "READ_ONLY", "NONE", true)],
            []);
        var authorization = new DatabaseAuthorizationService(db, TimeProvider.System,
            new LookupFactsReader(facts), new LookupDelegationFactsReader());
        var access = new DatabaseTicketAccessAuthorizer(authorization, db, TimeProvider.System);
        var own = Ticket.Create(42, "INC-1000000", TicketType.Incident, "Own", null, 42, 42, now);
        var unrelated = Ticket.Create(43, "INC-1000001", TicketType.Incident, "Unrelated", null, 9, 9, now);
        var ownService = new TicketLifecycleService(new LookupRepository(own), null!, access, null!, null!, null!, null!, null!, null!, null!, null!, null!, TimeProvider.System, null!, null!);
        var unrelatedService = new TicketLifecycleService(new LookupRepository(unrelated), null!, access, null!, null!, null!, null!, null!, null!, null!, null!, null!, TimeProvider.System, null!, null!);

        Assert.Equal(own.Id, (await ownService.LookupAsync(own.Number, 42, CancellationToken.None)).Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unrelatedService.LookupAsync(unrelated.Number, 42, CancellationToken.None));
    }

    [Fact]
    public async Task Lookup_reports_unknown_identifiers_as_not_found()
    {
        var repository = new LookupRepository(null);
        var service = new TicketLifecycleService(repository, null!, new LookupAuthorizer(), null!, null!, null!, null!, null!, null!, null!, null!, null!, TimeProvider.System, null!, null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.LookupAsync("INC-1000000", 7, CancellationToken.None));
        Assert.Equal(("INC-1000000", true), repository.Lookup);
    }

    [Fact]
    public async Task Lookup_rejects_malformed_identifier_before_repository_access()
    {
        var repository = new LookupRepository(null);
        var service = new TicketLifecycleService(repository, null!, new LookupAuthorizer(), null!, null!, null!, null!, null!, null!, null!, null!, null!, TimeProvider.System, null!, null!);

        await Assert.ThrowsAsync<ArgumentException>(() => service.LookupAsync("bad-id", 7, CancellationToken.None));
        Assert.Null(repository.Lookup);
    }

    private sealed class LookupRepository(Ticket? ticket) : ITicketRepository
    {
        public (string Identifier, bool IsTicketNumber)? Lookup { get; private set; }
        public Task<Ticket?> GetAsync(long id, CancellationToken ct) => Task.FromResult<Ticket?>(null);
        public Task<Ticket?> FindByIdentifierAsync(string normalizedIdentifier, bool isTicketNumber, CancellationToken ct)
        {
            Lookup = (normalizedIdentifier, isTicketNumber);
            return Task.FromResult(ticket);
        }
        public Task AddAsync(Ticket value, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveAsync(Ticket value, CancellationToken ct) => throw new NotSupportedException();
        public Task AddRelationshipAsync(TicketRelationship relationship, CancellationToken ct) => throw new NotSupportedException();
        public Task<TicketSearchPage> SearchAsync(TicketSearchCriteria criteria, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class LookupAuthorizer : ITicketAccessAuthorizer
    {
        public bool DenyAccess { get; init; }
        public (long Actor, long TicketId)? AccessCheck { get; private set; }
        public Task EnsureCanMutateAsync(long actorUserId, Ticket ticket, CancellationToken ct) => throw new NotSupportedException();
        public Task EnsureCanAccessAsync(long actorUserId, Ticket ticket, CancellationToken ct)
        {
            AccessCheck = (actorUserId, ticket.Id);
            return DenyAccess ? Task.FromException(new UnauthorizedAccessException()) : Task.CompletedTask;
        }
        public Task EnsureCanSearchAsync(long actorUserId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class LookupFactsReader(AuthorizationFacts facts) : IAuthorizationFactsReader
    {
        public Task<AuthorizationFacts> ReadAsync(long actorUserId, string permission, DateTimeOffset at, CancellationToken cancellationToken) => Task.FromResult(facts);
    }

    private sealed class LookupDelegationFactsReader : IAdminHeadDelegationFactsReader
    {
        public Task<AdminHeadDelegationFacts> ReadAsync(long adminHeadUserId, long targetUserId, DateTimeOffset at, CancellationToken cancellationToken) =>
            Task.FromResult(new AdminHeadDelegationFacts(false, false, false));
    }
}
