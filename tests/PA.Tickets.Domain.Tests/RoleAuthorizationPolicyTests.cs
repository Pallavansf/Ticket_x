using PA.Tickets.Infrastructure.Foundations;
using SystemRoleDisplayLabelPolicy = PA.Tickets.Application.Foundations.SystemRoleDisplayLabelPolicy;

namespace PA.Tickets.Domain.Tests;

public sealed class RoleAuthorizationPolicyTests
{
    [Theory]
    [InlineData("ADMIN", " Manager ", "Manager")]
    [InlineData("ADMIN_HEAD", "Manager Admin", "Manager Admin")]
    public void Only_fixed_role_display_labels_are_configurable_without_changing_role_codes(
        string code, string label, string expected) =>
        Assert.Equal(expected, SystemRoleDisplayLabelPolicy.ValidateAndNormalize(code, true, label));

    [Theory]
    [InlineData("USER", true)]
    [InlineData("SUPER_ADMIN", true)]
    [InlineData("SPECTATOR", true)]
    [InlineData("ADMIN_HEAD", false)]
    [InlineData("admin", true)]
    public void Other_or_ambiguous_role_identities_cannot_change_display_labels(string code, bool systemRole) =>
        Assert.Throws<UnauthorizedAccessException>(() =>
            SystemRoleDisplayLabelPolicy.ValidateAndNormalize(code, systemRole, "New label"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_system_role_display_labels_are_rejected(string? label) =>
        Assert.Throws<ArgumentException>(() =>
            SystemRoleDisplayLabelPolicy.ValidateAndNormalize("ADMIN", true, label));

    [Theory]
    [InlineData("USER", "BUSINESS", false, true)]
    [InlineData("USER", "READ_ONLY", true, true)]
    [InlineData("USER", "DEPARTMENT_ADMIN", false, false)]
    [InlineData("USER", "ORGANIZATION_ADMIN", false, false)]
    [InlineData("USER", "SECURITY_GOVERNANCE", false, false)]
    public void User_permissions_are_limited_by_the_permission_security_class(
        string role, string securityClass, bool readOnly, bool expected) =>
        Assert.Equal(expected, RoleAuthorizationPolicy.AllowsPermission(role, true, securityClass,
            readOnly ? "VIEW" : "EDIT", readOnly));

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("ADMIN_HEAD")]
    public void Department_administrators_may_receive_scoped_but_not_organization_permissions(string role)
    {
        Assert.True(RoleAuthorizationPolicy.AllowsPermission(role, true, "BUSINESS", "EDIT", false));
        Assert.True(RoleAuthorizationPolicy.AllowsPermission(role, true, "DEPARTMENT_ADMIN", "MANAGE", false));
        Assert.True(RoleAuthorizationPolicy.AllowsPermission(role, true, "READ_ONLY", "VIEW", true));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission(role, true, "ORGANIZATION_ADMIN", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission(role, true, "SECURITY_GOVERNANCE", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission(role, true, "SECURITY_GOVERNANCE", "SCOPE", false));
    }

    [Fact]
    public void Super_admin_accepts_known_permission_security_classes_but_rejects_unknown_metadata()
    {
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("SUPER_ADMIN", true, "BUSINESS", "EDIT", false));
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("SUPER_ADMIN", true, "ORGANIZATION_ADMIN", "MANAGE", false));
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("SUPER_ADMIN", true, "SECURITY_GOVERNANCE", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SUPER_ADMIN", true, "UNKNOWN", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SUPER_ADMIN", false, "BUSINESS", "EDIT", false));
    }

    [Fact]
    public void Spectator_is_read_only_even_when_permission_metadata_is_inconsistent()
    {
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("SPECTATOR", true, "READ_ONLY", "VIEW", true));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SPECTATOR", true, "READ_ONLY", "VIEW", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SPECTATOR", true, "BUSINESS", "EDIT", true));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SPECTATOR", false, "READ_ONLY", "VIEW", true));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SPECTATOR", true, "READ_ONLY", "EDIT", true));
    }

    [Fact]
    public void Legacy_administrator_grants_are_narrowly_limited_to_ticket_operations()
    {
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("ADMINISTRATOR", true, "BUSINESS", "EDIT", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("ADMINISTRATOR", true, "DEPARTMENT_ADMIN", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.IsLegacyAdministratorPermission("USER_ADMIN"));
        Assert.True(RoleAuthorizationPolicy.IsLegacyAdministratorPermission("TICKET_READ"));
        Assert.True(RoleAuthorizationPolicy.IsLegacyAdministratorPermission("TICKET_UPDATE"));
    }

    [Fact]
    public void Operations_and_auditor_retain_only_permissions_allowed_by_database_mappings_and_security_class()
    {
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("OPERATIONS", true, "DEPARTMENT_ADMIN", "MANAGE", false));
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("AUDITOR", true, "READ_ONLY", "EXPORT", true));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("AUDITOR", true, "BUSINESS", "EDIT", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("OPERATIONS", true, "SECURITY_GOVERNANCE", "MANAGE", false));
    }

    [Fact]
    public void Custom_role_permissions_are_data_driven_but_cannot_receive_governance_authority()
    {
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("SLA_MANAGER", false, "BUSINESS", "EDIT", false));
        Assert.True(RoleAuthorizationPolicy.AllowsPermission("SLA_MANAGER", false, "DEPARTMENT_ADMIN", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SLA_MANAGER", false, "ORGANIZATION_ADMIN", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("SLA_MANAGER", false, "SECURITY_GOVERNANCE", "MANAGE", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("bad-code", false, "BUSINESS", "EDIT", false));
        Assert.False(RoleAuthorizationPolicy.AllowsPermission("UNKNOWN_ROLE", true, "BUSINESS", "EDIT", false));
    }

    [Theory]
    [InlineData("DEPARTMENT_ADMIN", true)]
    [InlineData("ORGANIZATION_ADMIN", true)]
    [InlineData("SECURITY_GOVERNANCE", true)]
    [InlineData("department_admin", true)]
    [InlineData("BUSINESS", false)]
    [InlineData("READ_ONLY", false)]
    [InlineData("UNKNOWN", false)]
    public void High_impact_permission_classes_require_independent_approval(string securityClass, bool expected) =>
        Assert.Equal(expected, RoleAuthorizationPolicy.RequiresIndependentApproval(securityClass));
}
