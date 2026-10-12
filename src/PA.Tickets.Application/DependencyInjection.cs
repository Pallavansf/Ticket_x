using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Tickets;

namespace PA.Tickets.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.TryAddSingleton(TimeProvider.System);
        s.AddScoped<ITicketLifecycleService, TicketLifecycleService>();
        s.AddScoped<ICommunicationService, CommunicationService>();
        s.AddScoped<Workflow.IWorkflowDefinitionService, Workflow.WorkflowDefinitionService>();
        s.AddScoped<Workflow.IWorkflowExecutionBoundary, Workflow.WorkflowExecutionBoundary>();
        s.AddScoped<Sla.ISlaExecutionEngine, Sla.SlaExecutionEngine>();
        return s;
    }
}


