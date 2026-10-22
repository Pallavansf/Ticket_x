using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Domain.Tests;

public sealed class ReportScheduleAuthorizationTests
{
    [Fact]
    public async Task Create_checks_report_permission_before_persisting_schedule_state()
    {
        var authorization = new DenyingAuthorization();
        var db = new ExecutionDbContext(new DbContextOptionsBuilder<ExecutionDbContext>().Options);
        var service = new ReportScheduleService(db, new TestReports(), authorization, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(
            9001,
            "test",
            ReportExportFormat.Csv,
            new ReportQueryRequest(),
            new ReportScheduleRecurrence(ReportScheduleFrequency.Daily, 9, 0),
            "UTC",
            CancellationToken.None));

        Assert.Equal((9001, "TICKET_READ", (AuthorizationResource?)null), authorization.Requested);
    }

    private sealed class TestReports : IReportingQueryService
    {
        public IReadOnlyCollection<ReportDefinition> Catalogue() => [Definition];

        public ReportDefinition GetDefinition(string code) =>
            string.Equals(code, Definition.Code, StringComparison.OrdinalIgnoreCase)
                ? Definition
                : throw new KeyNotFoundException("The requested report was not found.");

        public Task<ReportResult> QueryAsync(string code, ReportQueryRequest request, long actor, string? correlationId, CancellationToken ct) =>
            throw new NotSupportedException();

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
}
