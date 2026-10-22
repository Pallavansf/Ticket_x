namespace PA.Tickets.Api;

public sealed class PasswordRotationRequiredMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requiresRotation = context.User.HasClaim("password_change_required", "true");
        var isLogin = HttpMethods.IsPost(context.Request.Method) &&
            string.Equals(context.Request.Path.Value, "/api/v1/auth/login", StringComparison.OrdinalIgnoreCase);
        var isPasswordChange = HttpMethods.IsPost(context.Request.Method) &&
            string.Equals(context.Request.Path.Value, "/api/v1/auth/change-password", StringComparison.OrdinalIgnoreCase);
        if (context.User.Identity?.IsAuthenticated == true && requiresRotation && !isPasswordChange && !isLogin)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Password change is required before using this account.",
                status = StatusCodes.Status403Forbidden
            });
            return;
        }

        await next(context);
    }
}
