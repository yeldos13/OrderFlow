using System.Collections.Concurrent;

namespace OrderFlow.WarehouseStub.Events;

public sealed class ReceivedEventStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, ReceivedEvent> _events = new();

    public ReceivedEvent Record(OrderEventRequest request)
    {
        var now = timeProvider.GetUtcNow();

        return _events.AddOrUpdate(
            request.EventId,
            _ => new ReceivedEvent(request.EventId, request.EventType, request.OrderId, 1, now, now),
            (_, existing) => existing with
            {
                Deliveries = existing.Deliveries + 1,
                LastReceivedAt = now
            });
    }

    public IReadOnlyList<ReceivedEvent> GetAll() =>
        [.. _events.Values.OrderBy(received => received.FirstReceivedAt)];

    public void Clear() => _events.Clear();
}

public sealed record ReceivedEvent(
    Guid EventId,
    string EventType,
    Guid OrderId,
    int Deliveries,
    DateTimeOffset FirstReceivedAt,
    DateTimeOffset LastReceivedAt);
