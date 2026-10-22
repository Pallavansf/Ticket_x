namespace PA.Tickets.Application.Foundations;

public sealed record LocalAuthenticatedUser(long UserId, string LoginName, string DisplayName, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions, bool PasswordRotationRequired);

public interface ILocalAuthenticationService
{
    Task<LocalAuthenticatedUser?> AuthenticateAsync(string loginName, string password, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}
