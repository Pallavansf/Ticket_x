using System.Text;
using System.Text.Json;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Infrastructure.Foundations;

internal static class FileContentTypeDetector
{
    public static string? Detect(Stream content)
    {
        Span<byte> header = stackalloc byte[16];
        var read = content.Read(header);
        content.Position = 0;
        var bytes = header[..read];

        if (bytes.StartsWith("%PDF-"u8)) return "application/pdf";
        if (bytes.StartsWith(new byte[]{0x89,0x50,0x4e,0x47,0x0d,0x0a,0x1a,0x0a})) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return "image/gif";
        if (bytes.StartsWith(new byte[]{0x50,0x4b,0x03,0x04})) return "application/zip";
        if (bytes.StartsWith("MZ"u8)) return "application/x-msdownload";
        if (bytes.StartsWith(new byte[]{0x7f,0x45,0x4c,0x46})) return "application/x-elf";
        if (read == 0) return null;

        try
        {
            content.Position = 0;
            using var reader = new StreamReader(content,new UTF8Encoding(false,true),false,1024,true);
            var text = reader.ReadToEnd();
            if (text.Any(ch=>char.IsControl(ch)&&ch is not ('\r' or '\n' or '\t'))) return null;
            try
            {
                using var _ = JsonDocument.Parse(text);
                return "application/json";
            }
            catch (JsonException)
            {
                return "text/plain";
            }
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        finally
        {
            content.Position = 0;
        }
    }
}

internal static class AttachmentContentPolicy
{
    private static readonly IReadOnlyDictionary<string,string> ExtensionTypes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"]="text/plain",
        [".json"]="application/json",
        [".pdf"]="application/pdf",
        [".png"]="image/png",
        [".jpg"]="image/jpeg",
        [".jpeg"]="image/jpeg",
        [".gif"]="image/gif"
    };

    public static bool IsAllowed(string fileName,string? declaredMimeType,string? detectedMimeType)
    {
        if (detectedMimeType is null || !ExtensionTypes.TryGetValue(Path.GetExtension(fileName),out var expected) || expected!=detectedMimeType)
            return false;
        var declared = declaredMimeType?.Split(';',2)[0].Trim();
        return string.IsNullOrWhiteSpace(declared)
            || declared.Equals("application/octet-stream",StringComparison.OrdinalIgnoreCase)
            || declared.Equals(detectedMimeType,StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DeterministicContentScanner : IContentScanner
{
    public async Task<ContentScanResult> ScanAsync(Stream content,CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer,ct);
        var text = Encoding.ASCII.GetString(buffer.GetBuffer(),0,(int)buffer.Length);
        if (text.Contains("EICAR-STANDARD-ANTIVIRUS-TEST-FILE",StringComparison.Ordinal))
            return new(ContentScanStatus.Rejected,"deterministic-test-only","TEST_SIGNATURE");
        return new(ContentScanStatus.Clean,"deterministic-test-only");
    }
}
