using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Reporting;

public static class AuditChainVerifier
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.Ordinal)
    {
        "TICKET",
        "STATUS_DEFINITION",
        "PRIORITY_DEFINITION"
    };

    public static bool SupportsFormat(string entityType) => SupportedFormats.Contains(entityType);

    public static (bool IsValid, int RecordsVerified, long? FailedAuditId, string? Failure) Verify(
        IReadOnlyCollection<AuditRow> records,
        string entityType,
        string entityId)
    {
        if (!SupportsFormat(entityType))
            return (false, 0, null, "UNSUPPORTED_FORMAT");

        if (records.Count == 0)
            return (false, 0, null, "NO_AUDIT_RECORDS");

        string? expectedPreviousHash = null;
        var verified = 0;
        foreach (var record in records.OrderBy(x => x.Id))
        {
            if (!string.Equals(record.EntityType, entityType, StringComparison.Ordinal)
                || !string.Equals(record.EntityId, entityId, StringComparison.Ordinal))
                return (false, verified, record.Id, "STREAM_MISMATCH");

            if (!string.Equals(record.PreviousHash, expectedPreviousHash, StringComparison.Ordinal))
                return (false, verified, record.Id, "PREDECESSOR_MISMATCH");

            var canonical = JsonSerializer.Serialize(new
            {
                entityType = record.EntityType,
                entityId = record.EntityId,
                action = record.Action,
                actorUserId = record.ActorId,
                auditAtUtc = record.At.UtcDateTime,
                oldValue = record.OldValue,
                newValue = record.NewValue,
                reason = record.Reason,
                previousHash = record.PreviousHash
            });
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            if (!TryDecodeHash(record.Hash, out var actualHash)
                || !CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                return (false, verified, record.Id, "HASH_MISMATCH");

            expectedPreviousHash = record.Hash;
            verified++;
        }

        return (true, verified, null, null);
    }

    private static bool TryDecodeHash(string? value, out byte[] hash)
    {
        hash = [];
        if (value is null || value.Length != 64 || value != value.ToLowerInvariant() || value.Any(c => !Uri.IsHexDigit(c)))
            return false;

        try
        {
            hash = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
