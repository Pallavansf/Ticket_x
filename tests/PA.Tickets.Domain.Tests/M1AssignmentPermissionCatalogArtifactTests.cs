using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PA.Tickets.Domain.Tests;

public sealed class M1AssignmentPermissionCatalogArtifactTests
{
    private const string ExpectedReviewHash = "B9FDD7D00DA392642359C9F442E71E01806036944CAFA466A3B9FF3BE7D7F10D";

    [Fact]
    public void Review_candidate_defines_exact_permission_metadata_without_granting_roles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "M1AssignmentPermissionReview", "M1_TICKET_ASSIGN_TEAM_PERMISSION_CATALOGUE_REVIEW.sql");
        var sql = File.ReadAllText(path);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

        Assert.Equal(ExpectedReviewHash, hash);
        Assert.Contains("'TICKET_ASSIGN_TEAM', 'Assign tickets to an authorized team'", sql, StringComparison.Ordinal);
        Assert.Contains("true, 'TICKETS', 'ASSIGN', 'RECORD', 'BUSINESS', false", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO public.\"PA_TICKETS_ROLE_PERMISSION\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"(?im)^\s*(GRANT|REVOKE)\b"), sql);
        Assert.Contains("TICKET_READ", sql, StringComparison.Ordinal);
        Assert.Contains("TICKET_UPDATE", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
    }
}
