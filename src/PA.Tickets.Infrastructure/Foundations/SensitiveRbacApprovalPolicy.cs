using System.Security.Cryptography;
using System.Text;

namespace PA.Tickets.Infrastructure.Foundations;

public static class SensitiveRbacApprovalPolicy
{
    public const string PermissionGrantOperation = "ROLE_PERMISSION_GRANT";
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Applied = "APPLIED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";

    public static bool RequiresApproval(string securityClass) =>
        !string.IsNullOrWhiteSpace(securityClass) &&
        RoleAuthorizationPolicy.RequiresIndependentApproval(securityClass);

    public static bool CanTransition(string current, string next) =>
        (current, next) switch
        {
            (Pending, Approved or Rejected or Expired or Cancelled) => true,
            (Approved, Applied) => true,
            _ => false
        };

    public static string ComputePayloadHash(string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    public static bool PayloadHashMatches(string payload, string expectedHash)
    {
        if (expectedHash.Length != 64 ||
            expectedHash.Any(character => !char.IsAsciiHexDigit(character)))
            return false;
        var actual = Convert.FromHexString(ComputePayloadHash(payload));
        var expected = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool IsIndependentApprover(long initiatorId, long approverId, bool active,
        bool hasProtectedSuperAdminMembership, bool hasCurrentApprovalPermission) =>
        initiatorId != approverId && active && hasProtectedSuperAdminMembership && hasCurrentApprovalPermission;

    public static bool RequiresProposalForDirectGrant(bool isSystemRole, string securityClass) =>
        isSystemRole || RequiresApproval(securityClass);

    public static bool CanCreatePermissionProposal(string roleCode, bool isSystemRole, string securityClass) =>
        !roleCode.Equals(RoleAuthorizationPolicy.SuperAdmin, StringComparison.OrdinalIgnoreCase) &&
        (isSystemRole || RequiresApproval(securityClass));

    public static bool HasExpectedVersion(int currentVersion, int expectedVersion) =>
        currentVersion > 0 && currentVersion == expectedVersion;

    public static bool IsExpired(DateTimeOffset expiresAt, DateTimeOffset now) =>
        expiresAt <= now;

    public static bool IsPendingAndExpired(string status, DateTimeOffset expiresAt, DateTimeOffset now) =>
        status == Pending && IsExpired(expiresAt, now);

    public static bool HasCurrentPermissionSnapshot(
        IEnumerable<long> expectedPermissionIds,
        IEnumerable<long> currentPermissionIds,
        long permissionToGrant) =>
        expectedPermissionIds.ToHashSet().SetEquals(currentPermissionIds) &&
        !currentPermissionIds.Contains(permissionToGrant);

    public static bool CanReturnAppliedRetry(string status, long? approverId, long actorId) =>
        status == Applied && approverId == actorId;

    public static bool CanReturnRejectedRetry(
        string status, long? approverId, string? priorReason, long actorId, string reason) =>
        status == Rejected && approverId == actorId && priorReason == reason;

    public static bool CanReturnCancelledRetry(
        string status, long? cancelledById, string? priorReason, long actorId, string reason) =>
        status == Cancelled && cancelledById == actorId && priorReason == reason;

    public static bool AllowsRolePermission(
        string roleCode, bool isSystemRole, string securityClass, string actionCode, bool readOnly) =>
        RoleAuthorizationPolicy.AllowsPermission(roleCode, isSystemRole, securityClass, actionCode, readOnly);

    public static string SerializePermissionGrantPayload(SensitivePermissionGrantPayload payload) =>
        System.Text.Json.JsonSerializer.Serialize(payload);

    public static bool IsBoundToProposal(
        SensitivePermissionGrantPayload payload,
        SensitivePermissionGrantEnvelope envelope) =>
        payload.OperationType == envelope.OperationType &&
        payload.ProposalId == envelope.Id &&
        payload.InitiatorUserId == envelope.InitiatorUserId &&
        payload.CreatedAt == envelope.CreatedAt &&
        payload.ExpiresAt == envelope.ExpiresAt &&
        payload.Reason == envelope.Reason &&
        payload.IdempotencyKey == envelope.IdempotencyKey &&
        payload.CorrelationId == envelope.CorrelationId &&
        payload.TargetRoleId == envelope.TargetRoleId &&
        payload.TargetPermissionId == envelope.TargetPermissionId;
}

public sealed record SensitivePermissionGrantPayload(
    string OperationType,
    string ProposalId,
    long InitiatorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string Reason,
    string IdempotencyKey,
    string CorrelationId,
    long TargetRoleId,
    string TargetRoleCode,
    bool TargetRoleIsSystemRole,
    long TargetPermissionId,
    string TargetPermissionCode,
    string TargetPermissionName,
    string ModuleCode,
    string ActionCode,
    string ScopeClass,
    string SecurityClass,
    bool ReadOnly,
    long[] ExpectedActivePermissionIds);

public sealed record SensitivePermissionGrantEnvelope(
    string Id,
    string OperationType,
    long InitiatorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string Reason,
    string IdempotencyKey,
    string CorrelationId,
    long TargetRoleId,
    long TargetPermissionId);
