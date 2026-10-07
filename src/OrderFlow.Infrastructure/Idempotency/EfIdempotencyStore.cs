using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using OrderFlow.Application.Idempotency;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Idempotency;

internal sealed class EfIdempotencyStore(OrderFlowDbContext dbContext, TimeProvider timeProvider) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken) =>
        dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.Key == key, cancellationToken);

    public async Task<IIdempotencyScope?> TryBeginAsync(string key, string requestHash, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var record = IdempotencyRecord.Start(key, requestHash, timeProvider.GetUtcNow());

        dbContext.IdempotencyRecords.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            dbContext.ChangeTracker.Clear();
            return null;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        return new Scope(dbContext, transaction, record);
    }

    private sealed class Scope(OrderFlowDbContext dbContext, IDbContextTransaction transaction, IdempotencyRecord record)
        : IIdempotencyScope
    {
        public async Task CompleteAsync(
            int statusCode,
            string? contentType,
            string? body,
            string? location,
            CancellationToken cancellationToken)
        {
            record.Complete(statusCode, contentType, body, location);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
