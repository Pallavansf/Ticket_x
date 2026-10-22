using System.Net;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace PA.Tickets.Api;

public static class ApiSecurityConfiguration
{
    public static void AddAuthenticationAndCors(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var testSigningKey = configuration["Auth:TestSigningKey"];
        if (!string.IsNullOrWhiteSpace(testSigningKey) && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException("Test-only authentication configuration is not permitted in this environment.");
        }

        if (environment.IsProduction())
        {
            ValidateProductionConfiguration(configuration);
            if (!string.IsNullOrWhiteSpace(configuration["Auth:Authority"]))
                services.AddHostedService<OidcJwtConfigurationStartupValidator>();
        }

        if (environment.IsEnvironment("Testing") &&
            string.IsNullOrWhiteSpace(testSigningKey) &&
            string.IsNullOrWhiteSpace(configuration["Auth:Local:SigningKey"]))
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestingAuthenticationHandler>("Test", _ => { });
        }
        else if (!string.IsNullOrWhiteSpace(configuration["Auth:Local:SigningKey"]))
        {
            var localIssuer = configuration["Auth:Local:Issuer"];
            var audience = configuration["Auth:Audience"];
            var localSigningKey = configuration["Auth:Local:SigningKey"]!;
            ValidateLocalJwtConfiguration(localIssuer, audience, localSigningKey, configuration.GetValue<int?>("Auth:Local:TokenLifetimeMinutes") ?? 30);
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddPolicyScheme(JwtBearerDefaults.AuthenticationScheme, JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.ForwardDefaultSelector = context => SelectBearerScheme(context, localIssuer!);
                })
                .AddJwtBearer("LocalBearer", options =>
                {
                    options.RequireHttpsMetadata = !environment.IsDevelopment();
                    options.MapInboundClaims = true;
                    options.IncludeErrorDetails = false;
                    ConfigureLocalJwt(options, localIssuer!, audience!, localSigningKey);
                })
                .AddJwtBearer("OidcBearer", options =>
                {
                    options.RequireHttpsMetadata = !environment.IsDevelopment();
                    options.MapInboundClaims = true;
                    options.IncludeErrorDetails = false;
                    ConfigureOidcJwt(options, configuration);
                });
        }
        else
        {
            if (configuration.GetValue<bool>("Auth:Enabled") && !environment.IsEnvironment("Testing"))
                throw new InvalidOperationException("Enabled local authentication requires Auth:Local:SigningKey from protected configuration.");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = !environment.IsDevelopment();
                    options.MapInboundClaims = true;
                    options.IncludeErrorDetails = false;
                    if (!string.IsNullOrWhiteSpace(testSigningKey))
                    {
                        ConfigureTestJwt(options, configuration, testSigningKey);
                    }
                    else
                    {
                        ConfigureOidcJwt(options, configuration);
                    }
                });
        }

        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many sign-in attempts. Try again later."
                    },
                    cancellationToken);
            };
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetLoginRateLimitPartition(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
        services.AddCors(options =>
        {
            options.AddPolicy("Client", policy =>
            {
                policy.WithOrigins(GetAllowedOrigins(configuration, environment))
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }

    internal static string GetLoginRateLimitPartition(HttpContext context) =>
        context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown-client";

    public static void UseTrustedForwardedHeaders(
        IApplicationBuilder app,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            return;
        }

        if (!environment.IsProduction())
        {
            throw new InvalidOperationException("Forwarded headers can only be enabled in production.");
        }

        var proxyAddresses = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        if (proxyAddresses.Length == 0 || proxyAddresses.Any(x => !IPAddress.TryParse(x, out _)))
        {
            throw new InvalidOperationException("Forwarded headers require one or more valid ForwardedHeaders:KnownProxies addresses.");
        }

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        foreach (var address in proxyAddresses)
        {
            options.KnownProxies.Add(IPAddress.Parse(address));
        }

        app.UseForwardedHeaders(options);
    }

    public static string[] GetAllowedOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (environment.IsProduction())
        {
            if (configured is not { Length: > 0 } || configured.Any(x => !IsAllowedProductionOrigin(x)))
            {
                throw new InvalidOperationException("Production requires explicit HTTPS origins in Cors:AllowedOrigins.");
            }
            return configured;
        }

        return configured is { Length: > 0 } ? configured : ["http://localhost:4200"];
    }

    private static void ConfigureTestJwt(JwtBearerOptions options, IConfiguration configuration, string signingKey)
    {
        var issuer = configuration["Auth:Issuer"];
        var audience = configuration["Auth:Audience"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Test JWT authentication requires test issuer and audience configuration.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("The test JWT signing key must contain at least 32 bytes.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        RequireSubjectClaim(options);
    }

    private static void ConfigureLocalJwt(JwtBearerOptions options, string issuer, string audience, string signingKey)
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        RequireSubjectClaim(options);
    }

    private static void ConfigureOidcJwt(JwtBearerOptions options, IConfiguration configuration)
    {
        options.Authority = configuration["Auth:Authority"];
        options.Audience = configuration["Auth:Audience"];
        var issuer = configuration["Auth:Issuer"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = configuration["Auth:Audience"],
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms =
            [
                SecurityAlgorithms.RsaSha256,
                SecurityAlgorithms.RsaSsaPssSha256,
                SecurityAlgorithms.EcdsaSha256
            ]
        };
        RequireSubjectClaim(options);
    }

    private static void RequireSubjectClaim(JwtBearerOptions options)
    {
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var subject = context.Principal?.FindFirst("sub")?.Value
                    ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var actorId) || actorId <= 0)
                {
                    context.Fail("The bearer token is missing a valid subject claim.");
                }
                return Task.CompletedTask;
            }
        };
    }

    public static void ValidateProductionConfiguration(IConfiguration configuration)
    {
        var issuer = configuration["Auth:Issuer"];
        var authority = configuration["Auth:Authority"];
        var audience = configuration["Auth:Audience"];
        var localIssuer = configuration["Auth:Local:Issuer"];
        var localSigningKey = configuration["Auth:Local:SigningKey"];
        var tokenLifetime = configuration.GetValue<int?>("Auth:Local:TokenLifetimeMinutes") ?? 30;
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Production local authentication requires a non-empty Auth:Audience.");
        ValidateLocalJwtConfiguration(localIssuer, audience, localSigningKey, tokenLifetime);
        var oidcConfigured = !string.IsNullOrWhiteSpace(issuer) || !string.IsNullOrWhiteSpace(authority);
        if (oidcConfigured && (!IsHttpsUri(issuer) || !IsHttpsUri(authority)))
        {
            throw new InvalidOperationException(
                "Optional Production OIDC authentication requires HTTPS Auth:Issuer and Auth:Authority values.");
        }

        _ = GetAllowedOrigins(configuration, new ProductionEnvironment());
    }

    private static void ValidateLocalJwtConfiguration(string? issuer, string? audience, string? signingKey, int tokenLifetimeMinutes)
    {
        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(signingKey) ||
            Encoding.UTF8.GetByteCount(signingKey) < 32 ||
            tokenLifetimeMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException("Local JWT authentication requires Auth:Local:Issuer, Auth:Audience, a protected Auth:Local:SigningKey of at least 32 bytes, and Auth:Local:TokenLifetimeMinutes between 1 and 1440.");
        }
    }

    private static string SelectBearerScheme(HttpContext context, string localIssuer)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "LocalBearer";

        try
        {
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ReadJwtToken(authorization["Bearer ".Length..]);
            return string.Equals(token.Issuer, localIssuer, StringComparison.Ordinal) ? "LocalBearer" : "OidcBearer";
        }
        catch (SecurityTokenException)
        {
            return "LocalBearer";
        }
        catch (ArgumentException)
        {
            return "LocalBearer";
        }
    }

    private static bool IsAllowedProductionOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            value.Contains('*'))
        {
            return false;
        }

        return true;
    }

    private static bool IsHttpsUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo);

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = typeof(ApiSecurityConfiguration).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

public sealed class OidcJwtConfigurationStartupValidator(
    Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions> optionsMonitor)
    : Microsoft.Extensions.Hosting.IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get("OidcBearer");
        var manager = options.ConfigurationManager
            ?? throw new InvalidOperationException("Production JWT signing metadata is not configured.");
        var configuration = await manager.GetConfigurationAsync(cancellationToken);
        if (configuration.SigningKeys.Count == 0 ||
            !string.Equals(configuration.Issuer, options.TokenValidationParameters.ValidIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The configured OIDC authority did not provide the expected issuer and signing keys.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
