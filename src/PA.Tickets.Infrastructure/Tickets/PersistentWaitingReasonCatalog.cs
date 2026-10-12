using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Tickets;

public sealed class PersistentWaitingReasonCatalog(TicketsDbContext db) : IWaitingReasonCatalog
{
    public async Task<WaitingReasonDefinition> ResolveAsync(string code, DateTimeOffset at, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Waiting reason is required.");
        var normalized = code.Trim().ToUpperInvariant();
        var row = await db.WaitingReasons.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == normalized && x.Active && x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at), ct)
            ?? throw new InvalidOperationException($"Waiting reason '{normalized}' is not configured or effective.");
        var allowed = row.AllowedFromStatuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse<TicketStatus>(value, true, out var status) ? status : (TicketStatus?)null)
            .Where(status => status is not null)
            .Select(status => status!.Value)
            .ToHashSet();
        return new(row.Id, row.Code, row.Active, row.EffectiveFrom, row.EffectiveTo, row.PauseSla, row.RequireComment, row.RequireEvidence, row.RequireExpectedDate, allowed);
    }
}

public sealed class WaitingReasonRow
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public bool Active { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool PauseSla { get; set; }
    public bool RequireComment { get; set; }
    public bool RequireEvidence { get; set; }
    public bool RequireExpectedDate { get; set; }
    public string AllowedFromStatuses { get; set; } = "InProgress";
}
