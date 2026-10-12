namespace PA.Tickets.Application.Reporting;

public enum ReportExportFormat { Csv, Xlsx, Pdf }
public sealed record ReportExportRequest(ReportExportFormat Format, ReportQueryRequest Filters, int MaxRows = 10000);
public sealed record ReportExportArtifact(string FileName, string ContentType, byte[] Content, int RowCount, string Sha256);
public interface IReportExportFormatter
{
    ReportExportArtifact Format(ReportDefinition definition, ReportResult result, ReportExportFormat format, int maxRows);
}