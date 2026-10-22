using System.Security.Cryptography;

namespace PA.Tickets.Domain.Tests;

internal sealed record PostgreSqlProviderValidationAuthorization(
    string ApprovedTargetDescriptor,
    string BaselineArtifactPath,
    string BaselineSha256,
    string AuthorizationReference,
    string EvidenceRetentionBoundary,
    string Operation,
    string ApprovalStatus,
    DateTimeOffset ExpiresAtUtc,
    string TargetIdentityEvidenceReference,
    string LeastPrivilegeDecisionReference);

internal sealed record PostgreSqlProviderPreflightResult(
    string BaselineArtifactPath,
    string VerifiedSha256,
    string ApprovedTargetDescriptor,
    string Operation);

internal static class PostgreSqlProviderPreflight
{
    private const string ReadOnlyCatalogOperation = "READ_ONLY_CATALOG_RECONCILIATION";

    internal static PostgreSqlProviderPreflightResult Verify(
        string repositoryRoot,
        PostgreSqlProviderValidationAuthorization? authorization)
    {
        if (authorization is null || string.IsNullOrWhiteSpace(authorization.AuthorizationReference) ||
            string.IsNullOrWhiteSpace(authorization.EvidenceRetentionBoundary) ||
            string.IsNullOrWhiteSpace(authorization.TargetIdentityEvidenceReference) ||
            string.IsNullOrWhiteSpace(authorization.LeastPrivilegeDecisionReference) ||
            !string.Equals(authorization.ApprovalStatus, "OWNER_APPROVED", StringComparison.Ordinal) ||
            authorization.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            !string.Equals(authorization.Operation, ReadOnlyCatalogOperation, StringComparison.Ordinal))
            throw new InvalidOperationException("Provider validation authorization is incomplete; no provider operation is permitted.");

        var descriptor = ParseDescriptor(authorization.ApprovedTargetDescriptor);
        if (descriptor.Database.Equals("pa_tickets_test", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Database.Equals("pa_tickets_h_verify", StringComparison.OrdinalIgnoreCase) ||
            descriptor.User.Equals("pa_tickets_test_user", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider validation target is prohibited; no provider operation is permitted.");
        if (descriptor.Host.Contains(',', StringComparison.Ordinal))
            throw new InvalidOperationException("Provider validation target is multi-host and cannot be independently verified.");

        if (string.IsNullOrWhiteSpace(authorization.BaselineArtifactPath) || !RegexHex(authorization.BaselineSha256))
            throw new InvalidOperationException("An explicit baseline artifact and 64-character SHA-256 are required; no baseline is inferred.");
        var root = Path.GetFullPath(repositoryRoot);
        var artifact = Path.GetFullPath(Path.Combine(root, authorization.BaselineArtifactPath));
        var relative = Path.GetRelativePath(root, artifact);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) || !File.Exists(artifact))
            throw new InvalidOperationException("The selected baseline artifact must be an existing repository-local file.");

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(artifact)));
        if (!hash.Equals(authorization.BaselineSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected baseline artifact hash does not match the authorized SHA-256.");
        return new(relative.Replace(Path.DirectorySeparatorChar, '/'), hash, authorization.ApprovedTargetDescriptor.Trim(), authorization.Operation);
    }

    private static bool RegexHex(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static (string Host, string Database, string User, int Port) ParseDescriptor(string descriptor)
    {
        var fields = descriptor.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host) ||
            !fields.TryGetValue("database", out var database) || string.IsNullOrWhiteSpace(database) ||
            !fields.TryGetValue("user", out var user) || string.IsNullOrWhiteSpace(user) ||
            !fields.TryGetValue("port", out var portText) || !int.TryParse(portText, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException("The approved target descriptor is incomplete; no provider operation is permitted.");
        return (host, database, user, port);
    }
}
