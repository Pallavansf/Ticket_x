using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PA.Tickets.Domain.Tests;

public sealed class M1TicketReadUpdatePermissionBootstrapArtifactTests
{
    private const string ExpectedHash = "182A710F31BD27E8111932701F099C56D795D38C83A69318DEF4920C9DF4A8F3";

    [Fact]
    public void Bootstrap_defines_only_the_two_canonical_ticket_permissions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "M1TicketPermissionBootstrap", "M1_TICKET_READ_UPDATE_PERMISSION_BOOTSTRAP_REVIEW.sql");
        var bytes = File.ReadAllBytes(path);
        var sql = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Equal(ExpectedHash, Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.Contains("'TICKET_READ', 'View tickets'", sql, StringComparison.Ordinal);
        Assert.Contains("true, 'TICKETS', 'VIEW', 'RECORD', 'READ_ONLY', true", sql, StringComparison.Ordinal);
        Assert.Contains("'TICKET_UPDATE', 'Update tickets'", sql, StringComparison.Ordinal);
        Assert.Contains("true, 'TICKETS', 'EDIT', 'RECORD', 'BUSINESS', false", sql, StringComparison.Ordinal);
        Assert.Equal(2, sql.Split("INSERT INTO public.\"PA_TICKETS_PERMISSION\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("INSERT INTO public.\"PA_TICKETS_ROLE_PERMISSION\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO public.\"PA_TICKETS_USER_ROLE\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"(?im)^\s*(UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE|GRANT|REVOKE|setval)\b"), sql);
        Assert.Equal(2, Regex.Matches(sql, @"SELECT\s+nextval\s*\(", RegexOptions.IgnoreCase).Count);
        Assert.DoesNotContain("setval(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bootstrap_requires_034_and_guards_target_compatibility_and_allocation()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "M1TicketPermissionBootstrap", "M1_TICKET_READ_UPDATE_PERMISSION_BOOTSTRAP_REVIEW.sql");
        var sql = File.ReadAllText(path);

        Assert.Contains("current_database() <> 'pa_tickets_codex_e2e'", sql, StringComparison.Ordinal);
        Assert.Contains("current_user <> 'pa_tickets_codex'", sql, StringComparison.Ordinal);
        Assert.Contains("inet_server_addr() IS DISTINCT FROM '::1'::inet", sql, StringComparison.Ordinal);
        Assert.Contains("inet_server_port() <> 5432", sql, StringComparison.Ordinal);
        Assert.Contains("LOCK TABLE public.\"PA_TICKETS_PERMISSION\" IN SHARE ROW EXCLUSIVE MODE", sql, StringComparison.Ordinal);
        Assert.Contains("exact 034 metadata column definitions", sql, StringComparison.Ordinal);
        Assert.Contains("duplicate canonical permission codes", sql, StringComparison.Ordinal);
        Assert.Contains("conflicting canonical permission metadata", sql, StringComparison.Ordinal);
        Assert.Contains("max(\"N_PERMISSION_ID\")", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission sequence collision risk", sql, StringComparison.Ordinal);
        Assert.Contains("postcondition failed", sql, StringComparison.Ordinal);
        Assert.Contains("BEGIN;", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;" + Environment.NewLine, sql.TrimEnd() + Environment.NewLine, StringComparison.Ordinal);
    }
}
