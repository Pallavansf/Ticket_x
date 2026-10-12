using Microsoft.EntityFrameworkCore;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Execution;
using System.Text.Json;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;

namespace PA.Tickets.Infrastructure.Reporting;

public sealed record ExportStatus(long Id, string ReportCode, string Format, string Status, int? RowCount, long? FileSize, string? ContentType, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt);
public sealed record ExportDownload(ExportStatus Status, Stream Content, string FileName);
public interface IReportExportService
{
    Task<ExportStatus> CreateAsync(string reportCode, ReportExportRequest request, long actor, string? correlationId, CancellationToken ct);
    Task<ExportStatus> GetAsync(long exportId, long actor, CancellationToken ct);
    Task<ExportDownload> DownloadAsync(long exportId, long actor, CancellationToken ct);
}

public sealed class ReportExportService(ExecutionDbContext db, IReportingQueryService reports, IReportExportFormatter formatter, IFileStorage storage, TimeProvider clock) : IReportExportService
{
    private const int PageSize = 100;
    private const int MaxRows = 10_000;
    private const long MaxFileBytes = 25 * 1024 * 1024;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<ExportStatus> CreateAsync(string reportCode, ReportExportRequest request, long actor, string? correlationId, CancellationToken ct)
    {
        if (request.MaxRows is < 1 or > MaxRows) throw new ArgumentException($"Export row limit must be between 1 and {MaxRows}.", nameof(request));
        var definition = reports.GetDefinition(reportCode);
        var now = clock.GetUtcNow();
        var row = new ReportExportRow { ReportCode = definition.Code, Format = request.Format.ToString().ToUpperInvariant(), RequestedBy = actor, FiltersJson = JsonSerializer.Serialize(request.Filters), Status = "RUNNING", CreatedAt = now, StartedAt = now, CorrelationId = correlationId, ExpiresAt = now.Add(Retention) };
        db.ReportExports.Add(row);
        await db.SaveChangesAsync(ct);
        try
        {
            var allRows = new List<object>(Math.Min(request.MaxRows, PageSize));
            var page = 1;
            var total = 0;
            while (allRows.Count < request.MaxRows)
            {
                ct.ThrowIfCancellationRequested();
                var result = await reports.QueryAsync(definition.Code, request.Filters with { Page = page, PageSize = PageSize }, actor, correlationId, ct);
                total = result.TotalCount;
                if (total > request.MaxRows) throw new InvalidOperationException("The export row limit was exceeded.");
                if (result.Rows.Count == 0) break;
                allRows.AddRange(result.Rows);
                if (allRows.Count >= total) break;
                page++;
            }
            if (allRows.Count != total) throw new InvalidOperationException("The report changed while the export was being generated; no complete file was published.");
            var resultForFormat = new ReportResult(definition.Code, definition.Name, now, request.Filters, 1, allRows.Count, total, allRows);
            var artifact = formatter.Format(definition, resultForFormat, request.Format, request.MaxRows);
            if (artifact.Content.LongLength > MaxFileBytes) throw new InvalidOperationException("The export file-size limit was exceeded.");
            var key = $"exports/{row.Id}/{Guid.NewGuid():N}.{request.Format.ToString().ToLowerInvariant()}";
            var stored = await storage.PutAsync(key, artifact.FileName, artifact.ContentType, new MemoryStream(artifact.Content, writable: false), MaxFileBytes, ct);
            row.Status = "COMPLETED"; row.CompletedAt = clock.GetUtcNow(); row.RowCount = artifact.RowCount; row.FileSize = stored.Metadata.ContentLength; row.ContentType = stored.Metadata.ContentType; row.FileHash = stored.Metadata.Sha256Hash; row.StorageKey = stored.Metadata.ObjectKey;
            await db.SaveChangesAsync(ct);
            return ToStatus(row);
        }
        catch (Exception ex)
        {
            row.Status = "FAILED"; row.CompletedAt = clock.GetUtcNow(); row.FailureClassification = ex is OperationCanceledException ? "CANCELLED" : "PERMANENT"; row.FailureMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ExportStatus> GetAsync(long exportId, long actor, CancellationToken ct)
    {
        var row = await Authorized(exportId, actor, ct);
        await ExpireIfNeeded(row, ct);
        return ToStatus(row);
    }

    public async Task<ExportDownload> DownloadAsync(long exportId, long actor, CancellationToken ct)
    {
        var row = await Authorized(exportId, actor, ct);
        await ExpireIfNeeded(row, ct);
        if (row.Status != "COMPLETED" || string.IsNullOrWhiteSpace(row.StorageKey)) throw new KeyNotFoundException("The export is not available for download.");
        return new(ToStatus(row), await storage.OpenReadAsync(row.StorageKey, ct), $"{row.ReportCode}-{row.Id}.{row.Format.ToLowerInvariant()}");
    }

    private async Task ExpireIfNeeded(ReportExportRow row, CancellationToken ct)
    {
        if (row.Status == "COMPLETED" && row.ExpiresAt <= clock.GetUtcNow())
        {
            row.Status = "EXPIRED";
            await db.SaveChangesAsync(ct);
            if (!string.IsNullOrWhiteSpace(row.StorageKey)) await storage.DeleteAsync(row.StorageKey, CancellationToken.None);
        }
    }

    private async Task<ReportExportRow> Authorized(long id, long actor, CancellationToken ct) => await db.ReportExports.SingleOrDefaultAsync(x => x.Id == id && x.RequestedBy == actor, ct) ?? throw new KeyNotFoundException("The export was not found.");
    private static ExportStatus ToStatus(ReportExportRow row) => new(row.Id, row.ReportCode, row.Format, row.Status, row.RowCount, row.FileSize, row.ContentType, row.CreatedAt, row.CompletedAt, row.ExpiresAt);
}