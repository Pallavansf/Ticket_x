using PA.Tickets.Infrastructure.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class DatabaseAuthorizationScopeTests
{
    [Theory]
    [InlineData("ORGANIZATION", "ORGANIZATION", true)]
    [InlineData("DEPARTMENT", "ORGANIZATION", true)]
    [InlineData("DEPARTMENT", "DEPARTMENT", true)]
    [InlineData("DEPARTMENT", "TEAM", false)]
    [InlineData("DEPARTMENT_ADMIN", "ORGANIZATION", true)]
    [InlineData("DEPARTMENT_ADMIN", "DEPARTMENT", true)]
    [InlineData("DEPARTMENT_ADMIN", "ADMIN_GROUP", true)]
    [InlineData("DEPARTMENT_ADMIN", "TEAM", false)]
    [InlineData("TEAM", "ORGANIZATION", true)]
    [InlineData("TEAM", "DEPARTMENT", true)]
    [InlineData("TEAM", "TEAM", true)]
    [InlineData("TEAM", "ADMIN_GROUP", false)]
    [InlineData("ADMIN_GROUP", "ADMIN_GROUP", true)]
    [InlineData("ADMIN_GROUP", "ORGANIZATION", true)]
    [InlineData("ADMIN_GROUP", "DEPARTMENT", false)]
    [InlineData("RECORD", "ORGANIZATION", true)]
    [InlineData("RECORD", "DEPARTMENT", true)]
    [InlineData("RECORD", "TEAM", true)]
    [InlineData("RECORD", "RECORD", true)]
    [InlineData("RECORD", "ADMIN_GROUP", false)]
    [InlineData("NONE", "CLIENT", true)]
    [InlineData("NONE", "UNRECOGNIZED", true)]
    [InlineData("UNRECOGNIZED", "ORGANIZATION", false)]
    [InlineData("ORGANIZATION", "UNRECOGNIZED", false)]
    public void Scope_type_matches_only_the_defined_permission_scope_hierarchy(
        string scopeClass, string scopeType, bool expected) =>
        Assert.Equal(expected, DatabaseAuthorizationService.ScopeMatchesClass(scopeType, scopeClass));

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, 10L, false)]
    [InlineData(10L, 10L, true)]
    [InlineData(10L, 11L, false)]
    public void Organization_scope_matches_only_the_same_explicit_organization(long? scopeId, long? resourceId, bool expected) =>
        Assert.Equal(expected, OrganizationScopeAuthorization.MatchesResourceOrganization(scopeId, resourceId));
    [Fact]
    public void Scope_class_matching_is_case_insensitive()
    {
        Assert.True(DatabaseAuthorizationService.ScopeMatchesClass("TEAM", "team"));
        Assert.False(DatabaseAuthorizationService.ScopeMatchesClass("team", "TEAM"));
    }
}




