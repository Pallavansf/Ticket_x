namespace PA.Tickets.Application.Foundations;

public sealed record AdminHeadDelegationFacts(
    bool TargetIsPrivileged,
    bool TargetIsDelegatedAdminOnly,
    bool HasActiveDelegation);

public interface IAdminHeadDelegationFactsReader
{
    Task<AdminHeadDelegationFacts> ReadAsync(
        long adminHeadUserId,
        long targetUserId,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}

public sealed record ActiveAdministrationTargetFact(
    long Id,
    long? DepartmentId,
    long? TeamId,
    bool Active);

public interface IAdministrationTargetReader
{
    Task<ActiveAdministrationTargetFact?> ReadActiveAsync(long userId, CancellationToken cancellationToken);
}
