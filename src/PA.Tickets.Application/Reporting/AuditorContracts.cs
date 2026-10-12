namespace PA.Tickets.Application.Reporting;
public sealed record AuditorQueryRequest(DateOnly? From=null,DateOnly? To=null,long? TicketId=null,long? ActorId=null,string? EntityType=null,string? Action=null,string? CorrelationId=null,string Sort="timestamp",bool Descending=true,int Page=1,int PageSize=25);
public sealed record AuditorReportDefinition(string Code,string Name,string Description,IReadOnlyCollection<string> SupportedFilters,IReadOnlyCollection<string> Columns,string Permission);
public sealed record AuditorRow(long Id,DateTimeOffset Timestamp,long? TicketId,long? ActorId,string? Action,string? EntityType,string? EntityId,string? CorrelationId,string? OldValue,string? NewValue,string? Reason,string? Hash,string? PreviousHash,object? Metadata=null);
public sealed record AuditorReportResult(string ReportCode,string ReportName,DateTimeOffset GeneratedAt,AuditorQueryRequest AppliedFilters,int Page,int PageSize,int TotalCount,IReadOnlyCollection<AuditorRow> Rows);
public sealed record AuditorCatalogue(IReadOnlyCollection<AuditorReportDefinition> Reports);
public sealed record AuditIntegrityResult(long TicketId, bool IsValid, int RecordsVerified, long? FailedAuditId, string? Failure, DateTimeOffset VerifiedAtUtc);
public sealed record AuditStreamIntegrityResult(string EntityType, string EntityId, bool IsValid, int RecordsVerified, long? FailedAuditId, string? Failure, DateTimeOffset VerifiedAtUtc);
public interface IAuditorReportingService
{
    IReadOnlyCollection<AuditorReportDefinition> Catalogue();
    AuditorReportDefinition GetDefinition(string code);
    Task<AuditorReportResult> QueryAsync(string code, AuditorQueryRequest request, long actor, string? correlationId, CancellationToken ct);
    Task<AuditIntegrityResult> VerifyTicketAuditAsync(long ticketId, long actor, CancellationToken ct);
    Task<AuditStreamIntegrityResult> VerifyOperationalMetadataAuditAsync(string entityType, string entityId, long actor, CancellationToken ct);
}
