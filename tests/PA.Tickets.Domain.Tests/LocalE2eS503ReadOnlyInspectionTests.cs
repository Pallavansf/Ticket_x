using System.Security.Cryptography;

namespace PA.Tickets.Domain.Tests;

public sealed class LocalE2eS503ReadOnlyInspectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
    private const string TestSid = "S-1-5-21-100-200-300-1001";

    [Fact]
    public void Valid_authorization_is_bound_to_one_target_artifact_and_window()
    {
        LocalE2eS503ReadOnlyInspection.ValidateAuthorization(Authorization(), Now, TestSid);
    }

    [Fact]
    public async Task Caller_created_authorization_and_privilege_objects_never_enable_execution()
    {
        var exception = await Assert.ThrowsAsync<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ExecuteAsync(
                "unused-test-root", Authorization(), PrivilegeEvidence(), auditSink: null));

        Assert.Equal("EVIDENCE_PROVENANCE_NOT_CONFIGURED", exception.ReasonCode);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("artifact")]
    [InlineData("policy")]
    [InlineData("operation")]
    [InlineData("operator")]
    public void Authorization_scope_mismatches_fail_closed(string change)
    {
        var authorization = Authorization() with
        {
            TargetHost = change == "target" ? "127.0.0.1" : "::1",
            ArtifactSha256 = change == "artifact" ? new string('A', 64) : LocalE2eS503ReadOnlyInspection.ArtifactSha256,
            PolicySha256 = change == "policy" ? new string('B', 64) : LocalE2eS503ReadOnlyInspection.PolicySha256,
            Operation = change == "operation" ? "SCHEMA_WRITE" : LocalE2eS503ReadOnlyInspection.Operation,
            OperatorWindowsSid = change == "operator" ? "S-1-5-21-else" : TestSid
        };

        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidateAuthorization(authorization, Now, TestSid));
    }

    [Fact]
    public void Missing_and_expired_authorization_fail_closed()
    {
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidateAuthorization(null, Now, TestSid));
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidateAuthorization(
                Authorization() with { ExpiresAtUtc = Now }, Now, TestSid));
    }

    [Theory]
    [InlineData("REVOKED")]
    [InlineData("stale")]
    public void Missing_revocation_or_stale_revocation_fails_closed(string condition)
    {
        var authorization = Authorization() with
        {
            RevocationStatus = condition == "REVOKED" ? "REVOKED" : "ACTIVE",
            RevocationCheckedAtUtc = condition == "stale" ? Now.AddMinutes(-2) : Now
        };

        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidateAuthorization(authorization, Now, TestSid));
    }

    [Fact]
    public void Fixed_local_policy_accepts_owner_role_write_capabilities_but_requires_fresh_bound_evidence()
    {
        LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence(
            "S5-03_READ_ONLY_FINGERPRINT",
            PrivilegeEvidence() with
            {
                CanWriteApplicationTables = true,
                CanWriteSchema = true,
                CanWriteSequences = true
            }, Now);
        LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence(
            "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT",
            PrivilegeEvidence() with { CanWriteApplicationTables = true, CanWriteSchema = true, CanWriteSequences = true }, Now);
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence("S5-03_READ_ONLY_FINGERPRINT", null, Now));
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence(
                "S5-03_READ_ONLY_FINGERPRINT",
                PrivilegeEvidence() with { Role = "other_role" }, Now));
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence(
                "S5-03_READ_ONLY_FINGERPRINT",
                PrivilegeEvidence() with { TargetHost = "127.0.0.1" }, Now));
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence(
                "S5-03_READ_ONLY_FINGERPRINT",
                PrivilegeEvidence() with { CapturedAtUtc = Now.AddMinutes(-6) }, Now));
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence(
                "S5-03_READ_ONLY_FINGERPRINT",
                PrivilegeEvidence() with { IsSuperuser = true }, Now));
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence(
                "S5-03_READ_ONLY_FINGERPRINT",
                PrivilegeEvidence() with { EffectiveRoleMemberships = ["writer_role"] }, Now));
        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidatePrivilegeEvidence("SCHEMA_WRITE", PrivilegeEvidence(), Now));
    }

    [Theory]
    [InlineData("database")]
    [InlineData("role")]
    [InlineData("address")]
    [InlineData("port")]
    [InlineData("read-write")]
    public void Same_session_identity_must_match_exact_target(string mismatch)
    {
        var identity = new LocalE2eS503SessionIdentity(
            mismatch == "database" ? "other" : "pa_tickets_codex_e2e",
            mismatch == "role" ? "other" : "pa_tickets_codex",
            mismatch == "address" ? "127.0.0.1" : "::1",
            mismatch == "port" ? 5433 : 5432,
            mismatch == "read-write" ? "off" : "on");

        Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eS503ReadOnlyInspection.ValidateSessionIdentity(identity));
    }

    [Fact]
    public void Altered_artifact_including_a_write_statement_is_rejected_offline()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pa-tickets-s503-" + Guid.NewGuid().ToString("N"));
        var artifactPath = Path.Combine(temp, LocalE2eS503ReadOnlyInspection.ArtifactRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        try
        {
            File.WriteAllText(artifactPath, "BEGIN;\nSET TRANSACTION READ ONLY;\nDROP TABLE public.example;\nROLLBACK;\n");
            Assert.Throws<LocalE2eS503InspectionException>(() =>
                LocalE2eS503ReadOnlyInspection.LoadPinnedArtifact(temp));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void Missing_evidence_custody_directory_is_rejected()
    {
        var missing = Path.Combine(Path.GetTempPath(), "pa-tickets-s503-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(() => new FileLocalE2eS503AuditSink(missing));
    }

    [Fact]
    public void Checked_in_fingerprint_artifact_matches_the_approved_pin_offline()
    {
        var root = Path.Combine(Path.GetTempPath(), "pa-tickets-s503-pinned-" + Guid.NewGuid().ToString("N"));
        var artifactPath = Path.Combine(root, LocalE2eS503ReadOnlyInspection.ArtifactRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        var fixture = Path.Combine(AppContext.BaseDirectory, "PinnedS503Artifact", LocalE2eS503ReadOnlyInspection.ArtifactRelativePath);
        File.Copy(fixture, artifactPath);
        try
        {
            var artifact = LocalE2eS503ReadOnlyInspection.LoadPinnedArtifact(root);
        Assert.Equal(LocalE2eS503ReadOnlyInspection.ArtifactSha256, artifact.Sha256);
        Assert.Equal(LocalE2eS503ReadOnlyInspection.ArtifactSha256,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, LocalE2eS503ReadOnlyInspection.ArtifactRelativePath)))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static LocalE2eS503Authorization Authorization() => new(
        LocalE2eS503ReadOnlyInspection.Operation, "::1", 5432, "pa_tickets_codex_e2e", "pa_tickets_codex",
        LocalE2eS503ReadOnlyInspection.ArtifactRelativePath, LocalE2eS503ReadOnlyInspection.ArtifactSha256,
        LocalE2eS503ReadOnlyInspection.PolicySha256, "TEST-ONLY OWNER", TestSid, "TEST-ONLY-APPROVAL",
        Now.AddMinutes(-1), Now.AddMinutes(20), Now, "ACTIVE", "TEST-ONLY-REVOCATION");

    private static LocalE2eS503PrivilegeEvidence PrivilegeEvidence() => new(
        "::1", 5432, "pa_tickets_codex_e2e", "pa_tickets_codex", "TEST-ONLY-PRIVILEGE-EVIDENCE", Now,
        true, false, false, false, false, false, false, false, false, Array.Empty<string>());

}
