using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Persistence;

[Table("PA_TICKETS_INCIDENT")]
public sealed class IncidentRow
{
    [Key, Column("N_INCIDENT_ID")] public long Id { get; set; }
    [Column("N_TICKET_ID")] public long TicketId { get; set; }
    [Column("S_IMPACT")] public string Impact { get; set; } = "MEDIUM";
    [Column("S_URGENCY")] public string Urgency { get; set; } = "MEDIUM";
    [Column("N_MAJOR_INCIDENT")] public bool IsMajorIncident { get; set; }
    [Column("N_INCIDENT_COMMANDER_USER_ID")] public long? IncidentCommanderUserId { get; set; }
    [Column("D_ACKNOWLEDGED_AT")] public DateTimeOffset? AcknowledgedAt { get; set; }
    [Column("N_ACKNOWLEDGED_BY")] public long? AcknowledgedBy { get; set; }
    [Column("D_RESTORED_AT")] public DateTimeOffset? RestoredAt { get; set; }
    [Column("N_RESTORED_BY")] public long? RestoredBy { get; set; }
    [Column("D_RESOLVED_AT")] public DateTimeOffset? ResolvedAt { get; set; }
    [Column("N_RESOLVED_BY")] public long? ResolvedBy { get; set; }
    [Column("D_CLOSED_AT")] public DateTimeOffset? ClosedAt { get; set; }
    [Column("N_CLOSED_BY")] public long? ClosedBy { get; set; }
    [Column("D_MAJOR_INCIDENT_DECLARED_AT")] public DateTimeOffset? MajorIncidentDeclaredAt { get; set; }
    [Column("N_MAJOR_INCIDENT_DECLARED_BY")] public long? MajorIncidentDeclaredBy { get; set; }
    [Column("D_MAJOR_INCIDENT_RESOLVED_AT")] public DateTimeOffset? MajorIncidentResolvedAt { get; set; }
    [Column("S_ROOT_CAUSE_SUMMARY")] public string? RootCauseSummary { get; set; }
    [Column("S_WORKAROUND_SUMMARY")] public string? WorkaroundSummary { get; set; }
    [Column("S_RESOLUTION_SUMMARY")] public string? ResolutionSummary { get; set; }
    [Column("S_CUSTOMER_IMPACT")] public string? CustomerImpact { get; set; }
    [Column("S_BUSINESS_IMPACT")] public string? BusinessImpact { get; set; }
    [Column("S_SERVICE_IMPACT")] public string? ServiceImpact { get; set; }
    [Column("D_OUTAGE_START_AT")] public DateTimeOffset? OutageStartAt { get; set; }
    [Column("D_OUTAGE_END_AT")] public DateTimeOffset? OutageEndAt { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset UpdatedAt { get; set; }
    [Column("S_IDEMPOTENCY_KEY")] public string? IdempotencyKey { get; set; }
}
