namespace PA.Tickets.Infrastructure.Persistence;

public sealed class AuditChainLockRow
{
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
}
