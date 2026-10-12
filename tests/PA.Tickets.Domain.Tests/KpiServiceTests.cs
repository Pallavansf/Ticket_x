using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;

namespace PA.Tickets.Domain.Tests;

public sealed class KpiServiceTests
{
    private static KpiService Create()
    {
        var tickets = new TicketsDbContext(new DbContextOptionsBuilder<TicketsDbContext>().UseNpgsql("Host=unused").Options);
        var execution = new ExecutionDbContext(new DbContextOptionsBuilder<ExecutionDbContext>().UseNpgsql("Host=unused").Options);
        return new KpiService(tickets, execution, new AllowingAuthorization(), TimeProvider.System);
    }

    [Fact] public void Kpi_catalogue_contains_exactly_eight_controlled_definitions()
    {
        var catalogue = Create().Catalogue();
        Assert.Equal(8, catalogue.Count);
        Assert.Equal(new[] { "SLA_COMPLIANCE", "FIRST_RESPONSE_TIME", "RESOLUTION_TIME", "OPEN_BACKLOG", "AVERAGE_TICKET_AGE", "REOPEN_RATE", "APPROVAL_TURNAROUND", "WAITING_TIME" }, catalogue.Select(x => x.Code));
    }

    [Theory]
    [InlineData("SLA_COMPLIANCE", "percentage", "HigherIsBetter")]
    [InlineData("FIRST_RESPONSE_TIME", "minutes", "LowerIsBetter")]
    [InlineData("RESOLUTION_TIME", "minutes", "LowerIsBetter")]
    [InlineData("OPEN_BACKLOG", "count", "LowerIsBetter")]
    [InlineData("AVERAGE_TICKET_AGE", "minutes", "LowerIsBetter")]
    [InlineData("REOPEN_RATE", "percentage", "LowerIsBetter")]
    [InlineData("APPROVAL_TURNAROUND", "minutes", "LowerIsBetter")]
    [InlineData("WAITING_TIME", "minutes", "LowerIsBetter")]
    public void Kpi_metadata_is_controlled_and_versioned(string code, string unit, string direction)
    {
        var definition = Create().GetDefinition(code);
        Assert.Equal(1, definition.Version);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), definition.EffectiveFrom);
        Assert.Equal(unit, definition.Unit);
        Assert.Equal(direction, definition.Direction.ToString());
        Assert.Equal(new[] { KpiPeriodKind.Day, KpiPeriodKind.Week, KpiPeriodKind.Month, KpiPeriodKind.Custom }, definition.SupportedPeriods);
        Assert.NotNull(definition.ScopeDimensions);
    }

    [Fact] public void Unknown_kpi_is_rejected_as_controlled_not_found()
        => Assert.Throws<KeyNotFoundException>(() => Create().GetDefinition("NOT_A_KPI"));

    [Fact] public async Task Unknown_kpi_calculation_is_rejected_before_data_access()
        => await Assert.ThrowsAsync<KeyNotFoundException>(() => Create().CalculateAsync("NOT_A_KPI", new KpiCalculationRequest(new(KpiPeriodKind.Day, new DateOnly(2026, 1, 1))), 9001, "kpi-test", CancellationToken.None));

    [Fact] public async Task Invalid_custom_period_is_rejected_before_data_access()
        => await Assert.ThrowsAsync<ArgumentException>(() => Create().CalculateAsync("OPEN_BACKLOG", new KpiCalculationRequest(new(KpiPeriodKind.Custom, From: new DateOnly(2026, 2, 2), To: new DateOnly(2026, 2, 1))), 9001, "kpi-test", CancellationToken.None));

    [Fact] public async Task Custom_period_requires_both_boundaries()
        => await Assert.ThrowsAsync<ArgumentException>(() => Create().CalculateAsync("OPEN_BACKLOG", new KpiCalculationRequest(new(KpiPeriodKind.Custom)), 9001, "kpi-test", CancellationToken.None));

    [Theory]
    [InlineData(KpiPeriodKind.Day)]
    [InlineData(KpiPeriodKind.Week)]
    [InlineData(KpiPeriodKind.Month)]
    public void Supported_calendar_periods_are_exposed(KpiPeriodKind kind)
        => Assert.Contains(kind, Create().GetDefinition("OPEN_BACKLOG").SupportedPeriods!);

    [Fact] public void All_ratio_kpis_have_percentage_or_duration_units_and_deterministic_precision()
    {
        var catalogue = Create().Catalogue();
        Assert.All(catalogue.Where(x => x.Code is "SLA_COMPLIANCE" or "REOPEN_RATE"), x => Assert.Equal(2, x.Precision));
        Assert.All(catalogue.Where(x => x.Code is "FIRST_RESPONSE_TIME" or "RESOLUTION_TIME" or "AVERAGE_TICKET_AGE" or "APPROVAL_TURNAROUND" or "WAITING_TIME"), x => Assert.Equal(2, x.Precision));
        Assert.Equal(0, catalogue.Single(x => x.Code == "OPEN_BACKLOG").Precision);
    }

    [Fact] public void First_response_definition_does_not_claim_comment_or_communication_inference()
    {
        var definition = Create().GetDefinition("FIRST_RESPONSE_TIME");
        Assert.Contains("lifecycle event", definition.Definition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("comment", definition.Definition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("communication", definition.Definition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Scope_dimensions_include_authorization_boundaries()
    {
        var dimensions = Create().GetDefinition("OPEN_BACKLOG").ScopeDimensions!;
        Assert.Contains("client", dimensions);
        Assert.Contains("department", dimensions);
        Assert.Contains("team", dimensions);
        Assert.Contains("assignee", dimensions);
    }

    private sealed class AllowingAuthorization : IAuthorizationService
    {
        public Task EnsurePermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(long actorUserId, string permission, AuthorizationResource? resource, CancellationToken ct) => Task.FromResult(true);
    }
}
