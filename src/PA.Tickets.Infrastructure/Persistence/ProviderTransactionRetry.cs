using Microsoft.EntityFrameworkCore;

namespace PA.Tickets.Infrastructure.Persistence;

public sealed class ProviderTransactionRetry(IDatabaseProviderCapabilities capabilities)
{
    public async Task<T> ExecuteAsync<T>(DbContext db, Func<Task<T>> operation, CancellationToken ct)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception exception) when (attempt < maxAttempts &&
                (capabilities.IsTransientTransactionFailure(exception) || exception is DbUpdateConcurrencyException))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
            }
        }
    }

    public Task<T> ExecuteAsync<T>(DbContext db, Func<Task<T>> operation)
        => ExecuteAsync(db, operation, CancellationToken.None);
}
