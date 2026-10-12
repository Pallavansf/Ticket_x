using System.Text.Json;
using System.Text.Json.Serialization;
using PA.Tickets.Domain.Tickets;
namespace PA.Tickets.Application.Reporting;
public sealed record ReportQueryRequest(
    DateOnly? CreatedFrom = null, DateOnly? CreatedTo = null, DateOnly? ClosedFrom = null, DateOnly? ClosedTo = null,
    long? DepartmentId = null, long? TeamId = null, long? ClientId = null, long? ServiceId = null, long? CategoryId = null, long? SubCategoryId = null, long? RequestTypeId = null,
    TicketType? TicketType = null, long? PriorityId = null, TicketStatus? Status = null, long? RequesterId = null, long? AssigneeId = null,
    string Sort = "createdDate", bool Descending = true, int Page = 1, int PageSize = 25, string? SlaState = null)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnsupportedFilters { get; init; }
}
public sealed record ReportDefinition(string Code,string Name,string Description,IReadOnlyCollection<string> SupportedFilters,IReadOnlyCollection<string> Columns,string Permission);
public sealed record ReportCatalogue(IReadOnlyCollection<ReportDefinition> Reports);
public sealed record ReportResult(string ReportCode,string ReportName,DateTimeOffset GeneratedAt,ReportQueryRequest AppliedFilters,int Page,int PageSize,int TotalCount,IReadOnlyCollection<object> Rows);
public sealed record TicketVolumeRow(DateOnly Date,string TicketType,string Priority,string Status,int TicketCount);
public sealed record TicketStatusSummaryRow(string Status,int TicketCount);
public sealed record TicketAgingRow(string TicketNumber,string Subject,string TicketType,string Priority,string Status,DateTimeOffset CreatedAt,int AgeDays);
public sealed record AssignmentWorkloadRow(long? DepartmentId,string? Department,long? TeamId,string? Team,long? AssigneeId,string? Assignee,int AssignedOpenWorkload);
public sealed record OperationalCountRow(string Dimension,int Count);
public sealed record AgingBucketRow(string Bucket,int TicketCount);
public sealed record WaitingTicketRow(string TicketNumber,string Subject,string TicketType,string? WaitingReason,DateTimeOffset? WaitingStartedAt,int WaitingMinutes);

public interface IReportingQueryService { IReadOnlyCollection<ReportDefinition> Catalogue(); ReportDefinition GetDefinition(string code); Task<ReportResult> QueryAsync(string code,ReportQueryRequest request,long actor,string? correlationId,CancellationToken ct); }