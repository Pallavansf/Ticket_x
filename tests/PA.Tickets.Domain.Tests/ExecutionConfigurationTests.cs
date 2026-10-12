using Microsoft.Extensions.Configuration;
using PA.Tickets.Application.Execution;
using PA.Tickets.Infrastructure.Execution;

namespace PA.Tickets.Domain.Tests;

public sealed class ExecutionConfigurationTests
{
    [Fact]
    public void Routing_uses_specific_step_rule_over_ticket_type_rule()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WorkflowRouting:Rules:0:RuleCode"] = "TYPE",
            ["WorkflowRouting:Rules:0:TicketType"] = "1",
            ["WorkflowRouting:Rules:0:TeamId"] = "10",
            ["WorkflowRouting:Rules:1:RuleCode"] = "STEP",
            ["WorkflowRouting:Rules:1:StepCode"] = "PAYROLL",
            ["WorkflowRouting:Rules:1:TicketType"] = "1",
            ["WorkflowRouting:Rules:1:TeamId"] = "20"
        }).Build();

        var decision = new ConfigurationWorkflowRouter(config).Resolve(new WorkflowRoutingContext(1, 1, null, null, null, "PAYROLL"));

        Assert.Equal(20, decision.TeamId);
        Assert.Equal("STEP", decision.RuleCode);
    }

    [Fact]
    public void Routing_rejects_equal_specificity_ambiguity()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WorkflowRouting:Rules:0:RuleCode"] = "A",
            ["WorkflowRouting:Rules:0:TicketType"] = "1",
            ["WorkflowRouting:Rules:0:TeamId"] = "10",
            ["WorkflowRouting:Rules:1:RuleCode"] = "B",
            ["WorkflowRouting:Rules:1:TicketType"] = "1",
            ["WorkflowRouting:Rules:1:TeamId"] = "20"
        }).Build();

        Assert.Throws<InvalidOperationException>(() => new ConfigurationWorkflowRouter(config).Resolve(new WorkflowRoutingContext(1, 1, null, null, null, "PAYROLL")));
    }
}
