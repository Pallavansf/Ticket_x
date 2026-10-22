using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace PA.Tickets.Domain.Tests;

public sealed class ReportExportAuthorizationTests
{
    [Fact]
    public async Task Create_checks_report_permission_before_persisting_export_state()
    {
        var reports = new DenyingReports();
        var authorization = new DenyingAuthorization();
        var db = new ExecutionDbContext(new DbContextOptionsBuilder<ExecutionDbContext>().Options);
        var service = new ReportExportService(db, reports, new ReportExportFormatter(), new UnusedStorage(), TimeProvider.System, authorization);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(
            "test",
            new ReportExportRequest(ReportExportFormat.Csv, new ReportQueryRequest()),
            9001,
            null,
            CancellationToken.None));

        Assert.Equal((9001, "TICKET_READ", (AuthorizationResource?)null), authorization.Requested);
        Assert.False(reports.QueryCalled);
    }

    [Fact]
    public async Task Report_query_keeps_its_own_permission_check_before_ticket_query()
    {
        var authorization = new DenyingAuthorization();
        var tickets = new PA.Tickets.Infrastructure.Persistence.TicketsDbContext(
            new DbContextOptionsBuilder<PA.Tickets.Infrastructure.Persistence.TicketsDbContext>().Options);
        var executions = new ExecutionDbContext(new DbContextOptionsBuilder<ExecutionDbContext>().Options);
        var service = new ReportingQueryService(tickets, executions, authorization, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.QueryAsync(
            "ticket-volume",
            new ReportQueryRequest(),
            9001,
            null,
            CancellationToken.None));

        Assert.Equal((9001, "TICKET_READ", (AuthorizationResource?)null), authorization.Requested);
    }

    [Fact]
    public async Task Completed_export_download_reads_storage_after_authorization_and_verifies_hash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ExecutionDbContext>().UseSqlite(connection).Options;
        await using var db = new ExecutionDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var bytes = System.Text.Encoding.UTF8.GetBytes("name\nvalue\n");
        db.ReportExports.Add(new ReportExportRow
        {
            Id = 42, ReportCode = "ticket-volume", Format = "CSV", RequestedBy = 9001,
            Status = "COMPLETED", CreatedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), StorageKey = "exports/42/file.csv",
            FileHash = Convert.ToHexString(SHA256.HashData(bytes)), ContentType = "text/csv", FileSize = bytes.Length, RowCount = 1
        });
        await db.SaveChangesAsync();
        var storage = new DownloadStorage(bytes);
        var authorization = new AllowingAuthorization();
        var service = new ReportExportService(db, new AllowingReports(), new ReportExportFormatter(), storage, TimeProvider.System, authorization);

        var download = await service.DownloadAsync(42, 9001, CancellationToken.None);
        await using var stream = download.Content;
        using var reader = new StreamReader(stream);
        Assert.Equal("name\nvalue\n", await reader.ReadToEndAsync());
        Assert.Equal("ticket-volume.csv", download.FileName);
        Assert.Equal("exports/42/file.csv", storage.RequestedKey);
        Assert.Equal((9001L, "TICKET_READ"), authorization.Requested);
    }

    private sealed class DenyingReports : IReportingQueryService
    {
        public bool QueryCalled { get; private set; }

        public IReadOnlyCollection<ReportDefinition> Catalogue() => [Definition];

        public ReportDefinition GetDefinition(string code) => Definition;

        public Task<ReportResult> QueryAsync(string code, ReportQueryRequest request, long actor, string? correlationId, CancellationToken ct)
        {
            QueryCalled = true;
            throw new InvalidOperationException("Query should not be reached after a denied permission check.");
        }

        private static ReportDefinition Definition => new("test", "Test", "", [], [], "TICKET_READ");
    }

    private sealed class DenyingAuthorization : IAuthorizationService
    {
        public (long Actor, string Permission, AuthorizationResource? Resource)? Requested { get; private set; }

        public Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct)
        {
            Requested = (actorUserId, permission, resource);
            throw new UnauthorizedAccessException("Report permission denied.");
        }

        public Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) =>
            Task.FromResult(false);
    }

    private sealed class UnusedStorage : IFileStorage
    {
        public Task<StoredObject> PutAsync(string objectKey, string fileName, string? contentType, Stream content, long maxFileSizeBytes, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string objectKey, CancellationToken ct) => throw new NotSupportedException();

        public Task DeleteAsync(string objectKey, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class DownloadStorage(byte[] bytes) : IFileStorage
    {
        public string? RequestedKey { get; private set; }
        public Task<StoredObject> PutAsync(string objectKey, string fileName, string? contentType, Stream content, long maxFileSizeBytes, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct) { RequestedKey = objectKey; return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false)); }
        public Task<bool> ExistsAsync(string objectKey, CancellationToken ct) => Task.FromResult(true);
        public Task DeleteAsync(string objectKey, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class AllowingReports : IReportingQueryService
    {
        public IReadOnlyCollection<ReportDefinition> Catalogue() => [new("ticket-volume", "Ticket Volume", "", [], ["name"], "TICKET_READ")];
        public ReportDefinition GetDefinition(string code) => Catalogue().Single();
        public Task<ReportResult> QueryAsync(string code, ReportQueryRequest request, long actor, string? correlationId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class AllowingAuthorization : IAuthorizationService
    {
        public (long Actor, string Permission)? Requested { get; private set; }
        public Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) { Requested = (actorUserId, permission); return Task.CompletedTask; }
        public Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) => Task.FromResult(true);
    }
}
