using System.Text.Json;

namespace OrderFlow.WarehouseStub.Events;

public sealed record OrderEventRequest(
    Guid EventId,
    string EventType,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    JsonElement Data);
