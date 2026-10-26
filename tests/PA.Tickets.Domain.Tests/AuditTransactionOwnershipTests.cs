using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Domain.Tests;

public sealed class AuditTransactionOwnershipTests
{
    [Fact]
    public async Task Caller_owned_transaction_is_not_committed_by_audit_helper()
    {
        await ((IDbContextTransaction?)null).CommitIfOwnedAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Locally_owned_transaction_is_committed_by_audit_helper()
    {
        var transaction = new TrackingTransaction();

        await transaction.CommitIfOwnedAsync(CancellationToken.None);

        Assert.Equal(1, transaction.CommitCalls);
    }

    private sealed class TrackingTransaction : IDbContextTransaction
    {
        public int CommitCalls { get; private set; }
        public Guid TransactionId { get; } = Guid.NewGuid();
        public DbTransaction GetDbTransaction() => throw new NotSupportedException();
        public void Commit() => CommitCalls++;
        public Task CommitAsync(CancellationToken cancellationToken = default) { CommitCalls++; return Task.CompletedTask; }
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
