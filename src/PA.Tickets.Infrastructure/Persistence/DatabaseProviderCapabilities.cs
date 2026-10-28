using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Npgsql;

namespace PA.Tickets.Infrastructure.Persistence;

public enum DatabaseProviderKind
{
    PostgreSql,
    Oracle
}

public interface IDatabaseProviderCapabilities
{
    DatabaseProviderKind Kind { get; }
    string SequenceDefaultSql(string oracleExpression);
    void ConfigureSequences(ModelBuilder modelBuilder);
    bool IsTransientTransactionFailure(Exception exception);
    bool IsUniqueConstraintViolation(Exception exception);
    string BuildJobLeaseSql(int limit);
}

public static class DatabaseProviderCapabilities
{
    public static IDatabaseProviderCapabilities Resolve(string? providerName)
    {
        if (providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            return PostgreSqlProviderCapabilities.Instance;
        if (providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            return SqliteProviderCapabilities.Instance;
        if (providerName?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true)
            return OracleProviderCapabilities.Instance;
        throw new InvalidOperationException($"Unsupported database provider '{providerName ?? "<unknown>"}'. Configure PostgreSQL or Oracle.");
    }
}

public sealed class SqliteProviderCapabilities : IDatabaseProviderCapabilities
{
    public static SqliteProviderCapabilities Instance { get; } = new();
    private SqliteProviderCapabilities() { }
    public DatabaseProviderKind Kind => DatabaseProviderKind.PostgreSql;

    public string SequenceDefaultSql(string oracleExpression) => "NULL";

    public void ConfigureSequences(ModelBuilder modelBuilder) { }

    public bool IsTransientTransactionFailure(Exception exception)
    {
        return exception switch
        {
            DbException db when db.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) => true,
            _ => exception.InnerException is not null && IsTransientTransactionFailure(exception.InnerException)
        };
    }

    public bool IsUniqueConstraintViolation(Exception exception)
    {
        return exception switch
        {
            DbException db when db.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) => true,
            _ => exception.InnerException is not null && IsUniqueConstraintViolation(exception.InnerException)
        };
    }

    public string BuildJobLeaseSql(int limit) => string.Empty;
}

public sealed class PostgreSqlProviderCapabilities : IDatabaseProviderCapabilities
{
    public static PostgreSqlProviderCapabilities Instance { get; } = new();
    private PostgreSqlProviderCapabilities() { }
    public DatabaseProviderKind Kind => DatabaseProviderKind.PostgreSql;

    public string SequenceDefaultSql(string oracleExpression)
    {
        var sequence = oracleExpression.Replace(".NEXTVAL", "", StringComparison.OrdinalIgnoreCase);
        return $"nextval('\"{sequence}\"'::regclass)";
    }

    public void ConfigureSequences(ModelBuilder modelBuilder)
    {
        foreach (var sequence in SequenceNames)
            modelBuilder.HasSequence<long>(sequence);
    }

