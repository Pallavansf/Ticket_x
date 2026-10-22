using ApplicationAdminHeadDelegationPolicy = PA.Tickets.Application.Foundations.AdminHeadDelegationPolicy;

namespace PA.Tickets.Domain.Tests;

public sealed class AdminHeadDelegationPolicyTests
{
    [Fact]
    public void Active_admin_without_privileged_overlap_is_eligible()
    {
        Assert.True(ApplicationAdminHeadDelegationPolicy.IsDelegatedAdminOnly(["ADMIN"]));
        Assert.True(ApplicationAdminHeadDelegationPolicy.IsDelegatedAdminOnly(["USER", "ADMIN"]));
    }

    [Theory]
    [InlineData("ADMIN_HEAD")]
    [InlineData("SUPER_ADMIN")]
    [InlineData("ADMINISTRATOR")]
    public void Any_privileged_overlap_denies_delegated_admin_only_status(string overlappingRole) =>
        Assert.False(ApplicationAdminHeadDelegationPolicy.IsDelegatedAdminOnly(["ADMIN", overlappingRole]));

    [Fact]
    public void Missing_admin_unknown_and_empty_facts_fail_closed()
    {
        Assert.False(ApplicationAdminHeadDelegationPolicy.IsDelegatedAdminOnly([]));
        Assert.False(ApplicationAdminHeadDelegationPolicy.IsDelegatedAdminOnly(["USER"]));
        Assert.False(ApplicationAdminHeadDelegationPolicy.IsDelegatedAdminOnly(["admin"]));
        Assert.False(ApplicationAdminHeadDelegationPolicy.IsDelegatedAdminOnly(["UNKNOWN_ROLE"]));
    }
}
