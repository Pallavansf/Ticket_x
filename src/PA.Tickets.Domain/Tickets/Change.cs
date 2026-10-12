namespace PA.Tickets.Domain.Tickets;

public enum ChangeType
{
    Standard = 1,
    Normal = 2,
    Emergency = 3
}

public enum ChangeImpact
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ChangeRisk
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ChangeStatus
{
    Draft,
    Assessment,
    PendingApproval,
    Approved,
    Scheduled,
    Implementing,
    Failed,
    RollingBack,
    Validating,
    Resolved,
    Closed,
    Reopened,
    Rejected,
    Cancelled
}
