using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class CommunicationSchemaReviewTests
{
    private const string CandidateRelativePath = "Docs/reconciliation/proposed/postgresql/S5-09_communication_schema_REVIEW.sql";

    [Fact]
    public void Review_candidate_contains_every_mapped_communication_column_and_sequence()
    {
        var candidate = ReadCandidate();
        var options = new DbContextOptionsBuilder<TicketsDbContext>()
            .UseNpgsql("Host=not-used.invalid;Database=model-only;Username=model-only")
            .Options;

        using var db = new TicketsDbContext(options);
        var mappedRelations = new (Type Entity, string Table)[]
        {
            (typeof(CommunicationRow), "PA_TICKETS_COMMUNICATION"),
            (typeof(CommunicationTemplateRow), "PA_TICKETS_COMMUNICATION_TEMPLATE"),
            (typeof(CommunicationAttemptRow), "PA_TICKETS_COMMUNICATION_ATTEMPT"),
            (typeof(CommunicationProviderConfigurationRow), "PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG"),
            (typeof(CommunicationChannelConfigurationRow), "PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG")
        };

        foreach (var (entityClass, table) in mappedRelations)
        {
            var entity = db.Model.FindEntityType(entityClass);
            Assert.NotNull(entity);
            Assert.Equal(table, entity!.GetTableName());
            Assert.Null(entity.GetSchema());

            var tableBody = TableBody(candidate, table);
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                Assert.False(string.IsNullOrWhiteSpace(column), $"{entityClass.Name}.{property.Name} has no relational column mapping.");
                var definition = ColumnDefinition(tableBody, column!);
                Assert.Equal(property.IsNullable, !IsNotNull(definition));

                if (property.ClrType == typeof(string))
                {
                    Assert.Null(property.GetMaxLength());
                    Assert.StartsWith("text", definition, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        var sequenceMappings = new (Type Entity, string Column, string Sequence)[]
        {
            (typeof(CommunicationRow), "N_COMMUNICATION_ID", "PA_TICKETS_COMMUNICATION_SEQ"),
            (typeof(CommunicationTemplateRow), "N_TEMPLATE_VERSION_ID", "PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ"),
            (typeof(CommunicationAttemptRow), "N_ATTEMPT_ID", "PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ"),
            (typeof(CommunicationProviderConfigurationRow), "N_PROVIDER_CONFIG_ID", "PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ"),
            (typeof(CommunicationChannelConfigurationRow), "N_CHANNEL_CONFIG_ID", "PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ")
        };

        foreach (var (entityClass, column, sequence) in sequenceMappings)
        {
            var entity = db.Model.FindEntityType(entityClass)!;
            var property = entity.FindProperty("Id")!;
            Assert.Contains(sequence, property.GetDefaultValueSql(), StringComparison.Ordinal);
            Assert.Contains(sequence, PostgreSqlProviderCapabilities.SequenceNames);
            Assert.Contains($"CREATE SEQUENCE IF NOT EXISTS \"{sequence}\"", candidate, StringComparison.Ordinal);
            Assert.Contains($"\"{column}\" bigint PRIMARY KEY DEFAULT nextval('\"{sequence}\"')", candidate, StringComparison.Ordinal);
        }

        Assert.Contains("\"S_STATUS\" text NOT NULL DEFAULT 'QUEUED'", TableBody(candidate, "PA_TICKETS_COMMUNICATION"), StringComparison.Ordinal);
        Assert.Contains("\"S_STATUS\" text NOT NULL DEFAULT 'DRAFT'", TableBody(candidate, "PA_TICKETS_COMMUNICATION_TEMPLATE"), StringComparison.Ordinal);
        Assert.Equal("QUEUED", new CommunicationRow().Status);
        Assert.Equal("DRAFT", new CommunicationTemplateRow().Status);
    }

    [Fact]
    public void Review_candidate_stops_before_ddl_and_contains_no_data_or_cleanup_operations()
    {
        var candidate = ReadCandidate();
        var stop = candidate.IndexOf("RAISE EXCEPTION 'S5-09 communication review artifact; execution is prohibited'", StringComparison.Ordinal);
        var firstDdl = Regex.Match(candidate, @"(?im)^\s*CREATE\s+(?:SEQUENCE|TABLE)\b");

        Assert.True(stop >= 0, "The review-only abort guard must be present.");
        Assert.True(firstDdl.Success && stop < firstDdl.Index, "The abort guard must precede all candidate DDL.");
        Assert.DoesNotMatch(@"(?im)^\s*(?:INSERT|UPDATE|DELETE|DROP|TRUNCATE)\b", candidate);
        Assert.Contains("EF nullable-reference metadata is the nullability source", candidate, StringComparison.Ordinal);
        Assert.Contains("root migration 013", candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string TableBody(string sql, string table)
    {
        var match = Regex.Match(sql,
            $@"(?is)CREATE\s+TABLE\s+""{Regex.Escape(table)}""\s*\((?<body>.*?)\n\);",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Review candidate has no CREATE TABLE body for {table}.");
        return match.Groups["body"].Value;
    }

    private static string ColumnDefinition(string tableBody, string column)
    {
        var match = Regex.Match(tableBody,
            $@"(?im)^\s*""{Regex.Escape(column)}""\s+(?<definition>[^,\r\n]+)");
        Assert.True(match.Success, $"Candidate has no definition for column {column}.");
        return match.Groups["definition"].Value.Trim();
    }

    private static bool IsNotNull(string definition) =>
        definition.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase) ||
        definition.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase);

    private static string ReadCandidate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidatePath = Path.Combine(directory.FullName, CandidateRelativePath);
            if (File.Exists(candidatePath)) return File.ReadAllText(candidatePath);
        }

        throw new FileNotFoundException($"Could not locate {CandidateRelativePath} from the test output directory.");
    }
}
