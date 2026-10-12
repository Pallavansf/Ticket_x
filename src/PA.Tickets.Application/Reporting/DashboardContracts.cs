namespace PA.Tickets.Application.Reporting;
public enum DashboardWidgetType { Kpi, Number, Trend, Distribution, Table, Workload, Aging }
public sealed record DashboardScope(long? ClientId=null,long? DepartmentId=null,long? TeamId=null,long? ServiceId=null,long? CategoryId=null,long? SubCategoryId=null,long? RequestTypeId=null,int? TicketType=null,long? PriorityId=null,long? RequesterId=null,long? AssigneeId=null);
public sealed record DashboardQueryRequest(KpiPeriodRequest Period,DashboardScope? Scope=null);
public sealed record DashboardWidgetDefinition(string Code,string Title,DashboardWidgetType Type,string Source,string Unit=null!);
public sealed record DashboardDefinition(string Code,string Name,string Description,int Version,bool Enabled,IReadOnlyCollection<KpiPeriodKind> SupportedPeriods,IReadOnlyCollection<string> SupportedFilters,IReadOnlyCollection<DashboardWidgetDefinition> Widgets,string Permission);
public sealed record DashboardPeriod(DateTimeOffset Start,DateTimeOffset End,KpiPeriodKind Kind);
public sealed record DashboardWidget(string Code,string Title,DashboardWidgetType Type,string? Unit,object? Data,KpiStatus? Status=null,string? Error=null);
public sealed record DashboardResponse(string DashboardCode,string DashboardName,DateTimeOffset GeneratedAt,DashboardPeriod Period,DashboardQueryRequest AppliedFilters,IReadOnlyCollection<DashboardWidget> Widgets);
public sealed record DashboardCatalogue(IReadOnlyCollection<DashboardDefinition> Dashboards);
public interface IDashboardService { IReadOnlyCollection<DashboardDefinition> Catalogue(); DashboardDefinition GetDefinition(string code); Task<DashboardResponse> QueryAsync(string code,DashboardQueryRequest request,long actor,string? correlationId,CancellationToken ct); }


