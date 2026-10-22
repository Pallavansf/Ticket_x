using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Api;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Tickets;

namespace PA.Tickets.Domain.Tests;

public sealed class HostedAttachmentDownloadAuthorizationTests
{
    [Fact]
    public async Task Out_of_scope_attachment_download_is_denied_before_version_or_storage_read()
    {
        var reader = new FakeDownloadReader();
        var storage = new RecordingStorage();
        using var factory = CreateFactory(reader, new FactsReader(scopeDepartmentId: 20), storage);
        using var client = factory.CreateClient();
        using var request = Request();

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, reader.OwnerCalls);
        Assert.Equal(0, reader.VersionCalls);
        Assert.Equal(0, storage.OpenReadCalls);
    }

    [Fact]
    public async Task In_scope_attachment_download_returns_checksum_verified_content()
    {
        var reader = new FakeDownloadReader();
        var storage = new RecordingStorage();
        using var factory = CreateFactory(reader, new FactsReader(scopeDepartmentId: 10), storage);
        using var client = factory.CreateClient();
        using var request = Request();

        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("safe attachment", content);
        Assert.Equal(1, reader.OwnerCalls);
        Assert.Equal(1, reader.VersionCalls);
        Assert.Equal(1, storage.OpenReadCalls);
    }

    [Fact]
    public async Task Mismatched_version_metadata_fails_closed_without_storage_read()
    {
        var reader = new FakeDownloadReader { MismatchedVersionAttachmentId = 9999 };
        var storage = new RecordingStorage();
        using var factory = CreateFactory(reader, new FactsReader(scopeDepartmentId: 10), storage);
        using var client = factory.CreateClient();
        using var request = Request();

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, reader.VersionCalls);
        Assert.Equal(0, storage.OpenReadCalls);
    }

    [Fact]
    public async Task Missing_authorization_actor_fails_closed_before_version_or_storage_read()
    {
        var reader = new FakeDownloadReader();
        var storage = new RecordingStorage();
        using var factory = CreateFactory(reader, new FactsReader(scopeDepartmentId: 10, missingActor: true), storage);
        using var client = factory.CreateClient();
        using var request = Request();

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, reader.OwnerCalls);
        Assert.Equal(0, reader.VersionCalls);
        Assert.Equal(0, storage.OpenReadCalls);
    }

    private static HttpRequestMessage Request()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/attachments/7001/versions/1/download");
        request.Headers.Add("X-Test-User", "4107");
        request.Headers.Add("X-Test-Role", "User");
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeDownloadReader reader, FactsReader facts, RecordingStorage storage) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "PostgreSQL");
            builder.UseSetting("ConnectionStrings:TicketsPostgres", "Host=127.0.0.1;Port=1;Database=t57_providerless_sentinel;Username=t57;Password=t57");
            builder.UseSetting("Storage:Provider", "Local");
            builder.UseSetting("Jobs:Enabled", "false");
            builder.UseSetting("Communication:Worker:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAttachmentDownloadReader>();
                services.AddSingleton<IAttachmentDownloadReader>(reader);
                services.RemoveAll<IAuthorizationFactsReader>();
                services.AddSingleton<IAuthorizationFactsReader>(facts);
                services.RemoveAll<ISpectatorMembershipReader>();
                services.AddSingleton<ISpectatorMembershipReader, NonSpectatorFacts>();
                services.RemoveAll<IFileStorage>();
                services.AddSingleton<IFileStorage>(storage);
            });
        });

    private sealed class NonSpectatorFacts : ISpectatorMembershipReader
    {
        public Task<bool> IsActiveSpectatorAsync(long userId, DateTimeOffset at, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FactsReader(long? scopeDepartmentId, bool missingActor = false) : IAuthorizationFactsReader
    {
        public Task<AuthorizationFacts> ReadAsync(long actorUserId, string permission, DateTimeOffset at, CancellationToken cancellationToken) =>
            Task.FromResult(new AuthorizationFacts(
                missingActor ? null : new AuthorizationActorFact(actorUserId, 10, null, true),
                new AuthorizationPermissionFact(91, permission, "TICKETS", "READ", "DEPARTMENT", "READ_ONLY", true, true),
                [new AuthorizationRoleFact(12, RoleAuthorizationPolicy.User, true)],
                [new AuthorizationGrantFact(12, RoleAuthorizationPolicy.User, true, true, EffectiveGrantAuthorization.Active, at.AddMinutes(-1), null, permission, "READ_ONLY", "DEPARTMENT", true)],
                [new AuthorizationScopeFact(21, 12, "DEPARTMENT", null, scopeDepartmentId, null, null, null, null, at.AddMinutes(-1), null, true)]));
    }

    private sealed class FakeDownloadReader : IAttachmentDownloadReader
    {
        public int OwnerCalls { get; private set; }
        public int VersionCalls { get; private set; }
        public long? MismatchedVersionAttachmentId { get; init; }

        public Task<AttachmentDownloadOwnerFact> ReadOwnerAsync(long attachmentId, CancellationToken ct)
        {
            OwnerCalls++;
            return Task.FromResult(new AttachmentDownloadOwnerFact(attachmentId, 8001, Ticket()));
        }

        public Task<AttachmentDownloadVersionFact> ReadVersionAsync(long attachmentId, int version, CancellationToken ct)
        {
            VersionCalls++;
            var bytes = "safe attachment"u8.ToArray();
            return Task.FromResult(new AttachmentDownloadVersionFact(
                9001,
                MismatchedVersionAttachmentId ?? attachmentId,
                version,
                "safe.txt",
                "text/plain",
                bytes.Length,
                "TEST",
                "attachments/7001/1",
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                "CURRENT",
                null,
                DateTimeOffset.UtcNow,
                4107));
        }

        private static Ticket Ticket() => PA.Tickets.Domain.Tickets.Ticket.Restore(
            8001, "REQ-8001", TicketType.Request, TicketStatus.New, 4107, 4107, "Protected ticket", null,
            null, null, null, null, 10, null, null, DateTimeOffset.UtcNow, null, null, false, false, 0, null, []);
    }

    private sealed class RecordingStorage : IFileStorage
    {
        public int OpenReadCalls { get; private set; }
        public Task<StoredObject> PutAsync(string objectKey, string fileName, string? contentType, Stream content, long maxFileSizeBytes, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct)
        {
            OpenReadCalls++;
            return Task.FromResult<Stream>(new MemoryStream("safe attachment"u8.ToArray(), writable: false));
        }
        public Task<bool> ExistsAsync(string objectKey, CancellationToken ct) => Task.FromResult(true);
        public Task DeleteAsync(string objectKey, CancellationToken ct) => Task.CompletedTask;
    }
}
