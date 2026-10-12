using System.Text.Json;

namespace OrderFlow.Consumer.Warehouse;

public sealed record WarehouseOrderEvent(
    Guid EventId,
    string EventType,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    JsonElement Data);
