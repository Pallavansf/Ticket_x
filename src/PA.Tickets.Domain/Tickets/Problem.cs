namespace PA.Tickets.Domain.Tickets;

public enum ProblemStatus
{
    Open = 1,
    Investigating = 2,
    KnownError = 3,
    Resolved = 4,
    Closed = 5,
    Reopened = 6
}

public sealed record Problem
{
    public long TicketId { get; init; }
    public string Statement { get; init; } = "";
    public string? RootCause { get; init; }
    public string? Workaround { get; init; }
    public string? PermanentFix { get; init; }
    public ProblemStatus Status { get; init; }
    public bool IsKnownError { get; init; }

    public static Problem Create(long ticketId, string statement)
    {
        if (ticketId <= 0 || string.IsNullOrWhiteSpace(statement))
            throw new ArgumentException("A Problem ticket and statement are required.");
        return new Problem { TicketId = ticketId, Statement = statement.Trim(), Status = ProblemStatus.Open };
    }
}
