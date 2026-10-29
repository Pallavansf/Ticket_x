using System.Security.Cryptography;

namespace PA.Tickets.Domain.Tests;

public sealed class LocalE2eReadOnlyCatalogInspectionTests
{
    private static string RepositoryRoot => FindRepositoryRoot();

    [Fact]
    public void Artifact_map_contains_the_five_pinned_local_operations()
    {
        var artifacts = LocalE2eReadOnlyCatalogInspection.AllowedArtifacts.OrderBy(x => x.Operation).ToArray();
        Assert.Equal(5, artifacts.Length);
        Assert.Equal("LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT", artifacts[0].Operation);
        Assert.Equal("0BFC0FFC556103A75CD3F6A722A724EBF41F39F1F3523ECE986DFFA36C4AB034", artifacts[0].Sha256);
        Assert.Equal("M1_LIFECYCLE_REFERENCE_PREFLIGHT", artifacts[1].Operation);
        Assert.Equal("Docs/reconciliation/proposed/postgresql/M1_lifecycle_reference_and_sequence_preflight_REVIEW.sql", artifacts[1].RelativePath);
        Assert.Equal("C719C6C0204527F262FAB51A3C4138FC09057744990DF242F232BA9FC88AE47D", artifacts[1].Sha256);
        Assert.Equal("M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT", artifacts[2].Operation);
        Assert.Equal("Docs/reconciliation/proposed/postgresql/M1_permission_catalogue_sequence_preflight_REVIEW.sql", artifacts[2].RelativePath);
        Assert.Equal("795838E1CE3A02ADFBBE755197DB2228D52ED16C457046F57818E3912DC88609", artifacts[2].Sha256);
        Assert.Equal("M1_SEQUENCE_SAFETY_PREFLIGHT", artifacts[3].Operation);
        Assert.Equal("Docs/reconciliation/proposed/postgresql/M1_sequence_safety_inspection_REVIEW.sql", artifacts[3].RelativePath);
        Assert.Equal("526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A", artifacts[3].Sha256);
        Assert.Equal("S5-03_READ_ONLY_FINGERPRINT", artifacts[4].Operation);
        Assert.Equal("6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342", artifacts[4].Sha256);
        Assert.All(artifacts, _ => Assert.Equal(LocalE2eReadOnlyCatalogInspection.Target, "[::1]:5432/pa_tickets_codex_e2e as pa_tickets_codex"));
    }

    [Theory]
    [InlineData("LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT")]
    [InlineData("M1_LIFECYCLE_REFERENCE_PREFLIGHT")]
    [InlineData("M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT")]
    [InlineData("S5-03_READ_ONLY_FINGERPRINT")]
    public void Write_capable_development_role_exception_is_limited_to_pinned_read_only_operations(string operation)
    {
        Assert.True(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException(operation, "pa_tickets_codex"));
        Assert.False(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException(operation, "pa_tickets_codex_inspector"));
        Assert.False(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException("S5-10_SCHEMA_APPLICATION", "pa_tickets_codex"));
    }

    [Theory]
    [InlineData("LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT")]
    [InlineData("S5-03_READ_ONLY_FINGERPRINT")]
    [InlineData("M1_SEQUENCE_SAFETY_PREFLIGHT")]
    [InlineData("M1_LIFECYCLE_REFERENCE_PREFLIGHT")]
    [InlineData("M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT")]
    public void Evidence_retains_rows_only_for_pinned_catalog_operations(string operation)
    {
        Assert.True(LocalE2eReadOnlyCatalogInspection.ShouldCaptureResultRows(operation));
        Assert.False(LocalE2eReadOnlyCatalogInspection.ShouldCaptureResultRows("S5-10_SCHEMA_APPLICATION"));
    }