    public bool IsTransientTransactionFailure(Exception exception)
    {
        if (exception.Message.Contains("ORA-08177", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("ORA-00060", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("ORA-08181", StringComparison.OrdinalIgnoreCase)) return true;
        return exception switch
        {
            PostgresException postgres when postgres.SqlState is "40001" or "40P01" => true,
            DbException db when db.Message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase) => true,
            _ => exception.InnerException is not null && IsTransientTransactionFailure(exception.InnerException)
        };
    }

    public bool IsUniqueConstraintViolation(Exception exception)
    {
        if (exception.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("ORA-02299", StringComparison.OrdinalIgnoreCase)) return true;
        return exception switch
        {
            PostgresException postgres when postgres.SqlState == "23505" => true,
            _ => exception.InnerException is not null && IsUniqueConstraintViolation(exception.InnerException)
        };
    }

    public string BuildJobLeaseSql(int limit)
    {
        var boundedLimit = Math.Clamp(limit, 1, 1000);
        return $"""
            SELECT "N_BACKGROUND_JOB_ID", "S_JOB_TYPE", "S_DEDUPE_KEY", "S_PAYLOAD", "S_STATUS",
                   "N_ATTEMPT_COUNT", "D_SCHEDULED_AT", "D_NEXT_ATTEMPT_AT", "D_LOCKED_AT", "S_LOCKED_BY",
                   "S_LAST_ERROR", "D_COMPLETED_AT", "N_MAX_ATTEMPTS", "S_FAILURE_CLASSIFICATION", "D_FIRST_FAILED_AT", "D_LAST_ATTEMPT_AT", "D_FAILED_AT", "S_CORRELATION_ID"
            FROM "PA_TICKETS_BACKGROUND_JOB"
            WHERE (("S_STATUS" = 'PENDING' AND COALESCE("D_NEXT_ATTEMPT_AT", "D_SCHEDULED_AT") <= CURRENT_TIMESTAMP AND "N_ATTEMPT_COUNT" < "N_MAX_ATTEMPTS")
                OR ("S_STATUS" = 'PROCESSING' AND "D_LOCKED_AT" < CURRENT_TIMESTAMP - INTERVAL '10 minutes'))
            ORDER BY "D_SCHEDULED_AT"
            LIMIT {boundedLimit}
            FOR UPDATE SKIP LOCKED
            """;
    }

    public static readonly string[] SequenceNames =
    [
        "PA_TICKETS_WORKFLOW_SEQ", "PA_TICKETS_WORKFLOW_VERSION_SEQ", "PA_TICKETS_WORKFLOW_STEP_SEQ", "PA_TICKETS_WORKFLOW_TRANSITION_SEQ",
        "PA_TICKETS_APPROVAL_RULE_SEQ", "PA_TICKETS_APPROVAL_RULE_STEP_SEQ", "PA_TICKETS_APPROVAL_RULE_VERSION_SEQ",
        "PA_TICKETS_SEQ", "PA_TICKETS_TICKET_NO_SEQ", "PA_TICKETS_COMMUNICATION_SEQ",
        "PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ", "PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ",
        "PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ", "PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ",
        "PA_TICKETS_RELATION_SEQ", "PA_TICKETS_ASSIGNMENT_SEQ", "PA_TICKETS_WORK_ITEM_SEQ",
        "PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY_SEQ", "PA_TICKETS_APPROVAL_INSTANCE_SEQ",
        "PA_TICKETS_APPROVAL_HISTORY_SEQ", "PA_TICKETS_APPROVAL_DELEGATION_SEQ",
        "PA_TICKETS_SLA_PAUSE_SEQ", "PA_TICKETS_IDEMPOTENCY_SEQ",
        "PA_TICKETS_BACKGROUND_JOB_SEQ", "PA_TICKETS_EVENT_SEQ", "PA_TICKETS_AUDIT_SEQ",
        "PA_TICKETS_SERVICE_SEQ", "PA_TICKETS_CATEGORY_SEQ", "PA_TICKETS_SUB_CATEGORY_SEQ", "PA_TICKETS_REQUEST_TYPE_SEQ",
        "PA_TICKETS_FIELD_SEQ", "PA_TICKETS_FIELD_OPTION_SEQ", "PA_TICKETS_REQUEST_FIELD_SEQ", "PA_TICKETS_VALUE_SEQ",
        "PA_TICKETS_STATUS_SEQ", "PA_TICKETS_STATUS_TRANSITION_SEQ", "PA_TICKETS_PRIORITY_SEQ"
    ];
}

public sealed class OracleProviderCapabilities : IDatabaseProviderCapabilities
{
    public static OracleProviderCapabilities Instance { get; } = new();
    private OracleProviderCapabilities() { }
    public DatabaseProviderKind Kind => DatabaseProviderKind.Oracle;

    public string SequenceDefaultSql(string oracleExpression) => oracleExpression;

    public void ConfigureSequences(ModelBuilder modelBuilder)
    {
    }

    public bool IsTransientTransactionFailure(Exception exception)
    {
        if (exception.Message.Contains("ORA-08177", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("ORA-00060", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("ORA-08181", StringComparison.OrdinalIgnoreCase)) return true;
        return exception switch
        {
            OracleException oracle when oracle.Number is 8177 or 60 or 8181 => true,
            _ => exception.InnerException is not null && IsTransientTransactionFailure(exception.InnerException)
        };
    }

    public bool IsUniqueConstraintViolation(Exception exception)
    {
        if (exception.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("ORA-02299", StringComparison.OrdinalIgnoreCase)) return true;
        return exception switch
        {
            OracleException oracle when oracle.Number is 1 or 2299 => true,
            _ => exception.InnerException is not null && IsUniqueConstraintViolation(exception.InnerException)
        };
    }

    public string BuildJobLeaseSql(int limit)
    {
        var boundedLimit = Math.Clamp(limit, 1, 1000);
        return $"""
            SELECT "N_BACKGROUND_JOB_ID", "S_JOB_TYPE", "S_DEDUPE_KEY", "S_PAYLOAD", "S_STATUS",
                   "N_ATTEMPT_COUNT", "D_SCHEDULED_AT", "D_NEXT_ATTEMPT_AT", "D_LOCKED_AT", "S_LOCKED_BY",
                   "S_LAST_ERROR", "D_COMPLETED_AT", "N_MAX_ATTEMPTS", "S_FAILURE_CLASSIFICATION", "D_FIRST_FAILED_AT", "D_LAST_ATTEMPT_AT", "D_FAILED_AT", "S_CORRELATION_ID"
            FROM (
                SELECT j.*
                FROM PA_TICKETS_BACKGROUND_JOB j
                WHERE ((j.S_STATUS = 'PENDING' AND NVL(j.D_NEXT_ATTEMPT_AT, j.D_SCHEDULED_AT) <= SYSTIMESTAMP AND j.N_ATTEMPT_COUNT < j.N_MAX_ATTEMPTS)
                    OR (j.S_STATUS = 'PROCESSING' AND j.D_LOCKED_AT < SYSTIMESTAMP - INTERVAL '10' MINUTE))
                ORDER BY j.D_SCHEDULED_AT
                FETCH FIRST {boundedLimit} ROWS ONLY
            )
            FOR UPDATE SKIP LOCKED
            """;
    }
}
