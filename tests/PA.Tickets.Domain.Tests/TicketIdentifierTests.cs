using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;

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
}
