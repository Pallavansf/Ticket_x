using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Oracle.EntityFrameworkCore;
using PA.Tickets.Application.Execution;
using PA.Tickets.Application.Approvals;
using PA.Tickets.Application.Sla;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;
using PA.Tickets.Application.Workflow;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;
using PA.Tickets.Infrastructure.Tickets;

namespace PA.Tickets.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<WorkerActivityRegistry>();
        var provider = configuration["Database:Provider"];
        var postgres = string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase);
        if (!postgres && !string.Equals(provider, "Oracle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported database provider '{provider ?? "<missing>"}'. Configure PostgreSQL or Oracle.");
        var connectionString = postgres
            ? configuration.GetConnectionString("TicketsPostgres") ?? Environment.GetEnvironmentVariable("PA_TICKETS_TEST_CONNECTION_STRING")
            : configuration.GetConnectionString("TicketsOracle");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = postgres
                ? throw new InvalidOperationException("PostgreSQL integration requires TicketsPostgres or PA_TICKETS_TEST_CONNECTION_STRING.")
                : "User Id=PA_TICKETS;Password=SET_VIA_SECRET;Data Source=localhost:1521/ORCLPDB1";
        }

        services.AddDbContext<TicketsDbContext>(options =>
        {
            if (postgres) options.UseNpgsql(connectionString);
            else options.UseOracle(connectionString, x => x.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        });
        services.AddDbContext<ExecutionDbContext>(options =>
        {
            if (postgres) options.UseNpgsql(connectionString);
            else options.UseOracle(connectionString, x => x.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        });
        services.AddSingleton<IDatabaseProviderCapabilities>(_ => postgres
            ? PostgreSqlProviderCapabilities.Instance
            : OracleProviderCapabilities.Instance);
        services.AddScoped<ProviderTransactionRetry>();
        services.AddScoped<AuditChainCoordinator>();
        services.AddScoped<OperationalMetadataAuditWriter>();
        services.AddScoped<IAuthorizationService, DatabaseAuthorizationService>();
        services.AddScoped<IReportingQueryService, ReportingQueryService>();
        services.AddScoped<IKpiService, KpiService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuditorReportingService, AuditorReportingService>();
        services.AddSingleton<IReportExportFormatter, ReportExportFormatter>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IReportScheduleService, ReportScheduleService>();
        services.AddScoped<IBackgroundJobHandler, ReportScheduleJobHandler>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.AddScoped<ICatalogueAdministrationService, CatalogueAdministrationService>();
        services.AddScoped<ITicketCatalogueRuntime, TicketCatalogueRuntime>();
        services.AddScoped<ITicketCreationIdempotencyStore, TicketCreationIdempotencyStore>();

        services.AddScoped<ITicketRepository, EfTicketRepository>();
        services.AddScoped<ITicketStatusConfigurationRepository, TicketStatusConfigurationRepository>();
        services.AddScoped<ITicketPriorityConfigurationRepository, TicketPriorityConfigurationRepository>();
        if (postgres) services.AddScoped<ITicketIdentityGenerator, PostgreSqlTicketIdentityGenerator>();
        else services.AddScoped<ITicketIdentityGenerator, OracleTicketIdentityGenerator>();
        services.AddScoped<ITicketAccessAuthorizer, DatabaseTicketAccessAuthorizer>();
        services.AddScoped<IWaitingReasonCatalog, PersistentWaitingReasonCatalog>();
        services.AddScoped<ITicketRelationshipPolicy, ConfigurationTicketRelationshipPolicy>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IProblemService, ProblemService>();
        services.AddScoped<IChangeService, ChangeService>();
        services.AddScoped<IPhase5FoundationStore, Phase5FoundationStore>();
        if (!string.Equals(configuration["Storage:Provider"] ?? "Local", "Local", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported storage provider '{configuration["Storage:Provider"]}'.");
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IContentScanner, UnconfiguredContentScanner>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        if (postgres) services.AddScoped<ICommunicationStore, CommunicationStore>();
        else services.AddScoped<ICommunicationStore, OracleCommunicationStore>();
        services.AddScoped<IRecipientResolver, ExplicitRecipientResolver>();
        services.AddScoped<CommunicationTemplateRenderer>();
        var production = environment?.IsProduction() ?? false;
        services.AddScoped<ICommunicationProviderResolver>(sp =>
            new CommunicationProviderResolver(sp.GetServices<ICommunicationProvider>(),production));
        services.AddScoped<ICommunicationAdministrationRepository, CommunicationAdministrationRepository>();
        services.AddScoped<ICommunicationProvider, DeterministicCommunicationProvider>();
        services.AddScoped<ICommunicationDeliveryProcessor, CommunicationDeliveryProcessor>();
        services.AddScoped<INotificationOrchestrator, NotificationOrchestrator>();
        services.AddScoped<IWorkflowRouter, ConfigurationWorkflowRouter>();
        services.AddScoped<IWorkflowExecutionRepository, DurableWorkflowExecutionRepository>();
        services.AddScoped<IWorkflowInstanceCreationService, PersistentWorkflowInstanceCreationService>();
        services.AddScoped<IWorkflowConfigurationRepository, DurableWorkflowConfigurationRepository>();
        services.AddScoped<IWorkflowVersionSelector, PersistentWorkflowVersionSelector>();
        services.AddScoped<DurableApprovalConfigurationRepository>();
        services.AddScoped<IApprovalRuleConfigurationRepository>(sp=>sp.GetRequiredService<DurableApprovalConfigurationRepository>());
        services.AddScoped<IApprovalRuleVersionResolver>(sp=>sp.GetRequiredService<DurableApprovalConfigurationRepository>());
        services.AddScoped<IApprovalExecutionRepository, DurableApprovalExecutionRepository>();
        services.AddScoped<ISlaExecutionRepository, DurableSlaExecutionRepository>();
        services.AddScoped<ISlaPolicyResolver, PersistentSlaPolicyResolver>();
        services.AddScoped<PersistentSlaCalendarStore>();
        services.AddScoped<SlaDeadlineMonitor>();
        services.AddScoped<ISlaOrchestrationService, PersistentSlaOrchestrationService>();
        services.AddScoped<ISlaConfigurationService, PersistentSlaConfigurationService>();
        services.AddScoped<ISlaAdministrationRepository, PersistentSlaAdministrationRepository>();
        services.AddScoped<IBackgroundJobHandler, SlaEscalationJobHandler>();
        services.AddScoped<IBackgroundJobRepository, DurableBackgroundJobRepository>();
        if (configuration.GetValue<bool>("Jobs:Enabled")) services.AddHostedService<PersistentBackgroundWorker>();
        if (configuration.GetValue<bool>("Communication:Worker:Enabled")) services.AddHostedService<CommunicationBackgroundWorker>();
        return services;
    }
}



