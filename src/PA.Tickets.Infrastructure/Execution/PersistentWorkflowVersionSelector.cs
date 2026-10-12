using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Workflow;

namespace PA.Tickets.Infrastructure.Execution;

public sealed class PersistentWorkflowVersionSelector(ExecutionDbContext db) : IWorkflowVersionSelector
{
    public async Task<EffectiveWorkflowVersion> ResolveAsync(long workflowId, DateTimeOffset at, CancellationToken ct)
    {
        if (!await db.WorkflowDefinitions.AsNoTracking().AnyAsync(x => x.Id == workflowId && x.Active, ct)) throw new KeyNotFoundException("Active workflow was not found.");
        var rows = await db.WorkflowVersions.AsNoTracking().Where(x => x.WorkflowId == workflowId && x.Status == "PUBLISHED").ToListAsync(ct);
        var effective = rows.Select(row => new { Row = row, Dates = ReadDates(row.Definition) })
            .Where(x => (x.Dates.From == null || x.Dates.From <= at) && (x.Dates.To == null || x.Dates.To > at))
            .OrderByDescending(x => x.Dates.From)
            .ThenByDescending(x => x.Row.VersionNo)
            .ToList();
        if (effective.Count == 0) throw new KeyNotFoundException("No effective published workflow version was found.");
        if (effective.Count > 1) throw new InvalidOperationException("Workflow version configuration is ambiguous for the effective date.");
        var best = effective[0];
        return new(best.Row.Id, best.Row.WorkflowId, best.Row.VersionNo);
    }

    public async Task<EffectiveWorkflowVersion> ResolveVersionAsync(long workflowId, long versionId, DateTimeOffset at, CancellationToken ct)
    {
        if (!await db.WorkflowDefinitions.AsNoTracking().AnyAsync(x => x.Id == workflowId && x.Active, ct)) throw new KeyNotFoundException("Active workflow was not found.");
        var row = await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.WorkflowId == workflowId && x.Status == "PUBLISHED", ct)
            ?? throw new KeyNotFoundException("Published workflow version was not found for this workflow.");
        var dates = ReadDates(row.Definition);
        if (dates.From > at || dates.To <= at) throw new InvalidOperationException("The selected workflow version is not effective for the requested date.");
        return new(row.Id, row.WorkflowId, row.VersionNo);
    }

    public async Task ValidateRangeAsync(long workflowId,long versionId,DateTimeOffset from,DateTimeOffset? to,CancellationToken ct)
    {
        if(!await db.WorkflowDefinitions.AsNoTracking().AnyAsync(x=>x.Id==workflowId&&x.Active,ct))throw new KeyNotFoundException("Active workflow was not found.");
        var row=await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==versionId&&x.WorkflowId==workflowId&&x.Status=="PUBLISHED",ct)??throw new KeyNotFoundException("Published workflow version was not found for this workflow.");
        var dates=ReadDates(row.Definition);
        if(dates.From is not null&&dates.From>from||dates.To is not null&&(to is null||to>dates.To))throw new InvalidOperationException("Request Type effective dates must be within the selected workflow version.");
    }

    private static (DateTimeOffset? From, DateTimeOffset? To) ReadDates(string? definition)
    {
        if (string.IsNullOrWhiteSpace(definition)) return (null, null);
        using var document = JsonDocument.Parse(definition);
        var root = document.RootElement;
        return (ReadDate(root, "effectiveFrom"), ReadDate(root, "effectiveTo"));
    }

    private static DateTimeOffset? ReadDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && value.TryGetDateTimeOffset(out var date) ? date : null;
}
