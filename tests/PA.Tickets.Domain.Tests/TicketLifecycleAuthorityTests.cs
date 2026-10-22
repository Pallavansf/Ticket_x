using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Domain.Tests;

public sealed class TicketLifecycleAuthorityTests
{
    [Fact]
    public void Approval_decision_requires_the_designated_approver_and_pending_state()
    {
        var ticket = Ticket.Create(1, "T-1", TicketType.Request, "Approval authority", null, 1001, 1001, DateTimeOffset.UtcNow);
        var requestedAt = DateTimeOffset.UtcNow;
        ticket.RequestApproval(2001, 1001, requestedAt);

        Assert.Equal(2001, ticket.ApprovalApproverUserId);
        Assert.Throws<InvalidOperationException>(() => ticket.RequestApproval(2003, 1001, requestedAt));
        Assert.Equal(2001, ticket.ApprovalApproverUserId);
        Assert.Throws<InvalidOperationException>(() => ticket.Approve(2002, DateTimeOffset.UtcNow));
        Assert.Equal(TicketStatus.PendingApproval, ticket.Status);
        Assert.False(ticket.Approved);

        ticket.Approve(2001, DateTimeOffset.UtcNow);

        Assert.Equal(TicketStatus.Routed, ticket.Status);
        Assert.True(ticket.Approved);
        Assert.Equal(2001, ticket.UncommittedEvents.Last().ActorUserId);
        Assert.Throws<InvalidOperationException>(() => ticket.Approve(2001, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Approval_cannot_be_decided_before_a_request_exists()
    {
        var ticket = Ticket.Create(1, "T-1", TicketType.Request, "No approval", null, 1001, 1001, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ticket.Approve(2001, DateTimeOffset.UtcNow));
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.False(ticket.Approved);
    }

    [Fact]
    public void Approval_request_rejects_the_previous_executor_as_approver()
    {
        var now = DateTimeOffset.UtcNow;
        var ticket = Ticket.Create(2, "T-2", TicketType.Request, "Executor separation", null, 1001, 1001, now);
        ticket.Classify(10, 20, 30, 40, 1001, now);
        var workItem = ticket.Route(3, "T-2-WI-001", null, null, 1001, now);
        ticket.StartExecution(workItem, 2001, 1001, now);

        Assert.Throws<InvalidOperationException>(() => ticket.RequestApproval(2001, 1001, now));
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public void Ticket_cannot_be_resolved_while_a_work_item_is_incomplete()
    {
        var now = DateTimeOffset.UtcNow;
        var ticket = Ticket.Create(3, "T-3", TicketType.Request, "Incomplete work", null, 1001, 1001, now);
        ticket.Classify(10, 20, 30, 40, 1001, now);
        var workItem = ticket.Route(4, "T-3-WI-1", 50, 60, 1001, now);
        ticket.StartExecution(workItem, 2001, 1001, now);
        ticket.AddEvidence(2001, now);

        Assert.Throws<InvalidOperationException>(() => ticket.Resolve(1001, now));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAt);
        Assert.DoesNotContain(ticket.UncommittedEvents, item => item.Action == "TICKET_RESOLVED");
    }
}
