using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Api.Controllers;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Application.Reporting;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;
using PA.Tickets.Infrastructure.Reporting;
namespace PA.Tickets.Domain.Tests;
public sealed class AuditorReportingTests
{
 static AuditorReportingService Create(IAuthorizationService? auth=null){var t=new TicketsDbContext(new DbContextOptionsBuilder<TicketsDbContext>().UseNpgsql("Host=unused").Options);var e=new ExecutionDbContext(new DbContextOptionsBuilder<ExecutionDbContext>().UseNpgsql("Host=unused").Options);return new(t,e,auth??new Allowing(),TimeProvider.System);}
 [Fact] public void Auditor_catalogue_contains_only_controlled_persisted_reports(){var codes=Create().Catalogue().Select(x=>x.Code).ToArray();Assert.Equal(new[]{"AUDIT_ACTIVITY","TICKET_AUDIT_TRAIL","WORKFLOW_AUDIT","APPROVAL_AUDIT","ASSIGNMENT_AUDIT","EVIDENCE_AUDIT","COMMUNICATION_AUDIT"},codes);Assert.DoesNotContain("SECURITY_AUDIT",codes);}
 [Fact] public void Unknown_report_is_rejected(){Assert.Throws<KeyNotFoundException>(()=>Create().GetDefinition("UNKNOWN"));}
 [Fact] public async Task Invalid_page_size_is_rejected_before_database_access()=>await Assert.ThrowsAsync<ArgumentException>(()=>Create().QueryAsync("AUDIT_ACTIVITY",new AuditorQueryRequest(PageSize:101),9001,null,CancellationToken.None));
 [Fact] public async Task Invalid_date_range_is_rejected_before_database_access()=>await Assert.ThrowsAsync<ArgumentException>(()=>Create().QueryAsync("AUDIT_ACTIVITY",new AuditorQueryRequest(new DateOnly(2026,2,2),new DateOnly(2026,2,1)),9001,null,CancellationToken.None));
 [Fact] public async Task Unsupported_sort_is_rejected()=>await Assert.ThrowsAsync<ArgumentException>(()=>Create().QueryAsync("AUDIT_ACTIVITY",new AuditorQueryRequest(Sort:"sql"),9001,null,CancellationToken.None));
 [Fact] public async Task Missing_auditor_permission_is_rejected_before_query(){var auth=new Denying();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Create(auth).QueryAsync("AUDIT_ACTIVITY",new AuditorQueryRequest(),9001,null,CancellationToken.None));Assert.True(auth.Called);}
 [Fact] public async Task Auditor_definition_requires_auditor_permission(){var auth=new Denying();var controller=new AuditorReportsController(Create(),auth);controller.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"9001")}))}};await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>controller.Definition("AUDIT_ACTIVITY",CancellationToken.None));Assert.True(auth.Called);}
 [Theory]
 [InlineData("AUDIT_ACTIVITY")][InlineData("TICKET_AUDIT_TRAIL")][InlineData("WORKFLOW_AUDIT")][InlineData("APPROVAL_AUDIT")][InlineData("ASSIGNMENT_AUDIT")][InlineData("EVIDENCE_AUDIT")][InlineData("COMMUNICATION_AUDIT")]
 public void Report_definitions_require_auditor_view(string code)=>Assert.Equal("AUDITOR_VIEW",Create().GetDefinition(code).Permission);
 [Fact] public void Report_contract_excludes_attachment_contents_and_security_catalogue_gap_is_explicit(){var evidence=Create().GetDefinition("EVIDENCE_AUDIT");Assert.DoesNotContain("content",evidence.Columns,StringComparer.OrdinalIgnoreCase);Assert.Throws<KeyNotFoundException>(()=>Create().GetDefinition("SECURITY_AUDIT"));}
 private sealed class Allowing:IAuthorizationService{public Task EnsurePermissionAsync(long a,string p,AuthorizationResource? r,CancellationToken c)=>Task.CompletedTask;public Task<bool> HasPermissionAsync(long a,string p,AuthorizationResource? r,CancellationToken c)=>Task.FromResult(true);}
 private sealed class Denying:IAuthorizationService{public bool Called;public Task EnsurePermissionAsync(long a,string p,AuthorizationResource? r,CancellationToken c){Called=true;throw new UnauthorizedAccessException();}public Task<bool> HasPermissionAsync(long a,string p,AuthorizationResource? r,CancellationToken c)=>Task.FromResult(false);}
}
