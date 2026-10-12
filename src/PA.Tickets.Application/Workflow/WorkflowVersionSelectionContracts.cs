namespace PA.Tickets.Application.Workflow;

public sealed record EffectiveWorkflowVersion(long VersionId, long WorkflowId, int VersionNo);

public interface IWorkflowVersionSelector
{
    Task<EffectiveWorkflowVersion> ResolveAsync(long workflowId, DateTimeOffset at, CancellationToken ct);
    Task<EffectiveWorkflowVersion> ResolveVersionAsync(long workflowId, long versionId, DateTimeOffset at, CancellationToken ct);
    Task ValidateRangeAsync(long workflowId, long versionId, DateTimeOffset from, DateTimeOffset? to, CancellationToken ct);
}
