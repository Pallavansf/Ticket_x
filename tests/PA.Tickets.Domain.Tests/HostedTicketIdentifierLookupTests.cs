using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Api;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Domain.Tests;

public sealed class HostedTicketIdentifierLookupTests
{
    [Fact]
    public async Task Lookup_requires_authentication_and_returns_the_service_result_for_an_authenticated_actor()
    {
        var service = DispatchProxy.Create<ITicketLifecycleService, LookupServiceProxy>();
        var handler = (LookupServiceProxy)(object)service;
        TicketView? response = new(22, "INC-000123", TicketType.Incident, TicketStatus.New, "Printer", null, null, null, null, Array.Empty<WorkItemView>(), null, null, null, null, null);
        handler.Lookup = (identifier, actor, _) =>
        {
            handler.LastLookup = (identifier, actor);
            return Task.FromResult(response!);
        };
        using var factory = CreateFactory(service);
        using var client = factory.CreateClient();

        using var anonymous = await client.GetAsync("/api/v1/tickets/lookup/INC-000123");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(handler.LastLookup);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tickets/lookup/inc-000123");
        request.Headers.Add("X-Test-User", "4107");
        using var authorized = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        var result = await authorized.Content.ReadFromJsonAsync<TicketView>();
        Assert.Equal("INC-000123", result?.Number);
        Assert.Equal(("inc-000123", 4107L), handler.LastLookup);
    }

    [Fact]
    public async Task Lookup_errors_use_the_standard_safe_api_error_contract()
    {
        var service = DispatchProxy.Create<ITicketLifecycleService, LookupServiceProxy>();
        var handler = (LookupServiceProxy)(object)service;
        handler.Lookup = (_, _, _) => Task.FromException<TicketView>(new ArgumentException("bad identifier detail"));
        using var factory = CreateFactory(service);
        using var client = factory.CreateClient();

        using var invalidRequest = AuthenticatedRequest("/api/v1/tickets/lookup/bad-id");
        using var invalid = await client.SendAsync(invalidRequest);
        await AssertSafeError(invalid, HttpStatusCode.BadRequest, "Invalid request", "bad identifier detail");

        handler.Lookup = (_, _, _) => Task.FromException<TicketView>(new KeyNotFoundException("private ticket detail"));
        using var missingRequest = AuthenticatedRequest("/api/v1/tickets/lookup/INC-000123");
        using var missing = await client.SendAsync(missingRequest);
        await AssertSafeError(missing, HttpStatusCode.NotFound, "Not found", "private ticket detail");

        handler.Lookup = (_, _, _) => Task.FromException<TicketView>(new UnauthorizedAccessException("private scope detail"));
        using var deniedRequest = AuthenticatedRequest("/api/v1/tickets/lookup/INC-000123");
        using var denied = await client.SendAsync(deniedRequest);
        await AssertSafeError(denied, HttpStatusCode.Forbidden, "Forbidden", "private scope detail");
    }

    private static WebApplicationFactory<Program> CreateFactory(ITicketLifecycleService service) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "PostgreSQL");
            builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t62_lookup_providerless_sentinel");
            builder.UseSetting("Storage:Provider", "Local");
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("Communication:Worker:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITicketLifecycleService>();
                services.AddSingleton(service);
            });
        });

    private static HttpRequestMessage AuthenticatedRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-User", "4107");
        return request;
    }

    private static async Task AssertSafeError(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedTitle, string privateDetail)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(payload);
        Assert.Equal(expectedTitle, payload["title"].GetString());
        Assert.Equal((int)expectedStatus, payload["status"].GetInt32());
        Assert.Contains("traceId", payload);
        Assert.DoesNotContain(privateDetail, body, StringComparison.Ordinal);
    }

    public class LookupServiceProxy : DispatchProxy
    {
        public Func<string, long, CancellationToken, Task<TicketView>>? Lookup { get; set; }
        public (string Identifier, long Actor)? LastLookup { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITicketLifecycleService.LookupAsync) && args is { Length: 3 })
                return Lookup!( (string)args[0]!, (long)args[1]!, (CancellationToken)args[2]!);
            throw new NotSupportedException($"Unexpected service call: {targetMethod?.Name}");
        }
    }
}
