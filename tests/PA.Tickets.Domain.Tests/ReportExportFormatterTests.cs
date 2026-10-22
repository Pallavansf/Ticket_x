using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Domain.Tests;

public sealed class ReportExportFormatterTests
{
    [Fact]
    public void Csv_export_uses_controlled_columns_and_neutralizes_formula_cells()
    {
        var definition = new ReportDefinition("test", "Test", "", [], ["name", "count"], "TICKET_READ");
        var result = new ReportResult("test", "Test", DateTimeOffset.UtcNow, new(), 1, 25, 1, [new { Name = "=SUM(A1:A2)", Count = 3 }]);
        var artifact = new ReportExportFormatter().Format(definition, result, ReportExportFormat.Csv, 10);
        var csv = System.Text.Encoding.UTF8.GetString(artifact.Content);
        Assert.Contains("name,count", csv, StringComparison.Ordinal);
        Assert.Contains("'=SUM(A1:A2),3", csv, StringComparison.Ordinal);
        Assert.Equal(1, artifact.RowCount);
        Assert.Equal(64, artifact.Sha256.Length);
    }

    [Fact]
    public void Csv_export_escapes_quotes_delimiters_and_line_breaks()
    {
        var definition = new ReportDefinition("test", "Test", "", [], ["value"], "TICKET_READ");
        var result = new ReportResult("test", "Test", DateTimeOffset.UtcNow, new(), 1, 25, 1, [new { Value = "a,b\"c\nd" }]);
        var csv = System.Text.Encoding.UTF8.GetString(new ReportExportFormatter().Format(definition, result, ReportExportFormat.Csv, 10).Content);
        Assert.Contains("\"a,b\"\"c\nd\"", csv, StringComparison.Ordinal);
    }


    [Fact]
    public void Ticket_aging_evidence_matches_display_rows_without_changing_formatter_input()
    {
        var definition = new ReportDefinition("ticket-aging", "Ticket Aging", "", [], ["ticketNumber", "subject"], "TICKET_READ");
        var rows = new object[]
        {
            new TicketAgingRow("INC-001", "First", "Incident", "High", "Open", DateTimeOffset.UtcNow.AddDays(-3), 3),
            new TicketAgingRow("INC-002", "Second", "Incident", "Low", "Open", DateTimeOffset.UtcNow.AddDays(-1), 1)
        };
        var result = new ReportResult("ticket-aging", "Ticket Aging", DateTimeOffset.UtcNow, new(), 1, 2, 2, rows);
        var page = new TicketAgingQueryResult(result,
        [
            new TicketAgingContributor(101, "INC-001"),
            new TicketAgingContributor(102, "INC-002")
        ]);

        TicketAgingEvidence.EnsureComplete(page);

        var formatter = new ReportExportFormatter();
        var before = formatter.Format(definition, result, ReportExportFormat.Csv, 10);
        var after = formatter.Format(definition, page.Result, ReportExportFormat.Csv, 10);

        Assert.Equal(before.Content, after.Content);
        Assert.Equal(before.Sha256, after.Sha256);
    }

    [Fact]
    public void Ticket_aging_evidence_rejects_misaligned_contributors()
    {
        var result = new ReportResult("ticket-aging", "Ticket Aging", DateTimeOffset.UtcNow, new(), 1, 1, 1,
        [new TicketAgingRow("INC-001", "First", "Incident", "High", "Open", DateTimeOffset.UtcNow, 0)]);
        var page = new TicketAgingQueryResult(result, [new TicketAgingContributor(101, "INC-999")]);

        Assert.Throws<InvalidOperationException>(() => TicketAgingEvidence.EnsureComplete(page));
    }

