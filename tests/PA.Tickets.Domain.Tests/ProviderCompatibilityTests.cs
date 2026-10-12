using Microsoft.EntityFrameworkCore;
using Npgsql;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class ProviderCompatibilityTests
{
    [Fact]
    public void Unsupported_provider_fails_fast()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseProviderCapabilities.Resolve("Microsoft.EntityFrameworkCore.SqlServer"));
    }

    [Fact]
    public void Sequence_defaults_are_provider_specific_and_stable()
    {
        Assert.Equal("nextval('\"PA_TICKETS_SEQ\"'::regclass)", PostgreSqlProviderCapabilities.Instance.SequenceDefaultSql("PA_TICKETS_SEQ.NEXTVAL"));
        Assert.Equal("PA_TICKETS_SEQ.NEXTVAL", OracleProviderCapabilities.Instance.SequenceDefaultSql("PA_TICKETS_SEQ.NEXTVAL"));
    }

    [Fact]
    public void Provider_error_classification_covers_postgres_and_oracle()
    {
        Assert.True(PostgreSqlProviderCapabilities.Instance.IsTransientTransactionFailure(new PostgresException("serialization", "ERROR", "ERROR", "40001")));
        Assert.True(PostgreSqlProviderCapabilities.Instance.IsUniqueConstraintViolation(new PostgresException("duplicate", "ERROR", "ERROR", "23505")));
        Assert.True(OracleProviderCapabilities.Instance.IsTransientTransactionFailure(new InvalidOperationException("ORA-08177: can't serialize access for this transaction")));
        Assert.True(OracleProviderCapabilities.Instance.IsUniqueConstraintViolation(new InvalidOperationException("ORA-00001: unique constraint violated")));
    }

    [Fact]
    public void Lease_sql_uses_atomic_row_claiming_for_both_providers()
    {
        Assert.Contains("FOR UPDATE SKIP LOCKED", PostgreSqlProviderCapabilities.Instance.BuildJobLeaseSql(25));
        Assert.Contains("FOR UPDATE SKIP LOCKED", OracleProviderCapabilities.Instance.BuildJobLeaseSql(25));
        Assert.Contains("LIMIT 25", PostgreSqlProviderCapabilities.Instance.BuildJobLeaseSql(25));
        Assert.Contains("FETCH FIRST 25 ROWS ONLY", OracleProviderCapabilities.Instance.BuildJobLeaseSql(25));
    }
}
