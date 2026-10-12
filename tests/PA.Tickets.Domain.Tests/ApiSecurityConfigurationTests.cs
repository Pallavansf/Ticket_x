using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PA.Tickets.Api;

namespace PA.Tickets.Domain.Tests;

public sealed class ApiSecurityConfigurationTests
{
    [Fact]
    public void Production_requires_explicit_issuer_authority_audience_and_restricted_https_origins()
    {
        var missing = Configuration(new Dictionary<string, string?>());
        var missingError = Assert.Throws<InvalidOperationException>(
            () => ApiSecurityConfiguration.ValidateProductionConfiguration(missing));
        Assert.Contains("Auth:Issuer", missingError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", missingError.Message, StringComparison.OrdinalIgnoreCase);

        var wildcard = Configuration(new Dictionary<string, string?>
        {
            ["Auth:Issuer"] = "https://identity.example.test/",
            ["Auth:Authority"] = "https://identity.example.test/",
            ["Auth:Audience"] = "pa-tickets",
            ["Cors:AllowedOrigins:0"] = "*"
        });
        Assert.Throws<InvalidOperationException>(() => ApiSecurityConfiguration.ValidateProductionConfiguration(wildcard));

        var insecureOrigin = Configuration(new Dictionary<string, string?>
        {
            ["Auth:Issuer"] = "https://identity.example.test/",
            ["Auth:Authority"] = "https://identity.example.test/",
            ["Auth:Audience"] = "pa-tickets",
            ["Cors:AllowedOrigins:0"] = "http://client.example.test"
        });
        Assert.Throws<InvalidOperationException>(() => ApiSecurityConfiguration.ValidateProductionConfiguration(insecureOrigin));

        var safe = Configuration(new Dictionary<string, string?>
        {
            ["Auth:Issuer"] = "https://identity.example.test/",
            ["Auth:Authority"] = "https://identity.example.test/",
            ["Auth:Audience"] = "pa-tickets",
            ["Cors:AllowedOrigins:0"] = "https://client.example.test"
        });
        ApiSecurityConfiguration.ValidateProductionConfiguration(safe);
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

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "PA.Tickets.SecurityTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
