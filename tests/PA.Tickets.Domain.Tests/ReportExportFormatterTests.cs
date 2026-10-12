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
    public void Export_rejects_rows_above_configured_bound()
    {
        var definition = new ReportDefinition("test", "Test", "", [], ["value"], "TICKET_READ");
        var result = new ReportResult("test", "Test", DateTimeOffset.UtcNow, new(), 1, 25, 2, [new { Value = 1 }, new { Value = 2 }]);
        Assert.Throws<InvalidOperationException>(() => new ReportExportFormatter().Format(definition, result, ReportExportFormat.Csv, 1));
    }
}