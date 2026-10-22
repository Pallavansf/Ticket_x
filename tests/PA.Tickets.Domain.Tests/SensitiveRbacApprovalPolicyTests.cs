using PA.Tickets.Infrastructure.Foundations;
using PA.Tickets.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PA.Tickets.Domain.Tests;

public sealed class SensitiveRbacApprovalPolicyTests
{
    [Theory]
    [InlineData("DEPARTMENT_ADMIN", true)]
    [InlineData("organization_admin", true)]
    [InlineData("Security_Governance", true)]
    [InlineData("BUSINESS", false)]
    [InlineData("", false)]
    public void Approval_requirement_normalizes_security_class(string securityClass, bool expected) =>
        Assert.Equal(expected, SensitiveRbacApprovalPolicy.RequiresApproval(securityClass));

    [Fact]
    public void Proposal_lifecycle_allows_only_pending_decisions_and_atomic_application()
    {
        Assert.True(SensitiveRbacApprovalPolicy.CanTransition("PENDING", "APPROVED"));
        Assert.True(SensitiveRbacApprovalPolicy.CanTransition("APPROVED", "APPLIED"));
        Assert.True(SensitiveRbacApprovalPolicy.CanTransition("PENDING", "REJECTED"));
        Assert.True(SensitiveRbacApprovalPolicy.CanTransition("PENDING", "EXPIRED"));
        Assert.True(SensitiveRbacApprovalPolicy.CanTransition("PENDING", "CANCELLED"));
        Assert.False(SensitiveRbacApprovalPolicy.CanTransition("APPLIED", "PENDING"));
        Assert.False(SensitiveRbacApprovalPolicy.CanTransition("REJECTED", "APPROVED"));
        Assert.False(SensitiveRbacApprovalPolicy.CanTransition("CANCELLED", "APPLIED"));
    }

    [Theory]
    [InlineData("REJECTED", "APPROVED")]
    [InlineData("CANCELLED", "APPROVED")]
    [InlineData("EXPIRED", "APPROVED")]
    [InlineData("APPLIED", "APPROVED")]
    [InlineData("APPROVED", "REJECTED")]
    [InlineData("APPROVED", "CANCELLED")]
    [InlineData("UNKNOWN", "APPLIED")]
    public void Terminal_or_unknown_proposal_states_cannot_be_replayed(string current, string next) =>
        Assert.False(SensitiveRbacApprovalPolicy.CanTransition(current, next));

