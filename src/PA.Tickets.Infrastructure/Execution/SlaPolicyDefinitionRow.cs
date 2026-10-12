using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PA.Tickets.Infrastructure.Execution;

[Table("PA_TICKETS_SLA_POLICY_DEFINITION")]
public sealed class SlaPolicyDefinitionRow
{
    [Key]
    [Column("N_POLICY_ID")]
    public long Id { get; set; }

    [Column("S_POLICY_CODE")]
    public string Code { get; set; } = "";

    [Column("S_POLICY_NAME")]
    public string Name { get; set; } = "";

    [Column("S_DESCRIPTION")]
    public string? Description { get; set; }

    [Column("N_ACTIVE")]
    public int ActiveFlag { get; set; } = 1;

    [NotMapped]
    public bool Active
    {
        get => ActiveFlag != 0;
        set => ActiveFlag = value ? 1 : 0;
    }

    [Column("D_CREATED_AT")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("N_CREATED_BY")]
    public long CreatedBy { get; set; }

    [ConcurrencyCheck]
    [Column("N_ROW_VERSION")]
    public int RowVersion { get; set; }
}
