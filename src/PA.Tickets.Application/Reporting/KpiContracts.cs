namespace PA.Tickets.Application.Reporting;
public enum KpiPeriodKind { Day, Week, Month, Custom }
public enum KpiDirection { HigherIsBetter, LowerIsBetter, TargetBand }
public enum KpiStatus { OnTarget, Warning, Critical, NotApplicable, Unconfigured }
public sealed record KpiScope(long? ClientId=null,long? DepartmentId=null,long? TeamId=null,long? ServiceId=null,long? CategoryId=null,long? SubCategoryId=null,long? RequestTypeId=null,int? TicketType=null,long? PriorityId=null,long? RequesterId=null,long? AssigneeId=null);
public sealed record KpiPeriodRequest(KpiPeriodKind Kind,DateOnly? Date=null,DateOnly? From=null,DateOnly? To=null);
public sealed record KpiDefinition(string Code,string Name,string Description,string Definition,string Unit,KpiDirection Direction,int Version,DateTimeOffset EffectiveFrom,decimal? Target=null,decimal? WarningThreshold=null,decimal? CriticalThreshold=null,decimal? TargetLower=null,decimal? TargetUpper=null,int Precision=2,IReadOnlyCollection<KpiPeriodKind>? SupportedPeriods=null,IReadOnlyCollection<string>? ScopeDimensions=null);
public sealed record KpiCalculationRequest(KpiPeriodRequest Period,KpiScope? Scope=null);
public sealed record KpiResult(string KpiCode,int DefinitionVersion,DateTimeOffset MeasurementPeriodStart,DateTimeOffset MeasurementPeriodEnd,DateTimeOffset CalculatedAt,KpiScope Scope,decimal? Numerator,decimal? Denominator,decimal? Value,string Unit,decimal? Target,decimal? WarningThreshold,decimal? CriticalThreshold,KpiStatus Status,int Precision,int? PopulationCount=null,string? CorrelationId=null);
public sealed record KpiCatalogue(IReadOnlyCollection<KpiDefinition> Kpis);
public interface IKpiService { IReadOnlyCollection<KpiDefinition> Catalogue(); KpiDefinition GetDefinition(string code); Task<KpiResult> CalculateAsync(string code,KpiCalculationRequest request,long actor,string? correlationId,CancellationToken ct); }