    [Theory]
    [InlineData("S5-10_SCHEMA_APPLICATION")]
    [InlineData("arbitrary-sql")]
    [InlineData("")]
    public void Unknown_or_arbitrary_operation_is_rejected(string operation)
    {
        Assert.Throws<InvalidOperationException>(() => LocalE2eReadOnlyCatalogInspection.RequireArtifact(operation));
    }

    [Fact]
    public void Invocation_requires_exact_operation_and_explicit_confirmation()
    {
        const string operation = "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT";
        Assert.Throws<InvalidOperationException>(() => LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(operation, null, null));
        Assert.Throws<InvalidOperationException>(() => LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(operation, "S5-03_READ_ONLY_FINGERPRINT", LocalE2eReadOnlyCatalogInspection.ConfirmationValue));
        Assert.Throws<InvalidOperationException>(() => LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(operation, operation, "yes"));
        LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(operation, operation, LocalE2eReadOnlyCatalogInspection.ConfirmationValue);
    }

    [Fact]
    public void Consumed_sequence_inspection_remains_denied()
    {
        const string operation = "M1_SEQUENCE_SAFETY_PREFLIGHT";
        Assert.Throws<LocalE2eReadOnlyInspectionException>(() =>
            LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(
                operation, operation, LocalE2eReadOnlyCatalogInspection.ConfirmationValue));
        Assert.False(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException(operation, "pa_tickets_codex"));
    }

    [Fact]
    public void Lifecycle_reference_preflight_is_covered_by_adopted_standing_read_only_policy()
    {
        const string operation = "M1_LIFECYCLE_REFERENCE_PREFLIGHT";
        LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(
            operation, operation, LocalE2eReadOnlyCatalogInspection.ConfirmationValue);
        Assert.True(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException(operation, "pa_tickets_codex"));
        Assert.False(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException(operation, "postgres"));
    }

