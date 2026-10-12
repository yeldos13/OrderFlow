using System.Text.Json;
using OrderFlow.Consumer.Warehouse;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;

namespace OrderFlow.Consumer.Processing;

public sealed partial class OrderEventHandler(WarehouseClient warehouseClient, ILogger<OrderEventHandler> logger)
{
    public async Task HandleAsync(ReceivedMessage message, CancellationToken cancellationToken)
    {
        if (message.EventType is null || !OrderEventTypes.TryGetType(message.EventType, out var eventClrType))
        {
            throw new UnsupportedMessageException($"Unknown event type '{message.EventType}'.");
        }

        IOrderEvent orderEvent;
        JsonElement data;

        try
        {
            orderEvent = (IOrderEvent?)JsonSerializer.Deserialize(message.Value, eventClrType, EventSerializer.Options)
                ?? throw new UnsupportedMessageException("Message body is empty.");
            data = JsonSerializer.Deserialize<JsonElement>(message.Value);
        }
        catch (JsonException exception)
        {
            throw new UnsupportedMessageException("Message body is not a valid event.", exception);
        }

        await warehouseClient.SendAsync(
            new WarehouseOrderEvent(orderEvent.EventId, message.EventType, orderEvent.OrderId, orderEvent.OccurredAt, data),
            cancellationToken);

        LogEventDelivered(message.EventType, orderEvent.EventId, orderEvent.OrderId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivered {EventType} {OrderEventId} for order {OrderId} to warehouse")]
    private partial void LogEventDelivered(string eventType, Guid orderEventId, Guid orderId);
}