    [Fact]
    public void Transition_table_contains_only_the_documented_edges()
    {
        var states = new[] { "PENDING", "APPROVED", "APPLIED", "REJECTED", "EXPIRED", "CANCELLED" };
        var allowed = new HashSet<(string Current, string Next)>
        {
            ("PENDING", "APPROVED"),
            ("PENDING", "REJECTED"),
            ("PENDING", "EXPIRED"),
            ("PENDING", "CANCELLED"),
            ("APPROVED", "APPLIED")
        };

        foreach (var current in states)
        foreach (var next in states)
            Assert.Equal(allowed.Contains((current, next)), SensitiveRbacApprovalPolicy.CanTransition(current, next));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, false)]
    public void Decision_requires_the_current_expected_version(int current, int expected, bool result) =>
        Assert.Equal(result, SensitiveRbacApprovalPolicy.HasExpectedVersion(current, expected));

    [Fact]
    public void Expiry_boundary_is_inclusive_and_does_not_depend_on_list_position()
    {
        var now = DateTimeOffset.Parse("2026-10-14T12:00:00Z");
        Assert.True(SensitiveRbacApprovalPolicy.IsExpired(now, now));
        Assert.True(SensitiveRbacApprovalPolicy.IsExpired(now.AddTicks(-1), now));
        Assert.False(SensitiveRbacApprovalPolicy.IsExpired(now.AddTicks(1), now));
    }

    [Fact]
    public void Every_expired_pending_proposal_is_due_for_expiry_even_beyond_the_list_batch()
    {
        var now = DateTimeOffset.Parse("2026-10-14T12:00:00Z");
        var proposals = Enumerable.Range(0, 51)
            .Select(index => (Status: SensitiveRbacApprovalPolicy.Pending, ExpiresAt: now.AddTicks(-index - 1)))
            .ToArray();

        Assert.Equal(51, proposals.Count(proposal =>
            SensitiveRbacApprovalPolicy.IsPendingAndExpired(proposal.Status, proposal.ExpiresAt, now)));
        Assert.False(SensitiveRbacApprovalPolicy.IsPendingAndExpired(
            SensitiveRbacApprovalPolicy.Pending, now.AddTicks(1), now));
        Assert.False(SensitiveRbacApprovalPolicy.IsPendingAndExpired(
            SensitiveRbacApprovalPolicy.Expired, now.AddTicks(-1), now));
    }

    [Fact]
    public void Payload_hash_detects_mutation_and_malformed_hashes()
    {
        const string payload = "{\"operation\":\"ROLE_PERMISSION_GRANT\",\"granted\":true}";
        var hash = SensitiveRbacApprovalPolicy.ComputePayloadHash(payload);

        Assert.True(SensitiveRbacApprovalPolicy.PayloadHashMatches(payload, hash));
        Assert.False(SensitiveRbacApprovalPolicy.PayloadHashMatches(payload + " ", hash));
        Assert.False(SensitiveRbacApprovalPolicy.PayloadHashMatches(payload, new string('z', 64)));
    }

    [Fact]
    public void Serialized_payload_digest_covers_every_security_relevant_field()
    {
        var payload = new SensitivePermissionGrantPayload(
            "ROLE_PERMISSION_GRANT", "a".PadLeft(32, '0'), 10,
            DateTimeOffset.Parse("2026-10-14T12:00:00Z"),
            DateTimeOffset.Parse("2026-10-15T12:00:00Z"),
            "Reviewed grant", "idempotency-key-0001", "correlation-1",
            1, "ADMIN", true, 2, "TICKET_EDIT", "Edit ticket",
            "TICKETS", "EDIT", "DEPARTMENT", "DEPARTMENT_ADMIN", false, [3, 4]);
        var json = SensitiveRbacApprovalPolicy.SerializePermissionGrantPayload(payload);
        var digest = SensitiveRbacApprovalPolicy.ComputePayloadHash(json);
        var envelope = new SensitivePermissionGrantEnvelope(
            payload.ProposalId, payload.OperationType, payload.InitiatorUserId, payload.CreatedAt,
            payload.ExpiresAt, payload.Reason, payload.IdempotencyKey, payload.CorrelationId,
            payload.TargetRoleId, payload.TargetPermissionId);

        Assert.True(SensitiveRbacApprovalPolicy.PayloadHashMatches(json, digest));
        Assert.True(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { Id = "different" }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { OperationType = "OTHER" }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { InitiatorUserId = 11 }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { CreatedAt = payload.CreatedAt.AddSeconds(1) }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { ExpiresAt = payload.ExpiresAt.AddSeconds(1) }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { Reason = "Changed" }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { IdempotencyKey = "changed" }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { CorrelationId = "changed" }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { TargetRoleId = 99 }));
        Assert.False(SensitiveRbacApprovalPolicy.IsBoundToProposal(payload, envelope with { TargetPermissionId = 99 }));
        AssertPayloadMutationInvalidatesDigest(payload with { OperationType = "OTHER" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { ProposalId = "b".PadLeft(32, '0') }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { InitiatorUserId = 11 }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { CreatedAt = payload.CreatedAt.AddSeconds(1) }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { ExpiresAt = payload.ExpiresAt.AddSeconds(1) }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { Reason = "Changed reason" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { IdempotencyKey = "different-key-0001" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { CorrelationId = "different-correlation" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { TargetRoleId = 9 }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { TargetRoleCode = "USER" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { TargetRoleIsSystemRole = false }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { TargetPermissionId = 9 }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { TargetPermissionCode = "OTHER" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { TargetPermissionName = "Other" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { ModuleCode = "OTHER" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { ActionCode = "VIEW" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { ScopeClass = "ORGANIZATION" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { SecurityClass = "BUSINESS" }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { ReadOnly = true }, digest);
        AssertPayloadMutationInvalidatesDigest(payload with { ExpectedActivePermissionIds = [4, 5] }, digest);
    }

    [Fact]
    public void Recomputed_unkeyed_digest_accepts_a_modified_payload_and_does_not_authenticate_its_writer()
    {
        const string originalPayload = "{\"targetPermissionId\":2}";
        const string modifiedPayload = "{\"targetPermissionId\":99}";
        var modifiedDigest = SensitiveRbacApprovalPolicy.ComputePayloadHash(modifiedPayload);

        Assert.True(SensitiveRbacApprovalPolicy.PayloadHashMatches(modifiedPayload, modifiedDigest));
        Assert.False(SensitiveRbacApprovalPolicy.PayloadHashMatches(modifiedPayload,
            SensitiveRbacApprovalPolicy.ComputePayloadHash(originalPayload)));
    }

    private static void AssertPayloadMutationInvalidatesDigest(SensitivePermissionGrantPayload payload, string digest)
    {
        var mutated = SensitiveRbacApprovalPolicy.SerializePermissionGrantPayload(payload);
        Assert.False(SensitiveRbacApprovalPolicy.PayloadHashMatches(mutated, digest));
    }

    [Theory]
    [InlineData(10, 10, true, true, true, false)]
    [InlineData(10, 11, true, true, true, true)]
    [InlineData(10, 11, false, true, true, false)]
    [InlineData(10, 11, true, false, true, false)]
    [InlineData(10, 11, true, true, false, false)]
    public void Approval_requires_a_distinct_active_actor_with_protected_authority(
        long initiatorId,
        long approverId,
        bool active,
        bool protectedSuperAdminMembership,
        bool currentApprovalPermission,
        bool expected) =>
        Assert.Equal(expected, SensitiveRbacApprovalPolicy.IsIndependentApprover(
            initiatorId, approverId, active, protectedSuperAdminMembership, currentApprovalPermission));

    [Theory]
    [InlineData(false, "BUSINESS", false)]
    [InlineData(false, "DEPARTMENT_ADMIN", true)]
    [InlineData(false, "ORGANIZATION_ADMIN", true)]
    [InlineData(false, "SECURITY_GOVERNANCE", true)]
    [InlineData(true, "BUSINESS", true)]
    [InlineData(true, "READ_ONLY", true)]
    public void Direct_permission_grant_path_cannot_bypass_proposal_policy(
        bool systemRole, string securityClass, bool requiresProposal) =>
        Assert.Equal(requiresProposal,
            SensitiveRbacApprovalPolicy.RequiresProposalForDirectGrant(systemRole, securityClass));

    [Theory]
    [InlineData("USER", true, "BUSINESS", true)]
    [InlineData("ADMIN", true, "DEPARTMENT_ADMIN", true)]
    [InlineData("SUPER_ADMIN", true, "BUSINESS", false)]
    [InlineData("TICKET_COORDINATOR", false, "DEPARTMENT_ADMIN", true)]
    [InlineData("TICKET_COORDINATOR", false, "BUSINESS", false)]
    public void Proposal_submission_is_limited_to_supported_targets(
        string role, bool systemRole, string securityClass, bool supported) =>
        Assert.Equal(supported,
            SensitiveRbacApprovalPolicy.CanCreatePermissionProposal(role, systemRole, securityClass));

    [Theory]
    [InlineData("USER", true, "ORGANIZATION_ADMIN", "MANAGE", false, false)]
    [InlineData("ADMIN", true, "SECURITY_GOVERNANCE", "MANAGE", false, false)]
    [InlineData("ADMIN_HEAD", true, "ORGANIZATION_ADMIN", "MANAGE", false, false)]
    [InlineData("TICKET_COORDINATOR", false, "SECURITY_GOVERNANCE", "MANAGE", false, false)]
    [InlineData("SPECTATOR", true, "READ_ONLY", "VIEW", true, true)]
    [InlineData("SPECTATOR", true, "BUSINESS", "EDIT", false, false)]
    [InlineData("SUPER_ADMIN", true, "SECURITY_GOVERNANCE", "MANAGE", false, true)]
    [InlineData("SUPER_ADMIN", false, "BUSINESS", "EDIT", false, false)]
    public void Role_ceiling_rejects_forbidden_permission_combinations(
        string role, bool systemRole, string securityClass, string action, bool readOnly, bool allowed) =>
        Assert.Equal(allowed, SensitiveRbacApprovalPolicy.AllowsRolePermission(
            role, systemRole, securityClass, action, readOnly));

    [Fact]
    public void Permission_snapshot_must_match_and_target_permission_must_not_already_be_granted()
    {
        Assert.True(SensitiveRbacApprovalPolicy.HasCurrentPermissionSnapshot([1, 2], [2, 1], 3));
        Assert.False(SensitiveRbacApprovalPolicy.HasCurrentPermissionSnapshot([1], [1, 2], 3));
        Assert.False(SensitiveRbacApprovalPolicy.HasCurrentPermissionSnapshot([1, 2], [1], 3));
        Assert.False(SensitiveRbacApprovalPolicy.HasCurrentPermissionSnapshot([1, 2], [1, 2], 2));
    }

    [Fact]
    public void Retries_are_idempotent_only_for_the_same_actor_and_same_terminal_request()
    {
        Assert.True(SensitiveRbacApprovalPolicy.CanReturnAppliedRetry("APPLIED", 22, 22));
        Assert.False(SensitiveRbacApprovalPolicy.CanReturnAppliedRetry("APPLIED", 22, 23));
        Assert.False(SensitiveRbacApprovalPolicy.CanReturnAppliedRetry("PENDING", null, 22));

        Assert.True(SensitiveRbacApprovalPolicy.CanReturnRejectedRetry("REJECTED", 22, "reason", 22, "reason"));
        Assert.False(SensitiveRbacApprovalPolicy.CanReturnRejectedRetry("REJECTED", 22, "reason", 22, "different"));
        Assert.False(SensitiveRbacApprovalPolicy.CanReturnRejectedRetry("REJECTED", 22, "reason", 23, "reason"));

        Assert.True(SensitiveRbacApprovalPolicy.CanReturnCancelledRetry("CANCELLED", 11, "reason", 11, "reason"));
        Assert.False(SensitiveRbacApprovalPolicy.CanReturnCancelledRetry("CANCELLED", 11, "reason", 11, "different"));
        Assert.False(SensitiveRbacApprovalPolicy.CanReturnCancelledRetry("CANCELLED", 11, "reason", 12, "reason"));
    }

    [Fact]
    public void Proposal_persistence_mapping_has_concurrency_and_retry_constraints()
    {
        var options = new DbContextOptionsBuilder<TicketsDbContext>()
            .UseNpgsql("Host=localhost;Database=not-used;Username=not-used;Password=not-used")
            .Options;
        using var db = new TicketsDbContext(options);
        var entity = db.Model.FindEntityType(typeof(RbacProposalRow));

        Assert.NotNull(entity);
        Assert.Equal("PA_TICKETS_RBAC_PROPOSAL", entity!.GetTableName());
        Assert.True(entity.FindProperty(nameof(RbacProposalRow.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("text", entity.FindProperty(nameof(RbacProposalRow.PayloadJson))!.GetColumnType());
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(RbacProposalRow.InitiatorUserId),
                nameof(RbacProposalRow.IdempotencyKey)
            }));
    }
}
