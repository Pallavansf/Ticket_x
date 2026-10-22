using ApplicationScopeAuthorizationPolicy = PA.Tickets.Application.Foundations.ScopeAuthorizationPolicy;

namespace PA.Tickets.Domain.Tests;

public sealed class ApplicationScopeAuthorizationPolicyTests
{
    [Theory]
    [InlineData("ORGANIZATION", "ORGANIZATION", true)]
    [InlineData("ORGANIZATION", "DEPARTMENT", true)]
    [InlineData("DEPARTMENT", "TEAM", true)]
    [InlineData("TEAM", "RECORD", true)]
    [InlineData("TEAM", "DEPARTMENT", false)]
    [InlineData("ADMIN_GROUP", "TEAM", false)]
    public void Hierarchy_matches_only_the_existing_scope_rules(string scopeType, string scopeClass, bool expected) =>
        Assert.Equal(expected, ApplicationScopeAuthorizationPolicy.ScopeMatchesClass(scopeType, scopeClass));

    [Theory]
    [InlineData("TEAM", "team", true)]
    [InlineData("team", "TEAM", false)]
    [InlineData("UNRECOGNIZED", "ORGANIZATION", false)]
    [InlineData("ORGANIZATION", "UNRECOGNIZED", false)]
    public void Unknown_or_mixed_case_scope_facts_fail_closed_except_for_defined_class_normalization(
        string scopeType, string scopeClass, bool expected) =>
        Assert.Equal(expected, ApplicationScopeAuthorizationPolicy.ScopeMatchesClass(scopeType, scopeClass));

    [Theory]
    [InlineData("CLIENT", true)]
    [InlineData("UNRECOGNIZED", true)]
    public void None_scope_class_retains_existing_unscoped_class_rule(string scopeType, bool expected) =>
        Assert.Equal(expected, ApplicationScopeAuthorizationPolicy.ScopeMatchesClass(scopeType, "NONE"));
}
