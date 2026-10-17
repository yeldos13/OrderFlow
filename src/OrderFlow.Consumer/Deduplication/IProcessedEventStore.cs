namespace OrderFlow.Consumer.Deduplication;

public interface IProcessedEventStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid eventId, string eventType, Guid orderId, CancellationToken cancellationToken);
}
