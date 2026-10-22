namespace PA.Tickets.Application.Foundations;

/// <summary>
/// Reads the authorization fact needed by the global Spectator mutation guard.
/// Implementations must fail closed when the membership fact cannot be obtained.
/// </summary>
public interface ISpectatorMembershipReader
{
    Task<bool> IsActiveSpectatorAsync(
        long userId,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
