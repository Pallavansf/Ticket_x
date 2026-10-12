using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Domain.Tests;

public sealed class AuditChainVerifierTests
{
    [Fact]
    public void Verify_accepts_a_valid_linked_ticket_chain()
    {
        var first = CreateAudit(1, "CREATED", null, "created");
        var second = CreateAudit(2, "UPDATED", first.Hash, "updated");

        var result = AuditChainVerifier.Verify([second, first], "TICKET", "42");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.RecordsVerified);
        Assert.Null(result.FailedAuditId);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Verify_detects_payload_tampering()
    {
        var audit = CreateAudit(1, "CREATED", null, "created");
        audit.Reason = "tampered";

        var result = AuditChainVerifier.Verify([audit], "TICKET", "42");

        Assert.False(result.IsValid);
        Assert.Equal(audit.Id, result.FailedAuditId);
        Assert.Equal("HASH_MISMATCH", result.Failure);
    }

    [Fact]
    public void Verify_detects_predecessor_tampering()
    {
        var first = CreateAudit(1, "CREATED", null, "created");
        var second = CreateAudit(2, "UPDATED", "0".PadLeft(64, '0'), "updated");

        var result = AuditChainVerifier.Verify([first, second], "TICKET", "42");

        Assert.False(result.IsValid);
        Assert.Equal(second.Id, result.FailedAuditId);
        Assert.Equal("PREDECESSOR_MISMATCH", result.Failure);
    }

    [Fact]
    public void Verify_detects_hash_tampering()
    {
        var audit = CreateAudit(1, "CREATED", null, "created");
        audit.Hash = new string('0', 64);

        var result = AuditChainVerifier.Verify([audit], "TICKET", "42");

        Assert.False(result.IsValid);
        Assert.Equal(audit.Id, result.FailedAuditId);
        Assert.Equal("HASH_MISMATCH", result.Failure);
    }

    [Fact]
    public void Verify_rejects_an_empty_stream()
    {
        var result = AuditChainVerifier.Verify([], "TICKET", "42");

        Assert.False(result.IsValid);
        Assert.Equal(0, result.RecordsVerified);
        Assert.Equal("NO_AUDIT_RECORDS", result.Failure);
    }

    [Theory]
    [InlineData("STATUS_DEFINITION")]
    [InlineData("PRIORITY_DEFINITION")]
    public void Verify_accepts_operational_metadata_streams_using_the_persisted_canonical_shape(string entityType)
    {
        var record = CreateAudit(1, entityType, "config-42", "UPDATED", null, "configuration changed");

        var result = AuditChainVerifier.Verify([record], entityType, "config-42");

        Assert.True(result.IsValid);
        Assert.Equal(1, result.RecordsVerified);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Verify_reports_unsupported_non_ticket_format_as_a_controlled_outcome()
    {
        var record = CreateAudit(1, "INCIDENT", "42", "INCIDENT_CREATED", null, "created");

        var result = AuditChainVerifier.Verify([record], "INCIDENT", "42");

        Assert.False(result.IsValid);
        Assert.Equal("UNSUPPORTED_FORMAT", result.Failure);
    }

    private static AuditRow CreateAudit(long id, string action, string? previousHash, string reason) =>
        CreateAudit(id, "TICKET", "42", action, previousHash, reason);

    private static AuditRow CreateAudit(long id, string entityType, string entityId, string action, string? previousHash, string? reason)
    {
        var audit = new AuditRow
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorId = 7,
            At = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Reason = reason,
            PreviousHash = previousHash
        };
        var canonical = JsonSerializer.Serialize(new
        {
            entityType = audit.EntityType,
            entityId = audit.EntityId,
            action = audit.Action,
            actorUserId = audit.ActorId,
            auditAtUtc = audit.At.UtcDateTime,
            oldValue = audit.OldValue,
            newValue = audit.NewValue,
            reason = audit.Reason,
            previousHash = audit.PreviousHash
        });
        audit.Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return audit;
    }
}
