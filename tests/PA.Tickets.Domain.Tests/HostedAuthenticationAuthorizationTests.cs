using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PA.Tickets.Api;
using PA.Tickets.Api.Controllers;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class HostedAuthenticationAuthorizationTests
{
    [Fact]
    public async Task Hosted_auth_pipeline_preserves_login_and_enforces_password_change_authorization()
    {
        var service = new StubLocalAuthenticationService();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(AuthenticationController).Assembly.FullName,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton<ILocalAuthenticationService>(service);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Local:SigningKey"] = "hosted-auth-test-signing-key-32-bytes-minimum",
                ["Auth:Local:Issuer"] = "pa-tickets-hosted-test",
                ["Auth:Audience"] = "pa-tickets-hosted-test",
                ["Auth:Local:TokenLifetimeMinutes"] = "15"
            })
            .Build());
        builder.Services
            .AddAuthentication("HostedTest")
            .AddScheme<AuthenticationSchemeOptions, HostedTestAuthenticationHandler>("HostedTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
            options.AddFixedWindowLimiter("login", limiter =>
            {
                limiter.PermitLimit = 5;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            }));
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthenticationController).Assembly);
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
        }).AddMvc();

        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<PasswordRotationRequiredMiddleware>();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var address = server.Features
                .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                .Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };

            using var login = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest("test-user", "test-password"));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using var anonymousPasswordChange = await client.PostAsJsonAsync(
                "/api/v1/auth/change-password",
                new ChangePasswordRequest("current-password", "new-password-long", "new-password-long"));
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousPasswordChange.StatusCode);
            Assert.Equal(0, service.ChangePasswordCalls);

            using var authenticated = new HttpRequestMessage(
                HttpMethod.Post, "/api/v1/auth/change-password")
            {
                Content = JsonContent.Create(
                    new ChangePasswordRequest("wrong-current", "new-password-long", "new-password-long"))
            };
            authenticated.Headers.Add("X-Hosted-Test-Subject", "4107");
            using var invalidCurrentPassword = await client.SendAsync(authenticated);
            Assert.Equal(HttpStatusCode.Unauthorized, invalidCurrentPassword.StatusCode);
            Assert.Equal(4107, service.LastChangePasswordUserId);
            Assert.Equal("wrong-current", service.LastCurrentPassword);

            using var validRequest = new HttpRequestMessage(
                HttpMethod.Post, "/api/v1/auth/change-password")
            {
                Content = JsonContent.Create(
                    new ChangePasswordRequest("current-password", "new-password-long", "new-password-long"))
            };
            validRequest.Headers.Add("X-Hosted-Test-Subject", "4107");
            using var validCurrentPassword = await client.SendAsync(validRequest);
            Assert.Equal(HttpStatusCode.NoContent, validCurrentPassword.StatusCode);
            Assert.Equal(4107, service.LastChangePasswordUserId);
            Assert.Equal("current-password", service.LastCurrentPassword);
            Assert.Equal(2, service.ChangePasswordCalls);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class StubLocalAuthenticationService : ILocalAuthenticationService
    {
        public int ChangePasswordCalls { get; private set; }
        public long? LastChangePasswordUserId { get; private set; }
        public string? LastCurrentPassword { get; private set; }

        public Task<LocalAuthenticatedUser?> AuthenticateAsync(
            string loginName,
            string password,
            CancellationToken cancellationToken) =>
            Task.FromResult<LocalAuthenticatedUser?>(
                loginName == "test-user" && password == "test-password"
                    ? new LocalAuthenticatedUser(4107, loginName, "Hosted Test User", [], [], true)
                    : null);

        public Task<bool> ChangePasswordAsync(
            long userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken)
        {
            ChangePasswordCalls++;
            LastChangePasswordUserId = userId;
            LastCurrentPassword = currentPassword;
            return Task.FromResult(currentPassword == "current-password");
        }
    }

    private sealed class HostedTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Hosted-Test-Subject", out var value) ||
                !long.TryParse(value.ToString(), out var subjectId) ||
                subjectId <= 0)
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, subjectId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("sub", subjectId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("password_change_required", "true")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
