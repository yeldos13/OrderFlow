using System.Text.Json;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders.Events;

namespace OrderFlow.Infrastructure.Outbox;

internal static class OutboxMessageFactory
{
    public static OutboxMessage Create(IDomainEvent domainEvent, string? traceParent)
    {
        var eventId = Guid.CreateVersion7(domainEvent.OccurredAt);

        IOrderEvent integrationEvent = domainEvent switch
        {
            OrderCreatedDomainEvent created => new OrderCreated(
                eventId,
                created.OrderId,
                created.CustomerId,
                created.TotalAmount,
                [.. created.Lines.Select(line => new OrderCreatedItem(line.ProductId, line.Quantity, line.UnitPrice))],
                created.OccurredAt),
            OrderConfirmedDomainEvent confirmed => new OrderConfirmed(eventId, confirmed.OrderId, confirmed.OccurredAt),
            OrderShippedDomainEvent shipped => new OrderShipped(eventId, shipped.OrderId, shipped.OccurredAt),
            OrderCancelledDomainEvent cancelled => new OrderCancelled(eventId, cancelled.OrderId, cancelled.OccurredAt),
            _ => throw new InvalidOperationException(
                $"Domain event {domainEvent.GetType().Name} has no integration event mapping.")
        };

        return OutboxMessage.Create(
            eventId,
            integrationEvent.OrderId,
            OrderEventTypes.GetName(integrationEvent),
            JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), EventSerializer.Options),
            traceParent,
            integrationEvent.OccurredAt);
    }
}
