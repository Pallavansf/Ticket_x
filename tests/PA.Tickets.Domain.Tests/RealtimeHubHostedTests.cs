using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PA.Tickets.Api;
using PA.Tickets.Application.Foundations;

namespace PA.Tickets.Domain.Tests;

public sealed class RealtimeHubHostedTests
{
    [Fact]
    public void Realtime_publisher_is_resolved_per_scope_without_database_access()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "PostgreSQL");
            builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=hub_test_sentinel;Username=hub_test;Password=hub_test");
            builder.UseSetting("Storage:Provider", "Local");
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("Communication:Worker:Enabled", "false");
        });
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IRealtimePublisher>();
        var second = secondScope.ServiceProvider.GetRequiredService<IRealtimePublisher>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Negotiation_requires_authentication_and_never_needs_a_database()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "PostgreSQL");
            builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=hub_test_sentinel;Username=hub_test;Password=hub_test");
            builder.UseSetting("Storage:Provider", "Local");
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("Communication:Worker:Enabled", "false");
        });
        using var client = factory.CreateClient();

        using var anonymous = await client.PostAsync("/hubs/updates/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/updates/negotiate?negotiateVersion=1");
        request.Headers.Add("X-Test-User", "5101");
        using var authenticated = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        using var document = await JsonDocument.ParseAsync(await authenticated.Content.ReadAsStreamAsync());
        var response = document.RootElement;
        Assert.True(response.TryGetProperty("connectionId", out _) || response.TryGetProperty("connectionToken", out _));
        Assert.True(response.GetProperty("availableTransports").GetArrayLength() > 0);
    }
}
