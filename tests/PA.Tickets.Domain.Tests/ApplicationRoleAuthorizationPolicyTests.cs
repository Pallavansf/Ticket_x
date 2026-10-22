using ApplicationRoleAuthorizationPolicy = PA.Tickets.Application.Foundations.RoleAuthorizationPolicy;

namespace PA.Tickets.Domain.Tests;

public sealed class ApplicationRoleAuthorizationPolicyTests
{
    [Fact]
    public void Extracted_policy_preserves_spectator_read_only_ceiling()
    {
        Assert.True(ApplicationRoleAuthorizationPolicy.AllowsPermission("SPECTATOR", true, "READ_ONLY", "VIEW", true));
        Assert.False(ApplicationRoleAuthorizationPolicy.AllowsPermission("SPECTATOR", true, "BUSINESS", "EDIT", false));
        Assert.False(ApplicationRoleAuthorizationPolicy.AllowsPermission("SPECTATOR", false, "READ_ONLY", "VIEW", true));
    }

    [Fact]
    public void Extracted_policy_fails_closed_for_unknown_metadata_and_custom_governance()
    {
        Assert.False(ApplicationRoleAuthorizationPolicy.AllowsPermission("SUPER_ADMIN", true, "UNKNOWN", "EDIT", false));
        Assert.False(ApplicationRoleAuthorizationPolicy.AllowsPermission("CUSTOM_ROLE", false, "SECURITY_GOVERNANCE", "MANAGE", false));
        Assert.False(ApplicationRoleAuthorizationPolicy.AllowsPermission("bad-code", false, "BUSINESS", "EDIT", false));
    }

    [Fact]
    public void Extracted_policy_preserves_legacy_permission_ceiling()
    {
        Assert.True(ApplicationRoleAuthorizationPolicy.IsLegacyAdministratorPermission("TICKET_READ"));
        Assert.True(ApplicationRoleAuthorizationPolicy.IsLegacyAdministratorPermission("TICKET_UPDATE"));
        Assert.False(ApplicationRoleAuthorizationPolicy.IsLegacyAdministratorPermission("USER_ADMIN"));
    }
}
