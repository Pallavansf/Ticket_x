using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;

namespace PA.Tickets.Api.Realtime;

public sealed record RealtimeTicketScope(long TicketId, long? DepartmentId, long? TeamId, long? ClientId, long? AssignedUserId, long? RequesterUserId = null);

public interface IRealtimeTicketScopeReader
{
    Task<RealtimeTicketScope?> GetAsync(long ticketId, CancellationToken ct);
}

public sealed class RealtimeTicketScopeReader(ITicketRepository tickets) : IRealtimeTicketScopeReader
{
    public async Task<RealtimeTicketScope?> GetAsync(long ticketId, CancellationToken ct)
    {
        var ticket = await tickets.GetAsync(ticketId, ct);
        return ticket is null ? null : new(ticket.Id, ticket.CurrentDepartmentId, ticket.CurrentTeamId, ticket.ClientId, ticket.CurrentAssignee, ticket.RequestedBy);
    }
}

public sealed class RealtimeTicketSubscriptionAuthorizer(
    IRealtimeTicketScopeReader scopes,
    PA.Tickets.Application.Foundations.IAuthorizationService authorization,
    IFeatureSwitchService featureSwitches)
{
    public async Task<bool> CanSubscribeAsync(long actorUserId, long ticketId, CancellationToken ct)
    {
        if (actorUserId <= 0 || ticketId <= 0) return false;
        var scope = await scopes.GetAsync(ticketId, ct);
        if (scope is null || !await featureSwitches.IsEnabledAsync("REALTIME", scope.DepartmentId, ct)) return false;
        var resource = new AuthorizationResource(scope.TicketId, scope.DepartmentId, scope.TeamId, scope.ClientId,
            scope.AssignedUserId, RequesterUserId: scope.RequesterUserId);
        return await authorization.HasPermissionAsync(actorUserId, "TICKET_READ", resource, ct);
    }
}
