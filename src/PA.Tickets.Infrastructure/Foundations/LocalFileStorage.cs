using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    private string Root => Path.GetFullPath(configuration["Storage:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "storage"));
    private const string Provider = "Local";

    public async Task<StoredObject> PutAsync(string objectKey,string fileName,string? contentType,Stream content,long maxFileSizeBytes,CancellationToken ct)
    {
        var fullPath = Resolve(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        long length = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    length += read;
                    if (length > maxFileSizeBytes) throw new InvalidOperationException("Attachment exceeds the configured maximum size.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                await output.FlushAsync(ct);
            }
            File.Move(temporary, fullPath, true);
            return new(new StorageObjectMetadata(Provider, objectKey, fileName, contentType, length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
        }
        catch { TryDelete(temporary); TryDelete(fullPath); throw; }
    }

    public Task<Stream> OpenReadAsync(string objectKey,CancellationToken ct)
    {
        var path = Resolve(objectKey);
        if (!File.Exists(path)) throw new FileNotFoundException("Attachment content was not found.");
        Stream result = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(result);
    }

    public Task<bool> ExistsAsync(string objectKey,CancellationToken ct) => Task.FromResult(File.Exists(Resolve(objectKey)));

    public Task DeleteAsync(string objectKey,CancellationToken ct)
    {
        TryDelete(Resolve(objectKey));
        return Task.CompletedTask;
    }

    private string Resolve(string objectKey)
    {
        var decodedKey = Uri.UnescapeDataString(objectKey);
        if (string.IsNullOrWhiteSpace(objectKey) || decodedKey.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(decodedKey)) throw new ArgumentException("Invalid storage object key.");
        var root = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(Root, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Storage object key escapes the configured root.");
        return full;
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
