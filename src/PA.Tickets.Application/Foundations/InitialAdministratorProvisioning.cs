namespace PA.Tickets.Application.Foundations;

public interface IInitialAdministratorProvisioner
{
    Task ProvisionAsync(
        string loginName,
        string displayName,
        string? email,
        string temporaryPassword,
        CancellationToken cancellationToken);
}
