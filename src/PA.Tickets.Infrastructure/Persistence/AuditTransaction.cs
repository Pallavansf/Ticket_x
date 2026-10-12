using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PA.Tickets.Infrastructure.Persistence;

public static class AuditTransaction
{
    public static async ValueTask<IDbContextTransaction?> BeginIfNeededAsync(DbContext db, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            return null;

        return await db.Database.BeginTransactionAsync(ct);
    }

    public static Task CommitIfOwnedAsync(this IDbContextTransaction? transaction, CancellationToken ct) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(ct);
}
