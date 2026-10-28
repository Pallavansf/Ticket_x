using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PA.Tickets.Domain.Tests;

public sealed class PostgreSqlPrivilegePreflightArtifactTests
{
    private const string ArtifactPath = "tests/scripts/postgresql-s5-03-privilege-preflight-pa_tickets_codex_e2e.sql";
    private const string ExpectedSha256 = "C9B672971C5BFD58B17B0D42C70734BA2BBA315DF59E591999DA231DDA93F08C";

    [Fact]
    public void Privilege_preflight_is_exact_target_pinned_and_hash_bound()
    {
        var sql = ReadArtifact();
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql)));

        Assert.Equal(ExpectedSha256, hash);
        Assert.Contains("current_database() IS DISTINCT FROM 'pa_tickets_codex_e2e'", sql, StringComparison.Ordinal);
        Assert.Contains("current_user IS DISTINCT FROM 'pa_tickets_codex'", sql, StringComparison.Ordinal);
        Assert.Contains("inet_server_addr() IS DISTINCT FROM '::1'::inet", sql, StringComparison.Ordinal);
        Assert.Contains("inet_server_port() IS DISTINCT FROM 5432", sql, StringComparison.Ordinal);
        Assert.Contains("current_setting('transaction_read_only') IS DISTINCT FROM 'on'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Privilege_preflight_reports_requested_effective_privilege_facts()
    {
        var sql = ReadArtifact();
        foreach (var required in new[]
        {
            "rolsuper", "rolcreatedb", "rolcreaterole", "rolreplication", "rolbypassrls", "rolinherit",
            "granted_role_is_superuser", "granted_role_can_create_database", "granted_role_can_create_roles",
            "pg_auth_members", "inherit_option", "set_option", "pg_has_role",
            "has_database_privilege", "'CONNECT'", "'CREATE'", "has_schema_privilege", "'USAGE'",
            "has_table_privilege", "has_column_privilege", "'SELECT'", "'INSERT'", "'UPDATE'", "'DELETE'", "'TRUNCATE'",
            "has_sequence_privilege", "'UPDATE'", "PRIVILEGE_PREFLIGHT_COMPLETE"
        })
            Assert.Contains(required, sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Artifact_is_read_only_and_contains_no_dynamic_or_write_statement()
    {
        var sql = ReadArtifact();
        Assert.Contains("BEGIN;", sql, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY;", sql, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("nextval(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("setval(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXECUTE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(
            new Regex(@"(?im)^\s*(CREATE|ALTER|DROP|TRUNCATE|INSERT|UPDATE|DELETE|GRANT|REVOKE|COPY|CALL|VACUUM|ANALYZE|REFRESH)\b"),
            sql);

        var guard = sql.IndexOf("DO $target_guard$", StringComparison.Ordinal);
        var firstPrivilegeCatalogQuery = sql.IndexOf("FROM pg_catalog.pg_roles", StringComparison.Ordinal);
        Assert.True(guard >= 0 && firstPrivilegeCatalogQuery > guard,
            "The session guard must occur before role catalog inspection.");
    }

    private static string ReadArtifact()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "PinnedPrivilegePreflight", ArtifactPath);
        return File.ReadAllText(fixture);
    }
}