    [Fact]
    public void Permission_catalogue_sequence_preflight_is_fixed_and_read_only()
    {
        const string operation = "M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT";
        LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(
            operation, operation, LocalE2eReadOnlyCatalogInspection.ConfirmationValue);
        Assert.True(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException(operation, "pa_tickets_codex"));
        Assert.False(LocalE2eReadOnlyCatalogInspection.IsOwnerApprovedLocalRoleException(operation, "postgres"));

        var artifact = LocalE2eReadOnlyCatalogInspection.RequireArtifact(operation);
        var (sql, hash) = LocalE2eReadOnlyCatalogInspection.LoadPinnedArtifact(RepositoryRoot, artifact);
        Assert.Equal("795838E1CE3A02ADFBBE755197DB2228D52ED16C457046F57818E3912DC88609", hash);
        LocalE2eReadOnlyCatalogInspection.ValidateReadOnlyArtifactEnvelope(sql);
        Assert.Contains("PA_TICKETS_PERMISSION_SEQ", sql, StringComparison.Ordinal);
        Assert.Contains("maximum_permission_id", sql, StringComparison.Ordinal);
        Assert.Contains("next_value_conflicts_with_existing_key", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("nextval(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("setval(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT")]
    [InlineData("S5-03_READ_ONLY_FINGERPRINT")]
    [InlineData("M1_SEQUENCE_SAFETY_PREFLIGHT")]
    [InlineData("M1_LIFECYCLE_REFERENCE_PREFLIGHT")]
    [InlineData("M1_PERMISSION_CATALOGUE_SEQUENCE_PREFLIGHT")]
    public void Checked_in_artifact_bytes_match_pins_and_read_only_envelopes(string operation)
    {
        var artifact = LocalE2eReadOnlyCatalogInspection.RequireArtifact(operation);
        var artifactPath = Path.Combine(RepositoryRoot, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(artifactPath)));
        var sql = File.ReadAllText(artifactPath);
        var (_, hash) = LocalE2eReadOnlyCatalogInspection.LoadPinnedArtifact(RepositoryRoot, artifact);
        Assert.Equal(artifact.Sha256, hash);
        Assert.Equal(artifact.Sha256, actualHash);
        if (operation == "LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT")
        {
            Assert.Contains("AS member_candidate", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("AS current_role", sql, StringComparison.OrdinalIgnoreCase);
        }
        if (operation == "M1_LIFECYCLE_REFERENCE_PREFLIGHT")
        {
            Assert.Contains("N_USER_ID\" = 1001", sql, StringComparison.Ordinal);
            Assert.Contains("S_STATUS_CODE\" IN ('NEW','TRIAGE','ASSIGNED')", sql, StringComparison.Ordinal);
            Assert.Contains("N_PRIORITY_ID\" = 2 OR \"N_IS_DEFAULT", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("nextval(", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("setval(", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ALTER ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("SET TRANSACTION READ ONLY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("ROLLBACK;" + Environment.NewLine, sql.TrimEnd() + Environment.NewLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fingerprint_selects_successor_and_rejects_old_artifact_bytes_for_new_runs()
    {
        var selected = LocalE2eReadOnlyCatalogInspection.RequireArtifact("S5-03_READ_ONLY_FINGERPRINT");
        Assert.Equal("tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e_EXISTING_DB_REVIEW.sql", selected.RelativePath);
        Assert.Equal("6092C5C53E8E1BEAC07F318D57475368FC6CF73050045758F65FF91750326342", selected.Sha256);

        var oldPath = "tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_codex_e2e.sql";
        var oldArtifactWithNewPin = new LocalE2eReadOnlyArtifact("S5-03_READ_ONLY_FINGERPRINT", oldPath, selected.Sha256);
        Assert.Throws<InvalidOperationException>(() => LocalE2eReadOnlyCatalogInspection.LoadPinnedArtifact(RepositoryRoot, oldArtifactWithNewPin));
    }

    [Fact]
    public void Changed_artifact_is_rejected_before_any_connection_can_be_opened()
    {
        var artifact = LocalE2eReadOnlyCatalogInspection.RequireArtifact("LOCAL_E2E_POSTGRES_PRIVILEGE_PREFLIGHT");
        var root = Path.Combine(Path.GetTempPath(), "pa-readonly-artifact-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, "BEGIN;\nSET TRANSACTION READ ONLY;\nDELETE FROM pg_catalog.pg_roles;\nROLLBACK;\n");
            Assert.Throws<InvalidOperationException>(() => LocalE2eReadOnlyCatalogInspection.LoadPinnedArtifact(root, artifact));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Read_only_envelope_validator_rejects_write_statements()
    {
        Assert.Throws<InvalidOperationException>(() => LocalE2eReadOnlyCatalogInspection.ValidateReadOnlyArtifactEnvelope(
            "BEGIN;\nSET TRANSACTION READ ONLY;\nSELECT 1;\nDROP TABLE public.x;\nROLLBACK;\n"));
    }

    [Fact]
    public void Sequence_safety_operation_is_pinned_and_contains_no_sequence_or_database_mutation()
    {
        const string operation = "M1_SEQUENCE_SAFETY_PREFLIGHT";
        var artifact = LocalE2eReadOnlyCatalogInspection.RequireArtifact(operation);
        Assert.Equal("Docs/reconciliation/proposed/postgresql/M1_sequence_safety_inspection_REVIEW.sql", artifact.RelativePath);
        Assert.Equal("526D36EA99B7F6BCC8DED6FF419362A56675F9261FACB963331853135730EE9A", artifact.Sha256);

        var (sql, hash) = LocalE2eReadOnlyCatalogInspection.LoadPinnedArtifact(RepositoryRoot, artifact);
        Assert.Equal(artifact.Sha256, hash);
        LocalE2eReadOnlyCatalogInspection.ValidateReadOnlyArtifactEnvelope(sql);

        var body = LocalE2eReadOnlyCatalogInspection.RemoveTransactionEnvelope(sql);
        body = System.Text.RegularExpressions.Regex.Replace(body, @"(?m)--[^\r\n]*", string.Empty);
        body = System.Text.RegularExpressions.Regex.Replace(body, @"'(?:''|[^'])*'", "''");
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(
            body,
            @"\b(nextval|setval|INSERT|UPDATE|DELETE|MERGE|TRUNCATE|CREATE|ALTER|DROP|GRANT|REVOKE)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Assert.Contains("persisted_last_value", body, StringComparison.Ordinal);
        Assert.Contains("maximum_key", body, StringComparison.Ordinal);
        Assert.Contains("next_value_conflicts_with_existing_key", body, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK;", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CONFIGURATION", false, "LOCAL_CONFIGURATION_MISSING_OR_INVALID")]
    [InlineData("CONNECT", false, "CONNECTION_OR_AUTHENTICATION_FAILED")]
    [InlineData("READ_ONLY_ENFORCEMENT", true, "READ_ONLY_ENFORCEMENT_FAILED")]
    [InlineData("SESSION_IDENTITY", true, "SESSION_IDENTITY_CHECK_FAILED")]
    [InlineData("CATALOG_SQL", true, "PINNED_CATALOG_SQL_FAILED")]
    [InlineData("ROLLBACK", true, "ROLLBACK_FAILED_STOP_NO_RETRY")]
    public void Failure_classifier_records_specific_sanitized_stage(string stage, bool opened, string expected)
    {
        Assert.Equal(expected, LocalE2eReadOnlyCatalogInspection.ClassifyFailure(new Exception("sensitive provider detail"), stage, opened));
    }

    [Theory]
    [InlineData("42501", "INSUFFICIENT_PRIVILEGE")]
    [InlineData("57014", "QUERY_CANCELLED_OR_TIMEOUT")]
    [InlineData("08P01", "POSTGRESQL_CONNECTION_ERROR")]
    [InlineData("25P02", "TRANSACTION_STATE_ERROR")]
    [InlineData("42703", "SQL_SYNTAX_OR_CATALOG_ERROR")]
    [InlineData("XX000", "POSTGRESQL_EXECUTION_ERROR")]
    [InlineData(null, "UNKNOWN_POSTGRESQL_ERROR")]
    [InlineData("bad", "UNKNOWN_POSTGRESQL_ERROR")]
    public void PostgreSql_sqlstate_is_mapped_to_a_sanitized_category(string? sqlState, string expected)
    {
        Assert.Equal(expected, LocalE2eReadOnlyCatalogInspection.ClassifyPostgreSqlError(sqlState));
    }

    [Fact]
    public void Session_identity_guard_preserves_its_specific_reason_code()
    {
        var exception = Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eReadOnlyCatalogInspection.ValidateSession(new LocalE2eS503SessionIdentity(
                "wrong", "pa_tickets_codex", "::1", 5432, "on")));

        Assert.Equal("SESSION_DATABASE_MISMATCH", LocalE2eReadOnlyCatalogInspection.ClassifyFailure(exception, "SESSION_IDENTITY", true));
    }

    [Fact]
    public void Exact_session_identity_is_accepted()
    {
        LocalE2eReadOnlyCatalogInspection.ValidateSession(new LocalE2eS503SessionIdentity(
            "pa_tickets_codex_e2e", "pa_tickets_codex", "::1", 5432, "on"));
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("0:0:0:0:0:0:0:1")]
    [InlineData("::1/128")]
    public void Equivalent_exact_ipv6_loopback_representations_are_accepted(string address)
    {
        LocalE2eReadOnlyCatalogInspection.ValidateSession(new LocalE2eS503SessionIdentity(
            "pa_tickets_codex_e2e", "pa_tickets_codex", address, 5432, "on"));
    }

    [Theory]
    [InlineData("database", "SESSION_DATABASE_MISMATCH")]
    [InlineData("role", "SESSION_ROLE_MISMATCH")]
    [InlineData("address", "SESSION_ADDRESS_MISMATCH")]
    [InlineData("port", "SESSION_PORT_MISMATCH")]
    [InlineData("read-only", "SESSION_READ_ONLY_MISMATCH")]
    public void Session_identity_reports_the_exact_mismatched_field(string field, string expectedReason)
    {
        var identity = new LocalE2eS503SessionIdentity(
            field == "database" ? "other" : "pa_tickets_codex_e2e",
            field == "role" ? "other" : "pa_tickets_codex",
            field == "address" ? "127.0.0.1" : "::1",
            field == "port" ? 5433 : 5432,
            field == "read-only" ? "off" : "on");

        var exception = Assert.Throws<LocalE2eS503InspectionException>(() =>
            LocalE2eReadOnlyCatalogInspection.ValidateSession(identity));
        Assert.Equal(expectedReason, exception.ReasonCode);
    }

    [Fact]
    public void Null_or_non_host_ipv6_prefix_is_not_treated_as_exact_loopback()
    {
        foreach (var address in new string?[] { null, "::1/64", "::2" })
        {
            var exception = Assert.Throws<LocalE2eS503InspectionException>(() =>
                LocalE2eReadOnlyCatalogInspection.ValidateSession(new LocalE2eS503SessionIdentity(
                    "pa_tickets_codex_e2e", "pa_tickets_codex", address, 5432, "on")));
            Assert.Equal("SESSION_ADDRESS_MISMATCH", exception.ReasonCode);
        }
    }

    [Theory]
    [InlineData("database")]
    [InlineData("role")]
    [InlineData("address")]
    [InlineData("port")]
    [InlineData("read-only")]
    public void Same_session_target_and_transaction_mismatches_fail_closed(string mismatch)
    {
        var identity = new LocalE2eS503SessionIdentity(
            mismatch == "database" ? "other" : "pa_tickets_codex_e2e",
            mismatch == "role" ? "postgres" : "pa_tickets_codex",
            mismatch == "address" ? "127.0.0.1" : "::1",
            mismatch == "port" ? 5433 : 5432,
            mismatch == "read-only" ? "off" : "on");
        Assert.Throws<LocalE2eS503InspectionException>(() => LocalE2eReadOnlyCatalogInspection.ValidateSession(identity));
    }

    [Fact]
    public async Task Explicit_execution_test_is_dormant_without_both_opt_in_values()
    {
        var oldOperation = Environment.GetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.InvocationVariable);
        var oldConfirmation = Environment.GetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.ConfirmationVariable);
        try
        {
            Environment.SetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.InvocationVariable, null);
            Environment.SetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.ConfirmationVariable, null);
            await LocalE2eReadOnlyCatalogInspectionExecution.RunIfExplicitlySelectedAsync(RepositoryRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.InvocationVariable, oldOperation);
            Environment.SetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.ConfirmationVariable, oldConfirmation);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tests", "scripts", "postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

public sealed class LocalE2eReadOnlyCatalogInspectionExecutionTests
{
    [Fact]
    public async Task Explicitly_selected_fixed_read_only_operation_executes()
    {
        await LocalE2eReadOnlyCatalogInspectionExecution.RunIfExplicitlySelectedAsync(FindRepositoryRoot());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tests", "scripts", "postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

internal static class LocalE2eReadOnlyCatalogInspectionExecution
{
    internal static async Task RunIfExplicitlySelectedAsync(string repositoryRoot)
    {
        var operation = Environment.GetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.InvocationVariable);
        if (string.IsNullOrEmpty(operation) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.ConfirmationVariable)))
            return;

        LocalE2eReadOnlyCatalogInspection.RequireExplicitInvocation(
            operation ?? string.Empty,
            operation,
            Environment.GetEnvironmentVariable(LocalE2eReadOnlyCatalogInspection.ConfirmationVariable));
        await LocalE2eReadOnlyCatalogInspection.ExecuteAsync(repositoryRoot, operation!);
    }
}
