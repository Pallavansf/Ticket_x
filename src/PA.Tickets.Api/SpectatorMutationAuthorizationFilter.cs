using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Api;

public sealed class SpectatorMutationAuthorizationFilter(ISpectatorMembershipReader membershipReader, TimeProvider clock) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var readRequest = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
        var authenticated = context.HttpContext.User.Identity?.IsAuthenticated == true;
        if (!authenticated || readRequest)
        {
            await next();
            return;
        }

        var subject = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            context.HttpContext.User.FindFirstValue("sub");
        if (!long.TryParse(subject, out var userId) || userId <= 0)
        {
            if (SpectatorMutationPolicy.ShouldDenyMutation(readRequest, authenticated, false, false))
                context.Result = new ForbidResult();
            return;
        }

        var now = clock.GetUtcNow();
        var isActiveSpectator = await membershipReader.IsActiveSpectatorAsync(
            userId,
            now,
            context.HttpContext.RequestAborted);
        if (SpectatorMutationPolicy.ShouldDenyMutation(readRequest, authenticated, true, isActiveSpectator))
        {
            context.Result = new ObjectResult(new
            {
                title = "Spectator memberships cannot perform write operations.",
                status = StatusCodes.Status403Forbidden
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
