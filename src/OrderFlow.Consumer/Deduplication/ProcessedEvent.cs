namespace OrderFlow.Consumer.Deduplication;

public sealed class ProcessedEvent
{
    private ProcessedEvent()
    {
    }

    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = null!;

    public Guid OrderId { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedEvent Create(Guid eventId, string eventType, Guid orderId, DateTimeOffset processedAt) => new()
    {
        EventId = eventId,
        EventType = eventType,
        OrderId = orderId,
        ProcessedAt = processedAt
    };
}
