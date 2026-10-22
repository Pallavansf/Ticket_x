using ApplicationSpectatorMutationPolicy = PA.Tickets.Application.Foundations.SpectatorMutationPolicy;

namespace PA.Tickets.Domain.Tests;

public sealed class SpectatorMutationPolicyTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    public void Read_requests_and_unauthenticated_requests_are_not_vetoed(
        bool readRequest, bool authenticated, bool validIdentity, bool spectator) =>
        Assert.False(ApplicationSpectatorMutationPolicy.ShouldDenyMutation(
            readRequest, authenticated, validIdentity, spectator));

    [Fact]
    public void Active_spectator_membership_vetoes_authenticated_mutations_even_with_other_roles()
    {
        Assert.True(ApplicationSpectatorMutationPolicy.ShouldDenyMutation(
            readRequest: false, authenticated: true, validUserIdentity: true, activeSpectatorMembership: true));
    }

    [Fact]
    public void Invalid_authenticated_identity_is_denied_before_membership_can_allow_a_mutation()
    {
        Assert.True(ApplicationSpectatorMutationPolicy.ShouldDenyMutation(
            readRequest: false, authenticated: true, validUserIdentity: false, activeSpectatorMembership: false));
    }

    [Fact]
    public void Valid_non_spectator_identity_reaches_the_action()
    {
        Assert.False(ApplicationSpectatorMutationPolicy.ShouldDenyMutation(
            readRequest: false, authenticated: true, validUserIdentity: true, activeSpectatorMembership: false));
    }
}
