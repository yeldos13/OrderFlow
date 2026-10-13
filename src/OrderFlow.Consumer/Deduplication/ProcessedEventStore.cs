using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderFlow.Consumer.Persistence;

namespace OrderFlow.Consumer.Deduplication;

public sealed class ProcessedEventStore(ConsumerDbContext dbContext, TimeProvider timeProvider)
{
    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        dbContext.ProcessedEvents.AnyAsync(processedEvent => processedEvent.EventId == eventId, cancellationToken);

    public async Task MarkProcessedAsync(Guid eventId, string eventType, Guid orderId, CancellationToken cancellationToken)
    {
        dbContext.ProcessedEvents.Add(ProcessedEvent.Create(eventId, eventType, orderId, timeProvider.GetUtcNow()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.ChangeTracker.Clear();
        }
    }
}
