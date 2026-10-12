using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Reporting;
namespace PA.Tickets.Domain.Tests;
public sealed class DashboardServiceTests
{
 [Fact] public void Dashboard_catalogue_contains_only_controlled_dashboards(){var s=Create(out _,out _);Assert.Equal(new[]{"operations","sla","workload"},s.Catalogue().Select(x=>x.Code));Assert.All(s.Catalogue(),x=>Assert.True(x.Enabled));}
 [Theory]
 [InlineData("operations", "OPEN_BACKLOG")]
 [InlineData("sla", "SLA_COMPLIANCE")]
 [InlineData("workload", "OPEN_BACKLOG")]
 public async Task Dashboard_queries_reuse_kpi_service(string dashboard,string expectedKpi){var service=Create(out var kpis,out var reports);var result=await service.QueryAsync(dashboard,new DashboardQueryRequest(new(KpiPeriodKind.Day,new DateOnly(2026,1,2))),9001,"dashboard-test",CancellationToken.None);Assert.Contains(result.Widgets,x=>x.Code==expectedKpi&&x.Type==DashboardWidgetType.Kpi);Assert.Contains(expectedKpi,kpis.Calls);Assert.NotEmpty(reports.Calls);}
 [Fact] public async Task Operations_composes_controlled_kpi_and_report_widgets(){var service=Create(out _,out var reports);var result=await service.QueryAsync("operations",new DashboardQueryRequest(new(KpiPeriodKind.Month,new DateOnly(2026,1,1))),9001,null,CancellationToken.None);Assert.Contains(result.Widgets,x=>x.Code=="ticketVolume"&&x.Type==DashboardWidgetType.Trend);Assert.Contains(result.Widgets,x=>x.Code=="statusDistribution"&&x.Type==DashboardWidgetType.Distribution);Assert.Contains(result.Widgets,x=>x.Code=="agingDistribution"&&x.Type==DashboardWidgetType.Aging);Assert.Contains("ticket-volume",reports.Calls);Assert.Contains("ticket-status-summary",reports.Calls);}
 [Fact] public async Task Dashboard_response_returns_period_and_applied_scope(){var service=Create(out _,out _);var request=new DashboardQueryRequest(new(KpiPeriodKind.Custom,From:new DateOnly(2026,1,1),To:new DateOnly(2026,1,8)),new DashboardScope(DepartmentId:501));var response=await service.QueryAsync("workload",request,9001,null,CancellationToken.None);Assert.Equal(KpiPeriodKind.Custom,response.Period.Kind);Assert.Equal(request,response.AppliedFilters);Assert.Equal(501,response.AppliedFilters.Scope!.DepartmentId);Assert.NotEqual(default,response.GeneratedAt);}
 [Fact] public void Unknown_dashboard_is_rejected(){var service=Create(out _,out _);Assert.Throws<KeyNotFoundException>(()=>service.GetDefinition("unknown"));}
 private static DashboardService Create(out FakeKpis kpis,out FakeReports reports){kpis=new();reports=new();return new DashboardService(kpis,reports,new AllowingAuthorization(),TimeProvider.System);}
 private sealed class AllowingAuthorization:IAuthorizationService{public Task EnsurePermissionAsync(long a,string p,AuthorizationResource? r,CancellationToken c)=>Task.CompletedTask;public Task<bool> HasPermissionAsync(long a,string p,AuthorizationResource? r,CancellationToken c)=>Task.FromResult(true);}
 private sealed class FakeKpis:IKpiService{public List<string> Calls{get;}=[];public IReadOnlyCollection<KpiDefinition> Catalogue()=>[];public KpiDefinition GetDefinition(string code)=>new(code,code,code,code,code=="OPEN_BACKLOG"?"count":"minutes",KpiDirection.LowerIsBetter,1,new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero));public Task<KpiResult> CalculateAsync(string code,KpiCalculationRequest request,long actor,string? correlationId,CancellationToken ct){Calls.Add(code);var start=new DateTimeOffset(2026,1,2,0,0,0,TimeSpan.Zero);return Task.FromResult(new KpiResult(code,1,start,start.AddDays(1),start,new KpiScope(),1,1,1,GetDefinition(code).Unit,null,null,null,KpiStatus.Unconfigured,2,1,correlationId));}}
 private sealed class FakeReports:IReportingQueryService{public List<string> Calls{get;}=[];public IReadOnlyCollection<ReportDefinition> Catalogue()=>[];public ReportDefinition GetDefinition(string code)=>new(code,code,code,[],[],"TICKET_READ");public Task<ReportResult> QueryAsync(string code,ReportQueryRequest request,long actor,string? correlationId,CancellationToken ct){Calls.Add(code);return Task.FromResult(new ReportResult(code,code,DateTimeOffset.UtcNow,request,1,25,0,[]));}}
}

