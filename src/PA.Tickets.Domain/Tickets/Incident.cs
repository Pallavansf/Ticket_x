namespace PA.Tickets.Domain.Tickets;

public enum IncidentImpact { Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum IncidentUrgency { Low = 1, Medium = 2, High = 3, Critical = 4 }

public sealed record Incident
{
    public long TicketId { get; init; }
    public IncidentImpact Impact { get; private set; }
    public IncidentUrgency Urgency { get; private set; }
    public bool IsMajorIncident { get; private set; }
    public long? IncidentCommanderUserId { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public long? AcknowledgedBy { get; private set; }
    public DateTimeOffset? RestoredAt { get; private set; }
    public long? RestoredBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public long? ResolvedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public long? ClosedBy { get; private set; }
    public DateTimeOffset? MajorIncidentDeclaredAt { get; private set; }
    public long? MajorIncidentDeclaredBy { get; private set; }
    public DateTimeOffset? MajorIncidentResolvedAt { get; private set; }
    public string? RootCauseSummary { get; private set; }
    public string? WorkaroundSummary { get; private set; }
    public string? ResolutionSummary { get; private set; }
    public string? CustomerImpact { get; private set; }
    public string? BusinessImpact { get; private set; }
    public string? ServiceImpact { get; private set; }
    public DateTimeOffset? OutageStartAt { get; private set; }
    public DateTimeOffset? OutageEndAt { get; private set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Incident Create(long ticketId, IncidentImpact impact, IncidentUrgency urgency, long? commanderUserId, DateTimeOffset now)
        => new()
        {
            TicketId = ticketId,
            Impact = impact,
            Urgency = urgency,
            IsMajorIncident = false,
            IncidentCommanderUserId = commanderUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

    public Incident UpdateImpact(IncidentImpact impact, DateTimeOffset now)
    {
        Impact = impact;
        UpdatedAt = now;
        return this;
    }

    public Incident UpdateUrgency(IncidentUrgency urgency, DateTimeOffset now)
    {
        Urgency = urgency;
        UpdatedAt = now;
        return this;
    }

    public Incident Acknowledge(long userId, DateTimeOffset now)
    {
        AcknowledgedAt = now;
        AcknowledgedBy = userId;
        UpdatedAt = now;
        return this;
    }

    public Incident AssignCommander(long userId, DateTimeOffset now)
    {
        IncidentCommanderUserId = userId;
        UpdatedAt = now;
        return this;
    }

    public Incident DeclareMajor(long userId, DateTimeOffset now)
    {
        if (IsMajorIncident) throw new InvalidOperationException("Major incident was already declared.");
        IsMajorIncident = true;
        MajorIncidentDeclaredAt = now;
        MajorIncidentDeclaredBy = userId;
        UpdatedAt = now;
        return this;
    }

    public Incident Resolve(string? rootCauseSummary, string? workaroundSummary, string? resolutionSummary, DateTimeOffset now)
    {
        RootCauseSummary = rootCauseSummary;
        WorkaroundSummary = workaroundSummary;
        ResolutionSummary = resolutionSummary;
        ResolvedAt = now;
        UpdatedAt = now;
        return this;
    }

    public Incident Restore(DateTimeOffset now, long userId)
    {
        RestoredAt = now;
        RestoredBy = userId;
        UpdatedAt = now;
        return this;
    }

    public Incident Reopen(DateTimeOffset now)
    {
        ResolvedAt = null;
        UpdatedAt = now;
        return this;
    }

    public Incident Close(DateTimeOffset now, long userId)
    {
        ClosedAt = now;
        ClosedBy = userId;
        UpdatedAt = now;
        return this;
    }
}
