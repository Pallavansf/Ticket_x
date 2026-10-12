namespace PA.Tickets.Application.Foundations;

public sealed record StorageObjectMetadata(string Provider,string ObjectKey,string FileName,string? ContentType,long ContentLength,string Sha256Hash);
public sealed record StoredObject(StorageObjectMetadata Metadata);

public interface IFileStorage
{
    Task<StoredObject> PutAsync(string objectKey,string fileName,string? contentType,Stream content,long maxFileSizeBytes,CancellationToken ct);
    Task<Stream> OpenReadAsync(string objectKey,CancellationToken ct);
    Task<bool> ExistsAsync(string objectKey,CancellationToken ct);
    Task DeleteAsync(string objectKey,CancellationToken ct);
}

public interface IContentScanner
{
    Task<ContentScanResult> ScanAsync(Stream content,CancellationToken ct);
}

public enum ContentScanStatus { Clean, Rejected, Unavailable, Error, Timeout }
public sealed record ContentScanResult(ContentScanStatus Status,string? ScannerVersion=null,string? FailureCode=null);
public sealed class ContentRejectedException(string message):InvalidOperationException(message);
public sealed class ContentScannerUnavailableException():InvalidOperationException("The uploaded file cannot be accepted because content scanning is unavailable.");
public sealed class AttachmentTooLargeException():InvalidOperationException("Attachment exceeds the configured maximum size.");

public sealed class UnconfiguredContentScanner : IContentScanner
{
    public Task<ContentScanResult> ScanAsync(Stream content,CancellationToken ct) =>
        Task.FromResult(new ContentScanResult(ContentScanStatus.Unavailable,null,"SCANNER_NOT_CONFIGURED"));
}
