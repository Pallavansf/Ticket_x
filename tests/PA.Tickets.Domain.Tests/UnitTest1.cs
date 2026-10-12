using PA.Tickets.Application.Execution;
using PA.Tickets.Domain.Tickets;
using PA.Tickets.Infrastructure.Execution;

namespace PA.Tickets.Domain.Tests;

public sealed class TicketLifecycleTests
{
    private static Ticket NewTicket() => Ticket.Create(1, "REQ-000001", TicketType.Request, "Access request", "Need access", 10, 10, DateTimeOffset.UtcNow);

    [Fact]
    public void Ticket_requires_evidence_before_resolution()
    {
        var ticket = NewTicket();
        ticket.Classify(1, 2, 3, 2, 10, DateTimeOffset.UtcNow);
        var item = ticket.Route(2, "REQ-000001-WI-001", 1, 1, 10, DateTimeOffset.UtcNow);
        ticket.StartExecution(item, 20, 10, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => ticket.Resolve(10, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Requester_cannot_approve_own_ticket()
    {
        var ticket = NewTicket();
        Assert.Throws<InvalidOperationException>(() => ticket.RequestApproval(10, 10, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Full_lifecycle_requires_approval_and_evidence()
    {
        var ticket = NewTicket();
        ticket.Classify(1, 2, 3, 2, 10, DateTimeOffset.UtcNow);
        var item = ticket.Route(2, "REQ-000001-WI-001", 1, 1, 10, DateTimeOffset.UtcNow);
        ticket.RequestApproval(30, 10, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => ticket.StartExecution(item, 20, 10, DateTimeOffset.UtcNow));
        ticket.Approve(30, DateTimeOffset.UtcNow);
        ticket.StartExecution(item, 20, 10, DateTimeOffset.UtcNow);
        ticket.AddEvidence(20, DateTimeOffset.UtcNow);
        ticket.CompleteWork(item, 20, DateTimeOffset.UtcNow);
        ticket.Resolve(10, DateTimeOffset.UtcNow);
        ticket.Close(10, DateTimeOffset.UtcNow);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Contains(ticket.UncommittedEvents, e => e.Action == "TICKET_CLOSED");
    }

    [Fact]
    public void Attachment_policy_does_not_allow_retired_version_to_return()
    {
        Assert.Throws<InvalidOperationException>(() => AttachmentVersionPolicy.ValidateTransition("RETIRED", "CURRENT"));
        AttachmentVersionPolicy.ValidateTransition("CURRENT", "SUPERSEDED");
    }

    [Fact]
    public void Durable_approval_deduplicates_same_user_same_level_and_preserves_required_flag()
    {
        var normalized = DurableApprovalExecutionRepository.NormalizeApprovers(
        [
            new DurableApprovalApprover(1002, 1, false),
            new DurableApprovalApprover(1002, 1, true),
            new DurableApprovalApprover(1003, 1, false),
            new DurableApprovalApprover(1004, 2, true),
            new DurableApprovalApprover(1004, 1, false)
        ]);

        Assert.Collection(normalized.OrderBy(x => x.Level).ThenBy(x => x.UserId).ToArray(),
            x => { Assert.Equal(1002L, x.UserId); Assert.Equal(1, x.Level); Assert.True(x.Required); },
            x => { Assert.Equal(1003L, x.UserId); Assert.Equal(1, x.Level); Assert.False(x.Required); },
            x => { Assert.Equal(1004L, x.UserId); Assert.Equal(1, x.Level); Assert.False(x.Required); },
            x => { Assert.Equal(1004L, x.UserId); Assert.Equal(2, x.Level); Assert.True(x.Required); });
    }
}

public sealed class Phase3FoundationTests
{
    [Fact]
    public void Waiting_and_resume_preserve_first_class_waiting_state()
    {
        var ticket=TicketLifecycleTestsFactory.Create();
        ticket.Classify(1,2,3,2,10,DateTimeOffset.UtcNow);
        var item=ticket.Route(2,"REQ-1-WI-1",1,1,10,DateTimeOffset.UtcNow);
        ticket.StartExecution(item,20,10,DateTimeOffset.UtcNow);
        ticket.Wait(item,"CLIENT",10,DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(TicketStatus.Waiting,ticket.Status);
        ticket.Resume(item,10,DateTimeOffset.UtcNow.AddMinutes(6));
        Assert.Equal(TicketStatus.InProgress,ticket.Status);
        Assert.Equal(5,item.WaitingMinutes);
    }
    [Fact]
    public void Closed_ticket_reopens_with_reason_and_can_be_cancelled_only_before_close()
    {
        var ticket=TicketLifecycleTestsFactory.Create();
        ticket.Classify(1,2,3,2,10,DateTimeOffset.UtcNow);var item=ticket.Route(2,"REQ-1-WI-1",1,1,10,DateTimeOffset.UtcNow);ticket.StartExecution(item,20,10,DateTimeOffset.UtcNow);ticket.AddEvidence(20,DateTimeOffset.UtcNow);ticket.CompleteWork(item,20,DateTimeOffset.UtcNow);ticket.Resolve(10,DateTimeOffset.UtcNow);ticket.Close(10,DateTimeOffset.UtcNow);
        ticket.Reopen("Customer supplied new evidence",10,DateTimeOffset.UtcNow);Assert.Equal(TicketStatus.Reopened,ticket.Status);Assert.Contains(ticket.UncommittedEvents,e=>e.Action=="TICKET_REOPENED");
    }
    [Fact]
    public void Sla_resolution_duration_adds_active_and_waiting_minutes(){Assert.Equal(75,PA.Tickets.Domain.Sla.SlaDuration.ResolutionMinutes(new( DateTimeOffset.UtcNow,60,15,false,false)));}
}
public static class TicketLifecycleTestsFactory { public static Ticket Create()=>Ticket.Create(1,"REQ-1",TicketType.Request,"Subject",null,10,10,DateTimeOffset.UtcNow); }

public sealed class WorkflowApprovalSlaTests
{
 [Fact] public void Parallel_join_all_required_activates_only_after_both_branches(){var v=new PA.Tickets.Domain.Workflow.WorkflowVersion(1,1,1,new[]{new PA.Tickets.Domain.Workflow.WorkflowStepDefinition("A","Start",PA.Tickets.Domain.Workflow.WorkflowStepType.Work),new("B","Branch B",PA.Tickets.Domain.Workflow.WorkflowStepType.Work),new("C","Branch C",PA.Tickets.Domain.Workflow.WorkflowStepType.Work),new("E","End",PA.Tickets.Domain.Workflow.WorkflowStepType.Work)},new[]{new PA.Tickets.Domain.Workflow.WorkflowTransitionDefinition("A-B","A","B"),new("A-C","A","C"),new("B-E","B","E"),new("C-E","C","E")});Assert.True(v.Validate().IsValid);var i=new PA.Tickets.Domain.Workflow.WorkflowInstance(1,10,v,DateTimeOffset.UtcNow);i.CompleteStep("A",v,new Dictionary<string,string?>(),DateTimeOffset.UtcNow);Assert.Contains("B",i.ActiveSteps);Assert.Contains("C",i.ActiveSteps);i.CompleteStep("B",v,new Dictionary<string,string?>(),DateTimeOffset.UtcNow);Assert.DoesNotContain("E",i.ActiveSteps);i.CompleteStep("C",v,new Dictionary<string,string?>(),DateTimeOffset.UtcNow);Assert.Contains("E",i.ActiveSteps);}
 [Fact] public void Conditional_transition_uses_controlled_fields_only(){var v=new PA.Tickets.Domain.Workflow.WorkflowVersion(1,1,1,new[]{new PA.Tickets.Domain.Workflow.WorkflowStepDefinition("A","Decision",PA.Tickets.Domain.Workflow.WorkflowStepType.Decision),new("B","True",PA.Tickets.Domain.Workflow.WorkflowStepType.Work),new("C","False",PA.Tickets.Domain.Workflow.WorkflowStepType.Work)},new[]{new PA.Tickets.Domain.Workflow.WorkflowTransitionDefinition("T","A","B",new("Priority","EQUALS","CRITICAL")),new("F","A","C",new("Priority","NOT_EQUALS","CRITICAL"))});var i=new PA.Tickets.Domain.Workflow.WorkflowInstance(1,10,v,DateTimeOffset.UtcNow);i.CompleteStep("A",v,new Dictionary<string,string?>{{"Priority","CRITICAL"}},DateTimeOffset.UtcNow);Assert.Contains("B",i.ActiveSteps);Assert.DoesNotContain("C",i.ActiveSteps);}
 [Fact] public void Sequential_approval_enforces_levels_and_sod(){var rule=new PA.Tickets.Domain.Approvals.ApprovalRule(1,"RULE",PA.Tickets.Domain.Approvals.ApprovalMode.Sequential,new[]{30L,40L});var a=new PA.Tickets.Domain.Approvals.ApprovalInstance(rule,1,10,10,20);a.Request(10,DateTimeOffset.UtcNow);Assert.Throws<InvalidOperationException>(()=>a.Decide(40,PA.Tickets.Domain.Approvals.ApprovalDecision.Approved,null,DateTimeOffset.UtcNow));a.Decide(30,PA.Tickets.Domain.Approvals.ApprovalDecision.Approved,null,DateTimeOffset.UtcNow);a.Decide(40,PA.Tickets.Domain.Approvals.ApprovalDecision.Approved,null,DateTimeOffset.UtcNow);Assert.True(a.IsComplete);}
 [Fact] public void Sla_clock_uses_business_calendar_pause_extensions_and_checks_both_deadlines()
 {
  var hours=Enum.GetValues<DayOfWeek>().Where(day=>day is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
   .ToDictionary(day=>day,day=>(TimeSpan.FromHours(9),TimeSpan.FromHours(17)));
  var holiday=new DateOnly(2026,9,14);
  var cal=new PA.Tickets.Domain.Sla.BusinessCalendar("UTC",hours,new HashSet<DateOnly>{holiday});
  var policy=new PA.Tickets.Domain.Sla.SlaPolicyVersion(1,"SLA",1,DateTimeOffset.UtcNow,null,30,60,"UTC",null,null,null,null,null);
  var clock=new PA.Tickets.Domain.Sla.SlaClock(policy,cal,new DateTimeOffset(2026,9,11,9,0,0,TimeSpan.Zero));
  Assert.Equal(new DateTimeOffset(2026,9,11,9,30,0,TimeSpan.Zero),clock.ResponseDueAt);
  clock.Pause("CLIENT",new DateTimeOffset(2026,9,11,9,15,0,TimeSpan.Zero));
  clock.Resume(cal,new DateTimeOffset(2026,9,11,9,45,0,TimeSpan.Zero));
  Assert.Equal(new DateTimeOffset(2026,9,11,10,0,0,TimeSpan.Zero),clock.ResponseDueAt);
  Assert.Equal(new DateTimeOffset(2026,9,11,10,30,0,TimeSpan.Zero),clock.ResolutionDueAt);
  Assert.False(clock.CheckBreach(new DateTimeOffset(2026,9,11,9,59,0,TimeSpan.Zero)));
  Assert.True(clock.CheckBreach(new DateTimeOffset(2026,9,11,10,0,0,TimeSpan.Zero)));
  Assert.Equal(PA.Tickets.Domain.Sla.SlaClockStatus.Breached,clock.Status);
  Assert.Equal(30,clock.WaitingMinutes);

  var holidayPolicy=policy with { ResponseTargetMinutes=60 };
  var holidayClock=new PA.Tickets.Domain.Sla.SlaClock(holidayPolicy,cal,new DateTimeOffset(2026,9,11,16,30,0,TimeSpan.Zero));
  Assert.Equal(new DateTimeOffset(2026,9,15,9,30,0,TimeSpan.Zero),holidayClock.ResponseDueAt);
 }
}
