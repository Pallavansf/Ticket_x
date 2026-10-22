using PA.Tickets.Infrastructure.Execution;

namespace PA.Tickets.Domain.Tests;

public sealed class DurableWorkflowAuthorizationTests
{
    [Theory]
    [InlineData(null, 4107)]
    [InlineData(4106L, 4107L)]
    public void Work_item_completion_rejects_an_unassigned_actor(long? assignedTo, long actor)
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            DurableWorkflowExecutionRepository.EnsureAssignedActor(assignedTo, actor));
    }

    [Fact]
    public void Work_item_completion_allows_only_the_assigned_actor()
    {
        var exception = Record.Exception(() =>
            DurableWorkflowExecutionRepository.EnsureAssignedActor(4107, 4107));

        Assert.Null(exception);
    }
}
