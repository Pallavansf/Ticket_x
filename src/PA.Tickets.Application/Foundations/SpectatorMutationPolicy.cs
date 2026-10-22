namespace PA.Tickets.Application.Foundations;

/// <summary>Pure decision for the global Spectator mutation veto.</summary>
public static class SpectatorMutationPolicy
{
    public static bool ShouldDenyMutation(
        bool readRequest,
        bool authenticated,
        bool validUserIdentity,
        bool activeSpectatorMembership) =>
        !readRequest && authenticated && (!validUserIdentity || activeSpectatorMembership);
}
