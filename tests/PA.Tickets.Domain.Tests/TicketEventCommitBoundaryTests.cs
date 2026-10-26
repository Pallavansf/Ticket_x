using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class TicketEventCommitBoundaryTests
{
    [Fact]
    public void Failed_commit_path_does_not_clear_events()
    {
        var ticket = NewTicket();

        Assert.NotEmpty(ticket.UncommittedEvents);

        // A failed commit never reaches the post-commit boundary.
        Assert.NotEmpty(ticket.UncommittedEvents);
    }

    [Fact]
    public void Events_clear_only_after_successful_commit_boundary()
    {
        var ticket = NewTicket();

        TicketEventCommitBoundary.ClearAfterSuccessfulCommit(ticket);

        Assert.Empty(ticket.UncommittedEvents);
    }

    [Fact]
    public void Caller_owned_transaction_keeps_events_until_owner_commits()
    {
        var ticket = NewTicket();

        TicketEventCommitBoundary.DeferUntilOuterCommit(ticket);

        // Nested work does not invoke the boundary; the outer owner commits later.
        Assert.NotEmpty(ticket.UncommittedEvents);

        TicketEventCommitBoundary.ClearAfterSuccessfulCommit(ticket.Id);

        Assert.Empty(ticket.UncommittedEvents);
    }

    [Fact]
    public void Failed_outer_commit_discards_pending_reference_without_clearing_events()
    {
        var ticket = NewTicket();

        TicketEventCommitBoundary.DeferUntilOuterCommit(ticket);
        TicketEventCommitBoundary.DiscardPending(ticket.Id);

        Assert.NotEmpty(ticket.UncommittedEvents);
    }

    private static Ticket NewTicket() =>
        Ticket.Create(1, "REQ-000001", TicketType.Request, "Boundary test", null, 10, 10, DateTimeOffset.UtcNow);
}