    [Fact]
    public void Ticket_aging_evidence_accumulates_pages_in_order_and_rejects_duplicates_or_incomplete_pages()
    {
        var first = Page((101, "INC-001"));
        var second = Page((102, "INC-002"));

        var contributors = TicketAgingEvidence.Accumulate([], first);
        contributors = TicketAgingEvidence.Accumulate(contributors, second);

        Assert.Equal([(101, "INC-001"), (102, "INC-002")], contributors.Select(x => (x.TicketId, x.TicketNumber)));
        Assert.Throws<InvalidOperationException>(() => TicketAgingEvidence.Accumulate(contributors, Page((101, "INC-001"))));
        Assert.Throws<InvalidOperationException>(() => TicketAgingEvidence.Accumulate(contributors, Page((103, "INC-003"), includeContributor: false)));

        static TicketAgingQueryResult Page((long Id, string Number) ticket, bool includeContributor = true)
        {
            var result = new ReportResult("ticket-aging", "Ticket Aging", DateTimeOffset.UtcNow, new(), 1, 1, 2,
                [new TicketAgingRow(ticket.Number, "Subject", "Incident", "High", "Open", DateTimeOffset.UtcNow, 1)]);
            return new(result, includeContributor ? [new TicketAgingContributor(ticket.Id, ticket.Number)] : []);
        }
    }
    [Fact]
    public async Task Ticket_aging_completion_without_writer_does_not_store_artifact()
    {
        var events = new List<string>();
        var artifact = new ReportExportArtifact("ticket-aging.csv", "text/csv", [1], 1, "artifact-hash");
        var storage = new RecordingStorage(events);

        await Assert.ThrowsAsync<InvalidOperationException>(() => TicketAgingCompletion.StoreAndCompleteAsync(
            storage,
            null,
            77,
            "exports/77/file.csv",
            artifact,
            1024,
            [101],
            CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task Ticket_aging_completion_stores_artifact_before_requesting_evidence_completion()
    {
        var events = new List<string>();
        var artifact = new ReportExportArtifact("ticket-aging.csv", "text/csv", [1, 2, 3], 2, "artifact-hash");
        var storage = new RecordingStorage(events);
        var writer = new RecordingEvidenceWriter(events);

        var stored = await TicketAgingCompletion.StoreAndCompleteAsync(
            storage,
            writer,
            77,
            "exports/77/file.csv",
            artifact,
            1024,
            [101, 102],
            CancellationToken.None);

        Assert.Equal(["storage", "evidence"], events);
        Assert.Equal("stored-hash", stored.Metadata.Sha256Hash);
        Assert.Equal((77L, new long[] { 101, 102 }, "stored-hash", 2), writer.Request);
    }

    [Fact]
    public async Task Ticket_aging_completion_failure_cannot_produce_a_successful_completion()
    {
        var events = new List<string>();
        var artifact = new ReportExportArtifact("ticket-aging.csv", "text/csv", [1], 1, "artifact-hash");
        var storage = new RecordingStorage(events);
        var writer = new RecordingEvidenceWriter(events) { Failure = new InvalidOperationException("evidence failed") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => TicketAgingCompletion.StoreAndCompleteAsync(
            storage,
            writer,
            77,
            "exports/77/file.csv",
            artifact,
            1024,
            [101],
            CancellationToken.None));

        Assert.Equal(["storage", "evidence"], events);
    }

    [Fact]
    public async Task Ticket_aging_completion_does_not_request_evidence_when_storage_fails()
    {
        var events = new List<string>();
        var artifact = new ReportExportArtifact("ticket-aging.csv", "text/csv", [1], 1, "artifact-hash");
        var storage = new RecordingStorage(events) { Failure = new IOException("storage failed") };
        var writer = new RecordingEvidenceWriter(events);

        await Assert.ThrowsAsync<IOException>(() => TicketAgingCompletion.StoreAndCompleteAsync(
            storage,
            writer,
            77,
            "exports/77/file.csv",
            artifact,
            1024,
            [101],
            CancellationToken.None));

        Assert.Equal(["storage"], events);
        Assert.Null(writer.Request);
    }

    private sealed class RecordingStorage(List<string> events) : IFileStorage
    {
        public Exception? Failure { get; init; }

        public Task<StoredObject> PutAsync(string objectKey, string fileName, string? contentType, Stream content, long maxFileSizeBytes, CancellationToken ct)
        {
            events.Add("storage");
            if (Failure is not null) return Task.FromException<StoredObject>(Failure);
            return Task.FromResult(new StoredObject(new StorageObjectMetadata("test", objectKey, fileName, contentType, content.Length, "stored-hash")));
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string objectKey, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string objectKey, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RecordingEvidenceWriter(List<string> events) : ITicketAgingEvidenceWriter
    {
        public Exception? Failure { get; init; }
        public (long ExportId, long[] ContributorIds, string Hash, int RowCount)? Request { get; private set; }

        public Task CompleteAsync(long exportId, IReadOnlyCollection<long> contributorTicketIds, string artifactHash, int rowCount, CancellationToken ct)
        {
            events.Add("evidence");
            Request = (exportId, contributorTicketIds.ToArray(), artifactHash, rowCount);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    [Fact]
    public void Export_rejects_rows_above_configured_bound()
    {
        var definition = new ReportDefinition("test", "Test", "", [], ["value"], "TICKET_READ");
        var result = new ReportResult("test", "Test", DateTimeOffset.UtcNow, new(), 1, 25, 2, [new { Value = 1 }, new { Value = 2 }]);
        Assert.Throws<InvalidOperationException>(() => new ReportExportFormatter().Format(definition, result, ReportExportFormat.Csv, 1));
    }
}
