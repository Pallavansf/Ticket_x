using System.Data;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Infrastructure.Execution;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class TicketCreationIdempotencyStore(
    TicketsDbContext db,
    TimeProvider clock,
    ProviderTransactionRetry retry) : ITicketCreationIdempotencyStore
{
    private const string Scope = "TICKET_CREATE";
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(5);

    public Task<TicketCreationIdempotencyClaim> ClaimAsync(long actor, string key, string fingerprint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Idempotency-Key is required.");
        var trimmedKey = key.Trim();
        return retry.ExecuteAsync(db, async () =>
        {
            try
            {
                return await ClaimOnceAsync(actor, trimmedKey, fingerprint, ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var existing = await FindAsync(actor, trimmedKey, ct);
                if (existing is null) throw;
                return await ResolveExistingAsync(existing, actor, fingerprint, ct);
            }
        }, ct);
    }

    public Task SetTicketAsync(long actor, string key, long ticketId, CancellationToken ct)
        => retry.ExecuteAsync(db, async () =>
        {
            var row = await FindAsync(actor, key, ct) ?? throw new KeyNotFoundException("Ticket idempotency record was not found.");
            EnsureActor(row, actor);
            if (row.ResultTicketId.HasValue && row.ResultTicketId.Value != ticketId)
                throw new InvalidOperationException("Ticket idempotency result cannot be replaced with a different ticket.");
            row.ResultTicketId = ticketId;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public Task CompleteAsync(long actor, string key, CancellationToken ct)
        => retry.ExecuteAsync(db, async () =>
        {
            var row = await FindAsync(actor, key, ct) ?? throw new KeyNotFoundException("Ticket idempotency record was not found.");
            EnsureActor(row, actor);
            row.Status = "COMPLETED";
            row.CompletedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public Task FailAsync(long actor, string key, CancellationToken ct)
        => retry.ExecuteAsync(db, async () =>
        {
            var row = await FindAsync(actor, key, ct);
            if (row is null) return false;
            EnsureActor(row, actor);
            row.Status = "FAILED";
            row.CompletedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    private async Task<TicketCreationIdempotencyClaim> ClaimOnceAsync(long actor, string key, string fingerprint, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await FindAsync(actor, key, ct);
        if (row is not null)
        {
            var result = await ResolveExistingAsync(row, actor, fingerprint, ct);
            if (result.Replay)
            {
                await tx.CommitAsync(ct);
                return result;
            }

            if (result.Resume)
            {
                row.Status = "PROCESSING";
                row.CreatedAt = clock.GetUtcNow();
                row.CompletedAt = null;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }

            // Provably rolled back; update existing record to PROCESSING for retry
            row.Status = "PROCESSING";
            row.CreatedAt = clock.GetUtcNow();
            row.CompletedAt = null;
            row.ResultTicketId = null;
            row.Fingerprint = fingerprint;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(false, null);
        }

        row = new IdempotencyRow
        {
            Scope = Scope,
            Key = key,
            ActorId = actor,
            Fingerprint = fingerprint,
            Status = "PROCESSING",
            CreatedAt = clock.GetUtcNow()
        };
        db.Set<IdempotencyRow>().Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(false, null);
    }

    private async Task<TicketCreationIdempotencyClaim> ResolveExistingAsync(IdempotencyRow row, long actor, string fingerprint, CancellationToken ct)
    {
        EnsureActor(row, actor);
        if (!string.Equals(row.Fingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Idempotency-Key was reused for a different ticket request.");

        // 1. Check ResultTicketId
        if (row.ResultTicketId.HasValue)
        {
            var ticketExists = await db.Tickets.AnyAsync(x => x.Id == row.ResultTicketId.Value, ct);
            if (ticketExists)
            {
                if (row.Status == "COMPLETED")
                    return new(true, row.ResultTicketId.Value);
                if (row.Status == "FAILED")
                    return new(false, row.ResultTicketId.Value, Resume: true);
                if (row.Status == "PROCESSING" && clock.GetUtcNow() - row.CreatedAt > ProcessingTimeout)
                    return new(false, row.ResultTicketId.Value, Resume: true);
                if (row.Status == "PROCESSING")
                    throw new InvalidOperationException("The ticket request is already being processed.");
                throw new InvalidOperationException("The previous ticket creation ended in an ambiguous state and cannot be safely retried.");
            }

            // ResultTicketId is set but ticket is absent in DB
            if (row.Status == "FAILED")
            {
                return new(false, null);
            }

            throw new InvalidOperationException("The previous ticket creation ended in an ambiguous state and cannot be safely retried.");
        }

        // 3. If the operation is provably rolled back, allow retry.
        if (row.Status == "FAILED")
        {
            return new(false, null);
        }

        if (row.Status == "COMPLETED")
        {
            throw new InvalidOperationException("Idempotency record is marked COMPLETED but no ticket result was recorded.");
        }

        // row.Status == "PROCESSING"
        var isExpired = clock.GetUtcNow() - row.CreatedAt > ProcessingTimeout;
        if (!isExpired)
        {
            throw new InvalidOperationException("The ticket request is already being processed.");
        }

        // 4. If the state is ambiguous, fail safely rather than creating a duplicate ticket.
        throw new InvalidOperationException("The previous ticket creation request timed out in an ambiguous state and cannot be safely retried.");
    }

    private Task<IdempotencyRow?> FindAsync(long actor, string key, CancellationToken ct)
        => db.Set<IdempotencyRow>().SingleOrDefaultAsync(x => x.Scope == Scope && x.ActorId == actor && x.Key == key, ct);

    private static void EnsureActor(IdempotencyRow row, long actor)
    {
        if (row.ActorId.HasValue && row.ActorId.Value != actor)
            throw new UnauthorizedAccessException("The idempotency key belongs to another actor.");
    }
}
