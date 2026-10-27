namespace PA.Tickets.Infrastructure.Foundations;

public sealed class FeatureSwitchRow
{
    public required string Code { get; set; }
    public required string ScopeKey { get; set; }
    public long? DepartmentId { get; set; }
    public bool Enabled { get; set; }
    public int RowVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long UpdatedBy { get; set; }
}
