namespace PA.Tickets.Domain.Tests;

internal sealed record PostgreSqlCatalogColumn(string Name, string Type, bool Nullable, string? DefaultExpression);

internal sealed record PostgreSqlCatalogObject(
    string Kind,
    string Schema,
    string Name,
    IReadOnlyList<PostgreSqlCatalogColumn> Columns,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Indexes,
    IReadOnlyList<string> Sequences);

internal sealed record PostgreSqlSchemaSnapshot(string TargetDescriptor, IReadOnlyList<PostgreSqlCatalogObject> Objects);

internal sealed record PostgreSqlSchemaReconciliationRequest(string ApprovedTargetDescriptor, string BaselineArtifact);

internal sealed record PostgreSqlSchemaDifference(string Kind, string Object, string Detail);

internal sealed record PostgreSqlSchemaReconciliationResult(
    string BaselineArtifact,
    IReadOnlyList<PostgreSqlSchemaDifference> Differences)
{
    public bool IsEquivalent => Differences.Count == 0;
}

internal static class PostgreSqlSchemaReconciler
{
    internal static PostgreSqlSchemaReconciliationResult Compare(
        PostgreSqlSchemaReconciliationRequest request,
        PostgreSqlSchemaSnapshot expected,
        PostgreSqlSchemaSnapshot observed)
    {
        if (string.IsNullOrWhiteSpace(request.BaselineArtifact))
            throw new InvalidOperationException("A selected PostgreSQL baseline artifact is required; no baseline is selected implicitly.");
        if (string.IsNullOrWhiteSpace(request.ApprovedTargetDescriptor))
            throw new InvalidOperationException("An approved PostgreSQL target descriptor is required.");
        ValidateDescriptor(request.ApprovedTargetDescriptor);
        if (!string.Equals(expected.TargetDescriptor, request.ApprovedTargetDescriptor, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(observed.TargetDescriptor, request.ApprovedTargetDescriptor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL target identity verification failed; expected and observed metadata must use the approved descriptor.");

        var differences = new List<PostgreSqlSchemaDifference>();
        var expectedObjects = expected.Objects.ToDictionary(ObjectKey, StringComparer.Ordinal);
        var observedObjects = observed.Objects.ToDictionary(ObjectKey, StringComparer.Ordinal);

        foreach (var key in expectedObjects.Keys.Union(observedObjects.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!expectedObjects.TryGetValue(key, out var expectedObject))
            {
                differences.Add(new("UnexpectedObject", key, "present in observed catalog only"));
                continue;
            }
            if (!observedObjects.TryGetValue(key, out var observedObject))
            {
                differences.Add(new("MissingObject", key, "required by selected baseline"));
                continue;
            }

            CompareList(differences, key, "Column", expectedObject.Columns.Select(ColumnValue), observedObject.Columns.Select(ColumnValue));
            CompareList(differences, key, "Constraint", expectedObject.Constraints, observedObject.Constraints);
            CompareList(differences, key, "Index", expectedObject.Indexes, observedObject.Indexes);
            CompareList(differences, key, "Sequence", expectedObject.Sequences, observedObject.Sequences);
        }

        return new(request.BaselineArtifact, differences);
    }

    private static string ObjectKey(PostgreSqlCatalogObject value) => $"{value.Kind}:{value.Schema}:{value.Name}";

    private static void ValidateDescriptor(string descriptor)
    {
        var fields = descriptor.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue("host", out var host) || host.Contains(',', StringComparison.Ordinal) ||
            !fields.TryGetValue("port", out _) || !fields.TryGetValue("database", out var database) ||
            !fields.TryGetValue("user", out var user))
            throw new InvalidOperationException("The approved PostgreSQL target descriptor is incomplete or multi-host; credentials are never logged.");
        if (database.Equals("pa_tickets_test", StringComparison.OrdinalIgnoreCase) ||
            database.Equals("pa_tickets_h_verify", StringComparison.OrdinalIgnoreCase) ||
            user.Equals("pa_tickets_test_user", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL target verification failed: prohibited database or role in target descriptor; credentials are never logged.");
    }

    private static string ColumnValue(PostgreSqlCatalogColumn value) =>
        $"{value.Name}|{value.Type}|nullable={value.Nullable}|default={value.DefaultExpression ?? "<null>"}";

    private static void CompareList(
        ICollection<PostgreSqlSchemaDifference> differences,
        string objectKey,
        string kind,
        IEnumerable<string> expected,
        IEnumerable<string> observed)
    {
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var observedSet = observed.ToHashSet(StringComparer.Ordinal);
        foreach (var value in expectedSet.Except(observedSet, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            differences.Add(new($"Missing{kind}", objectKey, value));
        foreach (var value in observedSet.Except(expectedSet, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            differences.Add(new($"Unexpected{kind}", objectKey, value));
    }
}
