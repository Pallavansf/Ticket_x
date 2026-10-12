using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using PA.Tickets.Application.Reporting;

namespace PA.Tickets.Infrastructure.Reporting;

public sealed class ReportExportFormatter : IReportExportFormatter
{
    private static readonly string[] CsvFormulaPrefixes = ["=", "+", "-", "@"];

    public ReportExportArtifact Format(ReportDefinition definition, ReportResult result, ReportExportFormat format, int maxRows)
    {
        if (maxRows is < 1 or > 100_000) throw new ArgumentException("Export row limit is outside the permitted bounds.", nameof(maxRows));
        if (result.Rows.Count > maxRows) throw new InvalidOperationException("The export row limit was exceeded.");
        return format switch
        {
            ReportExportFormat.Csv => Create(definition.Code + ".csv", "text/csv; charset=utf-8", Csv(definition, result), result.Rows.Count),
            ReportExportFormat.Xlsx => Create(definition.Code + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Xlsx(definition, result), result.Rows.Count),
            ReportExportFormat.Pdf => throw new NotSupportedException("PDF export is not enabled because no supported PDF dependency is configured."),
            _ => throw new ArgumentException("Unsupported export format.", nameof(format))
        };
    }

    private static byte[] Csv(ReportDefinition definition, ReportResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        {
            writer.WriteLine(string.Join(',', definition.Columns.Select(Escape)));
            foreach (var row in result.Rows)
            {
                using var json = JsonDocument.Parse(JsonSerializer.Serialize(row));
                var values = definition.Columns.Select(column => TryGetProperty(json.RootElement, column, out var value) ? Cell(value) : "");
                writer.WriteLine(string.Join(',', values.Select(Escape)));
            }
        }
        return stream.ToArray();
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        if (root.TryGetProperty(name, out value)) return true;
        foreach (var property in root.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default;
        return false;
    }

    private static string Cell(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.String => Neutralize(value.GetString() ?? ""),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => Neutralize(value.GetRawText())
    };

    private static string Neutralize(string value) => CsvFormulaPrefixes.Any(value.StartsWith) ? "'" + value : value;
    private static string Escape(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n') ? '"' + value.Replace("\"", "\"\"") + '"' : value;
    private static byte[] Xlsx(ReportDefinition definition, ReportResult result)
    {
        var columns = definition.Columns.ToArray(); var sourceRows = result.Rows.ToArray();
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"" + Xml(definition.Name) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            var rows = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            rows.Append("<row r=\"1\">"); for (var i = 0; i < columns.Length; i++) rows.Append("<c r=\"").Append(Column(i)).Append("1\" t=\"inlineStr\"><is><t>").Append(Xml(columns[i])).Append("</t></is></c>"); rows.Append("</row>");
            for (var r = 0; r < sourceRows.Length; r++) { using var json = JsonDocument.Parse(JsonSerializer.Serialize(sourceRows[r])); rows.Append("<row r=\"").Append(r + 2).Append("\">"); for (var c = 0; c < columns.Length; c++) { var value = TryGetProperty(json.RootElement, columns[c], out var v) ? Cell(v) : ""; rows.Append("<c r=\"").Append(Column(c)).Append(r + 2).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(Xml(value)).Append("</t></is></c>"); } rows.Append("</row>"); }
            rows.Append("</sheetData></worksheet>"); Add(zip, "xl/worksheets/sheet1.xml", rows.ToString());
        }
        return output.ToArray();
    }
    private static void Add(ZipArchive zip, string name, string content) { using var entry = zip.CreateEntry(name, CompressionLevel.Fastest).Open(); using var writer = new StreamWriter(entry, new UTF8Encoding(false)); writer.Write(content); }
    private static string Column(int index) { var result = ""; for (var n = index + 1; n > 0; n = (n - 1) / 26) result = (char)('A' + (n - 1) % 26) + result; return result; }
    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? "";    private static ReportExportArtifact Create(string fileName, string contentType, byte[] content, int rowCount) => new(fileName, contentType, content, rowCount, Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
}