using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Application.Tickets;

public static class TicketStatusTransitionPolicy
{
    public static async Task EnsureAllowedAsync(
        ITicketStatusConfigurationRepository configuration,
        long actor,
        TicketStatus from,
        TicketStatus to,
        CancellationToken ct)
    {
        var fromCode = TicketStatusCodes.ToConfigurationCode(from);
        var toCode = TicketStatusCodes.ToConfigurationCode(to);
        if (!await configuration.CanTransitionAsync(actor, fromCode, toCode, ct))
            throw new InvalidOperationException($"The configured lifecycle does not allow a status transition from {fromCode} to {toCode}.");
    }
}
