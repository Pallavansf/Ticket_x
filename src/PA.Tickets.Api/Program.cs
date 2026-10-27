using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using PA.Tickets.Api;
using PA.Tickets.Application;
using PA.Tickets.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 51L * 1024 * 1024);
builder.Services.AddControllers(options => options.Filters.Add<SpectatorMutationAuthorizationFilter>());
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 16 * 1024;
    options.MaximumParallelInvocationsPerClient = 4;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddCheck("liveness", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<DatabaseReadinessCheck>("database", tags: new[] { "ready" })
    .AddCheck<WorkerReadinessCheck>("workers", tags: new[] { "ready" });
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddScoped<PA.Tickets.Application.Foundations.IContentScanner,
        PA.Tickets.Infrastructure.Foundations.DeterministicContentScanner>();
}
ApiSecurityConfiguration.AddAuthenticationAndCors(builder.Services, builder.Configuration, builder.Environment);
builder.Services.AddScoped<PA.Tickets.Application.Foundations.IRealtimePublisher,
    PA.Tickets.Api.Realtime.SignalRRealtimePublisher>();
builder.Services.AddScoped<PA.Tickets.Api.Realtime.IRealtimeTicketScopeReader,
    PA.Tickets.Api.Realtime.RealtimeTicketScopeReader>();
builder.Services.AddScoped<PA.Tickets.Api.Realtime.RealtimeTicketSubscriptionAuthorizer>();
builder.Services.AddScoped<PA.Tickets.Api.Realtime.RealtimeTicketDeliveryFilter>();
builder.Services.AddSingleton<PA.Tickets.Api.Realtime.RealtimeConnectionSubscriptions>();

var app = builder.Build();
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
    var correlationId = supplied.Length is > 0 and <= 100 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' || c == '.' || c == ':')
        ? supplied : Guid.NewGuid().ToString("N");
    context.TraceIdentifier = correlationId;
    context.Response.OnStarting(() => { context.Response.Headers["X-Correlation-ID"] = correlationId; return Task.CompletedTask; });
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    try { await next(); }
    finally
    {
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        var status = context.Response.StatusCode;
        var method = context.Request.Method;
        PA.Tickets.Infrastructure.Execution.OperationalMetrics.Requests.Add(1, new KeyValuePair<string, object?>("http.request.method", method), new KeyValuePair<string, object?>("http.response.status_code", status));
        PA.Tickets.Infrastructure.Execution.OperationalMetrics.RequestDuration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("http.request.method", method));
        if (status >= 500) PA.Tickets.Infrastructure.Execution.OperationalMetrics.RequestFailures.Add(1, new KeyValuePair<string, object?>("http.request.method", method));
        app.Logger.LogInformation("HTTP request completed {RequestId} {CorrelationId} {Method} {StatusCode} {DurationMs}", context.TraceIdentifier, correlationId, method, status, elapsed.TotalMilliseconds);
    }
});
ApiSecurityConfiguration.UseTrustedForwardedHeaders(app, builder.Configuration, builder.Environment);
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = error switch
    {
        PA.Tickets.Application.Foundations.AttachmentTooLargeException => StatusCodes.Status413PayloadTooLarge,
        PA.Tickets.Application.Foundations.ContentScannerUnavailableException => StatusCodes.Status503ServiceUnavailable,
        PA.Tickets.Application.Foundations.ContentRejectedException => StatusCodes.Status422UnprocessableEntity,
        PA.Tickets.Application.Foundations.FeatureDisabledException => StatusCodes.Status503ServiceUnavailable,
        PA.Tickets.Application.Foundations.FeatureSwitchUnavailableException => StatusCodes.Status503ServiceUnavailable,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
        InvalidOperationException => StatusCodes.Status409Conflict,
        ArgumentException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };
    var title = status switch
    {
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status400BadRequest => "Invalid request",
        StatusCodes.Status413PayloadTooLarge => "File too large",
        StatusCodes.Status422UnprocessableEntity => "File content rejected",
        StatusCodes.Status503ServiceUnavailable => error switch
        {
            PA.Tickets.Application.Foundations.FeatureDisabledException => "Feature unavailable",
            PA.Tickets.Application.Foundations.FeatureSwitchUnavailableException => "Feature configuration unavailable",
            _ => "File security scanning unavailable"
        },
        _ => "An unexpected error occurred."
    };
    context.Response.StatusCode = status;
    var code = error is PA.Tickets.Application.Foundations.FeatureDisabledException
        ? PA.Tickets.Application.Foundations.FeatureDisabledException.ErrorCode
        : error is PA.Tickets.Application.Foundations.FeatureSwitchUnavailableException
            ? PA.Tickets.Application.Foundations.FeatureSwitchUnavailableException.ErrorCode
            : null;
    await context.Response.WriteAsJsonAsync(new { title, status, code, traceId = context.TraceIdentifier });
}));
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (app.Environment.IsProduction())
{
    app.UseHsts();
}
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseCors("Client");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<PasswordRotationRequiredMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PA.Tickets.Api.Realtime.RealtimeHub>("/hubs/updates").RequireAuthorization();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public sealed class TestingAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var raw) ||
            !long.TryParse(raw.ToString(), out var userId) ||
            userId <= 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, Request.Headers.TryGetValue("X-Test-Role", out var role) ? role.ToString() : "User")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public partial class Program { }
