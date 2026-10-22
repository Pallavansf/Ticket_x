using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PA.Tickets.Api;
using PA.Tickets.Api.Controllers;

namespace PA.Tickets.Domain.Tests;

public sealed class ApiSecurityConfigurationTests
{
    [Fact]
    public void Only_login_is_anonymous_and_password_change_requires_authorization()
    {
        var controller = typeof(AuthenticationController);
        var login = controller.GetMethod(nameof(AuthenticationController.Login))!;
        var changePassword = controller.GetMethod(nameof(AuthenticationController.ChangePassword))!;

        Assert.False(controller.IsDefined(typeof(AllowAnonymousAttribute), inherit: true));
        Assert.True(login.IsDefined(typeof(AllowAnonymousAttribute), inherit: true));
        Assert.True(changePassword.IsDefined(typeof(AuthorizeAttribute), inherit: true));
    }

    [Theory]
    [InlineData("GET", "/api/v1/tickets")]
    [InlineData("POST", "/api/v1/auth/login/")]
    [InlineData("PUT", "/api/v1/auth/change-password")]
    [InlineData("POST", "/api/v1/admin/users")]
    public async Task Password_rotation_requirement_blocks_other_authenticated_requests(string method, string path)
    {
        var context = PasswordRotationContext(method, path, authenticated: true, requiresRotation: true);
        var nextCalled = false;
        var middleware = new PasswordRotationRequiredMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("Password change is required", await ReadResponseAsync(context), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/login", true, true)]
    [InlineData("POST", "/API/V1/AUTH/CHANGE-PASSWORD", true, true)]
    [InlineData("GET", "/api/v1/tickets", false, false)]
    [InlineData("GET", "/api/v1/tickets", false, true)]
    [InlineData("GET", "/api/v1/tickets", true, false)]
    public async Task Password_rotation_requirement_allows_exempt_paths_and_requests_without_gate_conditions(
        string method,
        string path,
        bool authenticated,
        bool requiresRotation)
    {
        var context = PasswordRotationContext(
            method,
            path,
            authenticated,
            requiresRotation);
        var nextCalled = false;
        var middleware = new PasswordRotationRequiredMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public void Production_requires_local_authentication_and_restricted_https_origins_with_optional_oidc()
    {
        var missing = Configuration(new Dictionary<string, string?>());
        var missingError = Assert.Throws<InvalidOperationException>(
            () => ApiSecurityConfiguration.ValidateProductionConfiguration(missing));
        Assert.Contains("Auth:Audience", missingError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", missingError.Message, StringComparison.OrdinalIgnoreCase);

        var wildcard = Configuration(new Dictionary<string, string?>
        {
            ["Auth:Local:Issuer"] = "pa-tickets-local",
            ["Auth:Local:SigningKey"] = "local-signing-key-at-least-32-bytes",
            ["Auth:Local:TokenLifetimeMinutes"] = "30",
            ["Auth:Audience"] = "pa-tickets",
            ["Auth:Issuer"] = "https://identity.example.test/",
            ["Auth:Authority"] = "https://identity.example.test/",
            ["Cors:AllowedOrigins:0"] = "*"
        });
        Assert.Throws<InvalidOperationException>(() => ApiSecurityConfiguration.ValidateProductionConfiguration(wildcard));

        var insecureOrigin = Configuration(new Dictionary<string, string?>
        {
            ["Auth:Local:Issuer"] = "pa-tickets-local",
            ["Auth:Local:SigningKey"] = "local-signing-key-at-least-32-bytes",
            ["Auth:Local:TokenLifetimeMinutes"] = "30",
            ["Auth:Audience"] = "pa-tickets",
            ["Auth:Issuer"] = "https://identity.example.test/",
            ["Auth:Authority"] = "https://identity.example.test/",
            ["Cors:AllowedOrigins:0"] = "http://client.example.test"
        });
        Assert.Throws<InvalidOperationException>(() => ApiSecurityConfiguration.ValidateProductionConfiguration(insecureOrigin));

        var safe = Configuration(new Dictionary<string, string?>
        {
            ["Auth:Local:Issuer"] = "pa-tickets-local",
            ["Auth:Local:SigningKey"] = "local-signing-key-at-least-32-bytes",
            ["Auth:Local:TokenLifetimeMinutes"] = "30",
            ["Auth:Audience"] = "pa-tickets",
            ["Auth:Issuer"] = "https://identity.example.test/",
            ["Auth:Authority"] = "https://identity.example.test/",
            ["Cors:AllowedOrigins:0"] = "https://client.example.test"
        });
        ApiSecurityConfiguration.ValidateProductionConfiguration(safe);

        var withoutOidc = Configuration(new Dictionary<string, string?>
        {
            ["Auth:Local:Issuer"] = "pa-tickets-local",
            ["Auth:Local:SigningKey"] = "local-signing-key-at-least-32-bytes",
            ["Auth:Local:TokenLifetimeMinutes"] = "30",
            ["Auth:Audience"] = "pa-tickets",
            ["Cors:AllowedOrigins:0"] = "https://client.example.test"
        });
        ApiSecurityConfiguration.ValidateProductionConfiguration(withoutOidc);
    }

    [Fact]
    public void Test_signing_configuration_is_rejected_outside_testing_environment()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Auth:TestSigningKey"] = "test-only-signing-key-never-for-production"
        });
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApiSecurityConfiguration.AddAuthenticationAndCors(
                services,
                configuration,
                new TestHostEnvironment(Environments.Production)));
        Assert.DoesNotContain("test-only-signing-key-never-for-production", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_cors_defaults_to_localhost_but_production_requires_explicit_origins()
    {
        var configuration = Configuration(new Dictionary<string, string?>());
        Assert.Equal(
            ["http://localhost:4200"],
            ApiSecurityConfiguration.GetAllowedOrigins(configuration, new TestHostEnvironment(Environments.Development)));
        Assert.Throws<InvalidOperationException>(
            () => ApiSecurityConfiguration.GetAllowedOrigins(configuration, new TestHostEnvironment(Environments.Production)));
    }

    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static DefaultHttpContext PasswordRotationContext(
        string method,
        string path,
        bool authenticated,
        bool requiresRotation)
    {
        var claims = requiresRotation
            ? new[] { new Claim("password_change_required", "true") }
            : [];
        var identity = new ClaimsIdentity(claims, authenticated ? "test" : null);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "PA.Tickets.SecurityTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
