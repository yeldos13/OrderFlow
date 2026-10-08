namespace OrderFlow.Contracts.Orders;

public interface IOrderEvent
{
    Guid EventId { get; }

    Guid OrderId { get; }

    DateTimeOffset OccurredAt { get; }
}

public sealed record OrderCreated(
    Guid EventId,
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderCreatedItem> Items,
    DateTimeOffset OccurredAt) : IOrderEvent;

public sealed record OrderCreatedItem(Guid ProductId, int Quantity, decimal UnitPrice);

public sealed record OrderConfirmed(Guid EventId, Guid OrderId, DateTimeOffset OccurredAt) : IOrderEvent;

public sealed record OrderShipped(Guid EventId, Guid OrderId, DateTimeOffset OccurredAt) : IOrderEvent;

public sealed record OrderCancelled(Guid EventId, Guid OrderId, DateTimeOffset OccurredAt) : IOrderEvent;
