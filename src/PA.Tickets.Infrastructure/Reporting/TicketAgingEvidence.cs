using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;

namespace PA.Tickets.Infrastructure.Reporting;

internal interface ITicketAgingEvidenceWriter
{
    Task CompleteAsync(long exportId, IReadOnlyCollection<long> contributorTicketIds, string artifactHash, int rowCount, CancellationToken ct);
}

internal static class TicketAgingEvidence
{

    public static IReadOnlyList<TicketAgingContributor> Accumulate(
        IReadOnlyList<TicketAgingContributor> existing,
        TicketAgingQueryResult page)
    {
        EnsureComplete(page);

        var combined = existing.Concat(page.Contributors).ToArray();
        if (combined.Select(x => x.TicketId).Distinct().Count() != combined.Length)
            throw new InvalidOperationException("Ticket-aging contributor evidence is incomplete or duplicated.");

        return combined;
    }

    public static void EnsureComplete(TicketAgingQueryResult page)
    {
        if (page.Result.Rows.Count != page.Contributors.Count)
            throw new InvalidOperationException("Ticket-aging contributor evidence does not match the display rows.");

        for (var i = 0; i < page.Contributors.Count; i++)
        {
            if (page.Result.Rows.ElementAt(i) is not TicketAgingRow row)
                throw new InvalidOperationException("Ticket-aging display row type is invalid.");

            var contributor = page.Contributors[i];
            if (contributor.TicketId <= 0 || !string.Equals(contributor.TicketNumber, row.TicketNumber, StringComparison.Ordinal))
                throw new InvalidOperationException("Ticket-aging contributor evidence does not match its display row.");
        }
    }
}


internal static class TicketAgingCompletion
{
    public static async Task<StoredObject> StoreAndCompleteAsync(
        IFileStorage storage,
        ITicketAgingEvidenceWriter? evidenceWriter,
        long exportId,
        string objectKey,
        ReportExportArtifact artifact,
        long maxFileSizeBytes,
        IReadOnlyCollection<long> contributorTicketIds,
        CancellationToken ct)
    {
        if (evidenceWriter is null)
            throw new InvalidOperationException("Ticket-aging evidence completion is unavailable; the export remains unavailable.");

        var stored = await storage.PutAsync(
            objectKey,
            artifact.FileName,
            artifact.ContentType,
            new MemoryStream(artifact.Content, writable: false),
            maxFileSizeBytes,
            ct);

        await evidenceWriter.CompleteAsync(
            exportId,
            contributorTicketIds,
            stored.Metadata.Sha256Hash,
            artifact.RowCount,
            ct);

        return stored;
    }
}
